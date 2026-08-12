using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Scrapers.Models;

namespace Scrapers.Services;

public sealed class MeSHMatcher : IDisposable
{
    // Measured sweet spot for batched ONNX inference (issue #434 follow-up):
    // N=16 runs ~1.22x faster than N=1 with bit-identical scores.
    private const int MaxBatchSize = 16;
    private const int DefaultClsTokenId = 101;
    private const int DefaultSepTokenId = 102;
    private const int DefaultUnkTokenId = 100;
    private const int DefaultPadTokenId = 0;
    private const float DefaultMatchThreshold = 0.65f;

    private readonly InferenceSession _session;
    private readonly int _maxSeqLen;
    private readonly int _embedDim;
    private readonly int _clsTokenId;
    private readonly int _sepTokenId;
    private readonly int _unkTokenId;
    private readonly int _padTokenId;
    private readonly bool _doLowerCase;
    private readonly float _matchThreshold;
    private readonly Dictionary<string, int> _vocab;
    private readonly string[] _meshNames;
    private readonly string[] _meshCuis;
    private readonly string[][] _meshTreeNumbers;
    private readonly string[] _meshCategories;
    private readonly float[] _meshEmbeddings;
    private readonly int[] _meshEmbeddingIndex;
    private readonly int[] _meshSearchOrder;
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
        _maxSeqLen = GetSequenceLength(_session.InputMetadata["input_ids"].Dimensions);

        var vocabPath = Path.Combine(resourcesPath, "vocab.txt");
        _vocab = LoadVocab(vocabPath);
        _clsTokenId = GetTokenId(_vocab, "[CLS]", DefaultClsTokenId);
        _sepTokenId = GetTokenId(_vocab, "[SEP]", DefaultSepTokenId);
        _unkTokenId = GetTokenId(_vocab, "[UNK]", DefaultUnkTokenId);
        _padTokenId = GetTokenId(_vocab, "[PAD]", DefaultPadTokenId);
        _doLowerCase = LoadDoLowerCase(Path.Combine(resourcesPath, "tokenizer_config.json"));
        _matchThreshold = LoadMatchThreshold(Path.Combine(resourcesPath, "matcher_config.json"));

        var termsPath = Path.Combine(resourcesPath, "mesh_terms.json");
        (_meshNames, _meshCuis, _meshTreeNumbers, _meshCategories) = LoadMeshTerms(termsPath);

        var embPath = Path.Combine(resourcesPath, "mesh_embeddings.bin");
        _meshEmbeddings = LoadMeshEmbeddings(embPath, out int uniqueTerms, out _embedDim);

        var idxPath = Path.Combine(resourcesPath, "mesh_term_index.bin");
        _meshEmbeddingIndex = LoadIndex(idxPath, _meshNames.Length);

        if (_meshEmbeddings.Length != uniqueTerms * _embedDim)
            throw new InvalidOperationException($"Embedding buffer wrong size: {_meshEmbeddings.Length} (expected {uniqueTerms * _embedDim})");
        if (_meshEmbeddingIndex.Length != _meshNames.Length)
            throw new InvalidOperationException($"Index length mismatch: {_meshEmbeddingIndex.Length} (expected {_meshNames.Length})");

        // Multiple aliases can point to the same CUI embedding. Keep the first
        // alias for each embedding so tie resolution remains identical to the
        // original alias-by-alias scan without repeating the dot product.
        _meshSearchOrder = BuildSearchOrder(_meshEmbeddingIndex);

