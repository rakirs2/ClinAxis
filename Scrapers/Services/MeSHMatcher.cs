using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Scrapers.Models;

namespace Scrapers.Services;

public sealed class MeSHMatcher : IDisposable
{
    private const int MaxSeqLen = 128;
    // S-BioBert-snli-multinli-stsb embedding dimension (issue #355 P4-e,
    // supersedes all-MiniLM-L6-v2's 384).
    private const int EmbedDim = 768;
    private const int ClsTokenId = 101;
    private const int SepTokenId = 102;
    private const int UnkTokenId = 100;
    private const int PadTokenId = 0;

    private readonly InferenceSession _session;
    private readonly Dictionary<string, int> _vocab;
    private readonly string[] _meshNames;
    private readonly string[] _meshCuis;
    private readonly string[][] _meshTreeNumbers;
    private readonly string[] _meshCategories;
    private readonly float[] _meshEmbeddings;
    private readonly int[] _meshEmbeddingIndex;
    private readonly Dictionary<string, int> _meshNameLookup;
    private readonly MeSHMatchCache _matchCache = new();

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly char[] PunctuationChars = [
        '!', '"', '#', '$', '%', '&', '\'', '(', ')', '*', '+', ',', '-', '.',
        '/', ':', ';', '<', '=', '>', '?', '@', '[', '\\', ']', '^', '`',
        '{', '|', '}', '~'
    ];

    public MeSHMatcher(string resourcesPath)
    {
        ArgumentNullException.ThrowIfNull(resourcesPath);
        _session = new InferenceSession(Path.Combine(resourcesPath, "model.onnx"));

        var vocabPath = Path.Combine(resourcesPath, "vocab.txt");
        _vocab = LoadVocab(vocabPath);

        var termsPath = Path.Combine(resourcesPath, "mesh_terms.json");
        (_meshNames, _meshCuis, _meshTreeNumbers, _meshCategories) = LoadMeshTerms(termsPath);

        var embPath = Path.Combine(resourcesPath, "mesh_embeddings.bin");
        _meshEmbeddings = LoadMeshEmbeddings(embPath, out int uniqueTerms);

        var idxPath = Path.Combine(resourcesPath, "mesh_term_index.bin");
        _meshEmbeddingIndex = LoadIndex(idxPath, _meshNames.Length);

        if (_meshEmbeddings.Length != uniqueTerms * EmbedDim)
            throw new InvalidOperationException($"Embedding buffer wrong size: {_meshEmbeddings.Length} (expected {uniqueTerms * EmbedDim})");
        if (_meshEmbeddingIndex.Length != _meshNames.Length)
            throw new InvalidOperationException($"Index length mismatch: {_meshEmbeddingIndex.Length} (expected {_meshNames.Length})");

        _meshNameLookup = new Dictionary<string, int>(_meshNames.Length, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _meshNames.Length; i++)
            _meshNameLookup[_meshNames[i]] = i;
    }

    public MeSHMatchResult Match(string value, string source = "condition", string studyNctId = "")
    {
        ArgumentNullException.ThrowIfNull(value);
        var sideA = IsValidConditionSimple(value);

        if (!_matchCache.TryGet(MeSHMatchCache.NormalizeKey(value), out int bestIdx, out float bestScore))
        {
            if (_meshNameLookup.TryGetValue(value.Trim(), out int exactIdx))
            {
                bestIdx = exactIdx;
                bestScore = 1f;
            }
            else
            {
                var (tokenIds, attentionMask) = Tokenize(value);
                var embedding = ComputeEmbedding(tokenIds, attentionMask);

                if (embedding == null || embedding.Length != EmbedDim)
                    throw new InvalidOperationException($"Embedding has wrong size: {embedding?.Length ?? 0} (expected {EmbedDim})");

                for (int i = 0; i < _meshNames.Length; i++)
                {
                    float sim = CosineSimilarity(embedding, _meshEmbeddingIndex[i]);
                    if (sim > bestScore)
                    {
                        bestScore = sim;
                        bestIdx = i;
                    }
                }
            }

            _matchCache.Add(MeSHMatchCache.NormalizeKey(value), bestIdx, bestScore);
        }

        // Re-picked for S-BioBert-snli-multinli-stsb (issue #355 P4-e): on the
        // 139-keyword labeled set (ctgov-keywords.csv) 0.65 rescues 85.6% (vs
        // 50.4% at 0.8) and is the distribution knee (0.7 -> 74.8%). All
        // non-descriptor junk in the labeled set scores < 0.65 ("Type 1" 0.61,
        // "treatment" 0.61); junk that IS a MeSH descriptor ("Safety" 1.0)
        // is kept out by the KeywordFilter blocklist, not this threshold.
        const float threshold = 0.65f;
        bool matched = bestIdx >= 0 && bestScore >= threshold;

        return new MeSHMatchResult
        {
            Value = value,
            StudyNctId = studyNctId,
            Source = source,
            SideAValid = sideA,
            SideBMatched = matched,
            MeshTerm = matched ? _meshNames[bestIdx] : "",
            MeshCui = matched ? _meshCuis[bestIdx] : "",
            Category = matched ? _meshCategories[bestIdx] : "unmapped",
            Similarity = bestScore,
        };
    }

    public IReadOnlyList<MeSHMatchResult> MatchBatch(IEnumerable<string> values, string source = "condition", string studyNctId = "")
    {
        return values.Select(v => Match(v, source, studyNctId)).ToList();
    }

    internal int CacheHits => _matchCache.CacheHits;

    private float[] ComputeEmbedding(int[] tokenIds, int[] attentionMask)
    {
        var inputIds = new DenseTensor<long>([1, MaxSeqLen]);
        var mask = new DenseTensor<long>([1, MaxSeqLen]);

        for (int i = 0; i < MaxSeqLen; i++)
        {
            inputIds[0, i] = tokenIds[i];
            mask[0, i] = attentionMask[i];
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", mask),
        };

        using var results = _session.Run(inputs);
        var hiddenState = results[0].AsTensor<float>();

        var pooled = new float[EmbedDim];
        int validTokens = 0;
        for (int i = 0; i < MaxSeqLen; i++)
        {
            if (attentionMask[i] == 0) continue;
            validTokens++;
            for (int j = 0; j < EmbedDim; j++)
            {
                pooled[j] += hiddenState[0, i, j];
            }
        }

        if (validTokens > 0)
        {
            float invCount = 1f / validTokens;
            for (int i = 0; i < EmbedDim; i++)
                pooled[i] *= invCount;
        }

        float norm = 0f;
        for (int i = 0; i < EmbedDim; i++)
            norm += pooled[i] * pooled[i];
        norm = MathF.Sqrt(norm);

        if (norm > 1e-10f)
        {
            float invNorm = 1f / norm;
            for (int i = 0; i < EmbedDim; i++)
                pooled[i] *= invNorm;
        }

        return pooled;
    }

    private float CosineSimilarity(float[] embedding, int meshIdx)
    {
        float dot = 0f;
        int offset = meshIdx * EmbedDim;
        for (int i = 0; i < EmbedDim; i++)
            dot += embedding[i] * _meshEmbeddings[offset + i];
        return dot;
    }

    private (int[] TokenIds, int[] AttentionMask) Tokenize(string text)
    {
        var tokens = new List<int> { ClsTokenId };
        // BioBERT is case-sensitive (tokenizer_config.json do_lower_case=false);
        // MeSH embeddings were pre-computed from cased terms, so queries must
        // keep their original case ("MI" and "mi" are different tokens).
        var cleaned = RemoveDiacritics(text);
        var words = SplitOnPunctuation(cleaned);

        foreach (var word in words)
        {
            if (string.IsNullOrEmpty(word)) continue;
            var wordTokens = WordPiece(word);
            tokens.AddRange(wordTokens);
        }

        tokens.Add(SepTokenId);

        if (tokens.Count > MaxSeqLen)
            tokens = tokens[..MaxSeqLen];

        var tokenIds = new int[MaxSeqLen];
        var attentionMask = new int[MaxSeqLen];

        for (int i = 0; i < tokens.Count; i++)
        {
            tokenIds[i] = tokens[i];
            attentionMask[i] = 1;
        }

        for (int i = tokens.Count; i < MaxSeqLen; i++)
        {
            tokenIds[i] = PadTokenId;
            attentionMask[i] = 0;
        }

        return (tokenIds, attentionMask);
    }