        _meshNameLookup = new Dictionary<string, int>(_meshNames.Length, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _meshNames.Length; i++)
            _meshNameLookup[_meshNames[i]] = i;
    }

    public MeSHMatchResult Match(string value, string source = "condition", string studyNctId = "")
    {
        ArgumentNullException.ThrowIfNull(value);

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

                if (embedding == null || embedding.Length != _embedDim)
                    throw new InvalidOperationException($"Embedding has wrong size: {embedding?.Length ?? 0} (expected {_embedDim})");

                (bestIdx, bestScore) = FindBestMatch(embedding);
            }

            _matchCache.Add(MeSHMatchCache.NormalizeKey(value), bestIdx, bestScore);
        }

        return BuildResult(value, source, studyNctId, bestIdx, bestScore);
    }

    /// <summary>
    /// Matches many terms in one pass: dedupes, reuses cached and exact-name
    /// hits, and runs ONNX inference in chunks of <see cref="MaxBatchSize"/>
    /// (single forward pass per chunk instead of one per term).
    /// </summary>
    public IReadOnlyList<MeSHMatchResult> MatchBatch(IEnumerable<string> values, string source = "condition", string studyNctId = "")
    {
        ArgumentNullException.ThrowIfNull(values);
        var list = values.ToList();

        // Resolve per unique term: memo cache -> exact-name -> batched inference.
        var resolved = new Dictionary<string, (int BestIdx, float BestScore)>(StringComparer.Ordinal);
        var pending = new List<string>();
        var pendingKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var value in list)
        {
            ArgumentNullException.ThrowIfNull(value);
            var key = MeSHMatchCache.NormalizeKey(value);
            if (resolved.ContainsKey(key))
            {
                continue;
            }

            if (_matchCache.TryGet(key, out int cachedIdx, out float cachedScore))
            {
                resolved[key] = (cachedIdx, cachedScore);
                continue;
            }

            if (_meshNameLookup.TryGetValue(value.Trim(), out int exactIdx))
            {
                resolved[key] = (exactIdx, 1f);
                _matchCache.Add(key, exactIdx, 1f);
                continue;
            }

            if (pendingKeys.Add(key))
            {
                pending.Add(value);
            }
        }

        for (int start = 0; start < pending.Count; start += MaxBatchSize)
        {
            var chunk = pending.GetRange(start, Math.Min(MaxBatchSize, pending.Count - start));
            var tokenized = chunk.Select(Tokenize).ToList();
            var embeddings = ComputeEmbeddingsBatch(tokenized);

            for (int i = 0; i < chunk.Count; i++)
            {
                var (bestIdx, bestScore) = FindBestMatch(embeddings[i]);
                var key = MeSHMatchCache.NormalizeKey(chunk[i]);
                resolved[key] = (bestIdx, bestScore);
                _matchCache.Add(key, bestIdx, bestScore);
            }
        }

        var results = new List<MeSHMatchResult>(list.Count);
        foreach (var value in list)
        {
            var (bestIdx, bestScore) = resolved[MeSHMatchCache.NormalizeKey(value)];
            results.Add(BuildResult(value, source, studyNctId, bestIdx, bestScore));
        }

        return results;
    }

    internal int CacheHits => _matchCache.CacheHits;

    internal int AliasCount => _meshNames.Length;

    internal int SearchCandidateCount => _meshSearchOrder.Length;

    private float[] ComputeEmbedding(int[] tokenIds, int[] attentionMask)
    {
        var inputIds = new DenseTensor<long>([1, _maxSeqLen]);
        var mask = new DenseTensor<long>([1, _maxSeqLen]);

        for (int i = 0; i < _maxSeqLen; i++)
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

        var pooled = new float[_embedDim];
        int validTokens = 0;
        for (int i = 0; i < _maxSeqLen; i++)
        {
            if (attentionMask[i] == 0) continue;
            validTokens++;
            for (int j = 0; j < _embedDim; j++)
            {
                pooled[j] += hiddenState[0, i, j];
            }
        }

        if (validTokens > 0)
        {
            float invCount = 1f / validTokens;
            for (int i = 0; i < _embedDim; i++)
                pooled[i] *= invCount;
        }

        float norm = 0f;
        for (int i = 0; i < _embedDim; i++)
            norm += pooled[i] * pooled[i];
        norm = MathF.Sqrt(norm);

        if (norm > 1e-10f)
        {
            float invNorm = 1f / norm;
            for (int i = 0; i < _embedDim; i++)
                pooled[i] *= invNorm;
        }

        return pooled;
    }

    private float CosineSimilarity(float[] embedding, int meshIdx)
    {
        float dot = 0f;
        int offset = meshIdx * _embedDim;
        for (int i = 0; i < _embedDim; i++)
            dot += embedding[i] * _meshEmbeddings[offset + i];
        return dot;
    }

    /// <summary>
    /// Cosine scan over each unique MeSH CUI embedding for one query embedding.
    /// Search order is the first alias order, so this remains bit-identical to
    /// scanning every alias while avoiding duplicate dot products. Shared by
    /// the single-term and batched paths so both pick the same best match.
    /// </summary>
    private (int BestIdx, float BestScore) FindBestMatch(float[] embedding)
    {
        int bestIdx = -1;
        float bestScore = 0f;
        for (int i = 0; i < _meshSearchOrder.Length; i++)
        {
            int meshNameIndex = _meshSearchOrder[i];
            float sim = CosineSimilarity(embedding, _meshEmbeddingIndex[meshNameIndex]);
            if (sim > bestScore)
            {
                bestScore = sim;
                bestIdx = meshNameIndex;
            }
        }

        return (bestIdx, bestScore);
    }

    internal static int[] BuildSearchOrder(IReadOnlyList<int> embeddingIndex)
    {
        ArgumentNullException.ThrowIfNull(embeddingIndex);

        var seen = new HashSet<int>();
        var searchOrder = new List<int>(embeddingIndex.Count);
        for (int i = 0; i < embeddingIndex.Count; i++)
        {
            if (seen.Add(embeddingIndex[i]))
                searchOrder.Add(i);
        }

        return searchOrder.ToArray();
    }

    private MeSHMatchResult BuildResult(string value, string source, string studyNctId, int bestIdx, float bestScore)
    {
        bool matched = bestIdx >= 0 && bestScore >= _matchThreshold;

        return new MeSHMatchResult
        {
            Value = value,
            StudyNctId = studyNctId,
            Source = source,
            SideBMatched = matched,
            MeshTerm = matched ? _meshNames[bestIdx] : "",
            MeshCui = matched ? _meshCuis[bestIdx] : "",
            Category = matched ? _meshCategories[bestIdx] : "unmapped",
            Similarity = bestScore,
        };
    }

    /// <summary>
    /// Mean-pools every row of a batched forward pass. Row r pools
    /// hiddenState[r, i, j] with the same math as <see cref="ComputeEmbedding"/>
    /// (attention-masked mean over valid tokens, then L2 normalization), so
    /// per-row results are identical to N=1 inference.
    /// </summary>
    private float[][] ComputeEmbeddingsBatch(List<(int[] TokenIds, int[] AttentionMask)> tokenized)
    {
        int batchSize = tokenized.Count;
        var inputIds = new DenseTensor<long>([batchSize, _maxSeqLen]);
        var mask = new DenseTensor<long>([batchSize, _maxSeqLen]);

        for (int r = 0; r < batchSize; r++)
        {
            for (int i = 0; i < _maxSeqLen; i++)
            {
                inputIds[r, i] = tokenized[r].TokenIds[i];
                mask[r, i] = tokenized[r].AttentionMask[i];
            }
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", mask),
        };

        using var results = _session.Run(inputs);
        var hiddenState = results[0].AsTensor<float>();

        var pooled = new float[batchSize][];
        for (int r = 0; r < batchSize; r++)
        {
            var row = new float[_embedDim];
            int validTokens = 0;
            for (int i = 0; i < _maxSeqLen; i++)
            {
                if (tokenized[r].AttentionMask[i] == 0) continue;
                validTokens++;
                for (int j = 0; j < _embedDim; j++)
                {
                    row[j] += hiddenState[r, i, j];
                }
            }

            if (validTokens > 0)
            {
                float invCount = 1f / validTokens;
                for (int i = 0; i < _embedDim; i++)
                    row[i] *= invCount;
            }

            float norm = 0f;
            for (int i = 0; i < _embedDim; i++)
                norm += row[i] * row[i];
            norm = MathF.Sqrt(norm);

            if (norm > 1e-10f)
            {
                float invNorm = 1f / norm;
                for (int i = 0; i < _embedDim; i++)
                    row[i] *= invNorm;
            }

            pooled[r] = row;
        }

        return pooled;
    }

    private (int[] TokenIds, int[] AttentionMask) Tokenize(string text)
    {
        var tokens = new List<int> { _clsTokenId };
        // Follow the selected tokenizer bundle: uncased tokenizers lower
        // queries before WordPiece tokenization.
        var cleaned = RemoveDiacritics(text);
        if (_doLowerCase)
            cleaned = cleaned.ToLowerInvariant();
        var words = SplitOnPunctuation(cleaned);

        foreach (var word in words)
        {
            if (string.IsNullOrEmpty(word)) continue;
            var wordTokens = WordPiece(word);
            tokens.AddRange(wordTokens);
        }

        tokens.Add(_sepTokenId);

        if (tokens.Count > _maxSeqLen)
            tokens = tokens[.._maxSeqLen];

        var tokenIds = new int[_maxSeqLen];
        var attentionMask = new int[_maxSeqLen];

        for (int i = 0; i < tokens.Count; i++)
        {
            tokenIds[i] = tokens[i];
            attentionMask[i] = 1;
        }

        for (int i = tokens.Count; i < _maxSeqLen; i++)
        {
            tokenIds[i] = _padTokenId;
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
            int bestId = _unkTokenId;
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
                tokens.Add(_unkTokenId);
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

    internal static int GetSequenceLength(IReadOnlyList<int> dimensions)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        if (dimensions.Count < 2 || dimensions[1] <= 0)
            throw new InvalidOperationException("The input_ids model shape must have a fixed sequence length");

        return dimensions[1];
    }

    private static int GetTokenId(Dictionary<string, int> vocab, string token, int fallback)
    {
        return vocab.TryGetValue(token, out int id) ? id : fallback;
    }

    internal static bool LoadDoLowerCase(string path)
    {
        if (!File.Exists(path))
            return false;

        using var file = File.OpenRead(path);
        using var document = JsonDocument.Parse(file);
        return document.RootElement.TryGetProperty("do_lower_case", out var value) && value.GetBoolean();
    }

    internal static float LoadMatchThreshold(string path)
    {
        if (!File.Exists(path))
            return DefaultMatchThreshold;

        using var file = File.OpenRead(path);
        using var document = JsonDocument.Parse(file);
        if (!document.RootElement.TryGetProperty("match_threshold", out var value))
            return DefaultMatchThreshold;

        float threshold = value.GetSingle();
        if (threshold is < 0 or > 1)
            throw new InvalidOperationException("The MeSH match threshold must be between 0 and 1");

        return threshold;
    }

    private static (string[] Names, string[] Cuis, string[][] TreeNumbers, string[] Categories) LoadMeshTerms(string path)
    {
        using var file = File.OpenRead(path);
        var data = JsonSerializer.Deserialize<MeshTermsData>(file, _jsonOptions)
                   ?? throw new InvalidOperationException("Failed to deserialize mesh_terms.json");
        return (data.Names, data.Cuis, data.TreeNumbers, data.Categories);
    }

    private static float[] LoadMeshEmbeddings(string path, out int numTerms, out int embeddingDimension)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);

        int rows = br.ReadInt32();
        int cols = br.ReadInt32();
        numTerms = rows;
        embeddingDimension = cols;

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