    private List<int> WordPiece(string word)
    {
        var tokens = new List<int>();

        if (_vocab.TryGetValue(word, out int id))
        {
            tokens.Add(id);
            return tokens;
        }

        var chars = word.AsSpan();
        int start = 0;
        while (start < chars.Length)
        {
            int bestLen = 0;
            int bestId = UnkTokenId;
            bool isFirst = start == 0;

            int maxLen = Math.Min(chars.Length - start, 20);
            for (int len = maxLen; len >= 1; len--)
            {
                var sub = chars.Slice(start, len);
                var candidate = isFirst
                    ? sub.ToString()
                    : "##" + sub.ToString();

                if (_vocab.TryGetValue(candidate, out int candidateId))
                {
                    bestLen = len;
                    bestId = candidateId;
                    break;
                }
            }

            if (bestLen == 0)
            {
                tokens.Add(UnkTokenId);
                break;
            }

            tokens.Add(bestId);
            start += bestLen;
        }

        return tokens;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (char c in normalized)
        {
            if (char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static List<string> SplitOnPunctuation(string text)
    {
        var words = new List<string>();
        var current = new StringBuilder();

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c) || PunctuationChars.Contains(c))
            {
                if (current.Length > 0)
                {
                    words.Add(current.ToString());
                    current.Clear();
                }
                if (PunctuationChars.Contains(c))
                    words.Add(c.ToString());
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            words.Add(current.ToString());

        return words;
    }

    private static bool IsValidConditionSimple(string condition)
    {
        if (condition.Contains('"', StringComparison.Ordinal) ||
            condition.Contains('\'', StringComparison.Ordinal))
            return false;

        if (condition.Contains('.', StringComparison.Ordinal))
            return false;

        if (s_icdRegex.IsMatch(condition))
            return false;

        return true;
    }

    private static readonly Regex s_icdRegex = new(
        @"\b[A-TV-Z][0-9][0-9AB]\.?[0-9]{0,4}\b|\b[0-9]{3}\.?[0-9]{0,2}\b",
        RegexOptions.Compiled);

    private static Dictionary<string, int> LoadVocab(string vocabPath)
    {
        var vocab = new Dictionary<string, int>(StringComparer.Ordinal);
        var lines = File.ReadAllLines(vocabPath);
        for (int i = 0; i < lines.Length; i++)
        {
            var token = lines[i].Trim();
            if (!string.IsNullOrEmpty(token))
                vocab[token] = i;
        }
        return vocab;
    }

    private static (string[] Names, string[] Cuis, string[][] TreeNumbers, string[] Categories) LoadMeshTerms(string path)
    {
        using var file = File.OpenRead(path);
        var data = JsonSerializer.Deserialize<MeshTermsData>(file, _jsonOptions)
                   ?? throw new InvalidOperationException("Failed to deserialize mesh_terms.json");
        return (data.Names, data.Cuis, data.TreeNumbers, data.Categories);
    }

    private static float[] LoadMeshEmbeddings(string path, out int numTerms)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);

        int rows = br.ReadInt32();
        int cols = br.ReadInt32();
        numTerms = rows;

        var buffer = new float[rows * cols];
        var byteBuffer = new byte[rows * cols * 4];
        br.Read(byteBuffer);
        Buffer.BlockCopy(byteBuffer, 0, buffer, 0, byteBuffer.Length);
        return buffer;
    }

    private static int[] LoadIndex(string path, int expectedLength)
    {
        var bytes = File.ReadAllBytes(path);
        var result = new int[expectedLength];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    private class MeshTermsData
    {
        public string[] Names { get; set; } = [];
        public string[] Cuis { get; set; } = [];
        public string[][] TreeNumbers { get; set; } = [];
        public string[] Categories { get; set; } = [];
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
