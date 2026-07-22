using Microsoft.ML.OnnxRuntime;

namespace Scrapers.Services.Enrichment;

public sealed class BertNpiScorer : IDisposable
{
    private readonly string _modelPath;
    private readonly string _vocabPath;
    private readonly int _maxLength;
    private InferenceSession? _session;
    private Dictionary<string, int>? _vocab;
    private int _embeddingDim;

    public bool IsAvailable { get; private set; }
    public string ModelPath => _modelPath;

    public BertNpiScorer(string modelPath, string vocabPath, int maxLength = 128)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(vocabPath);

        _modelPath = modelPath;
        _vocabPath = vocabPath;
        _maxLength = maxLength;
    }

    public void Load()
    {
        if (!File.Exists(_modelPath))
        {
            System.Diagnostics.Debug.WriteLine($"BERT model not found at {_modelPath}");
            IsAvailable = false;
            return;
        }

        if (!File.Exists(_vocabPath))
        {
            System.Diagnostics.Debug.WriteLine($"BERT vocab not found at {_vocabPath}");
            IsAvailable = false;
            return;
        }

        try
        {
            _session = new InferenceSession(_modelPath);
            _vocab = LoadVocab(_vocabPath);
            var outputNames = _session.OutputNames;
            var outputMeta = _session.OutputMetadata;
            _embeddingDim = outputMeta[outputNames[0]].Dimensions[^1];
            IsAvailable = true;
        }
        catch (OnnxRuntimeException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load BERT model: {ex.Message}");
            IsAvailable = false;
        }
    }

    public float[] ComputeEmbedding(string text)
    {
        if (!IsAvailable || _session == null || _vocab == null)
            throw new InvalidOperationException("BERT model is not available. Check IsAvailable before calling.");

        var tokenized = Tokenize(text);

        using var inputIdsOrt = OrtValue.CreateTensorValueFromMemory(tokenized.InputIds, [1, _maxLength]);
        using var attentionMaskOrt = OrtValue.CreateTensorValueFromMemory(tokenized.AttentionMask, [1, _maxLength]);
        using var tokenTypeIdsOrt = OrtValue.CreateTensorValueFromMemory(tokenized.TokenTypeIds, [1, _maxLength]);

        var inputs = new Dictionary<string, OrtValue>
        {
            ["input_ids"] = inputIdsOrt,
            ["attention_mask"] = attentionMaskOrt,
            ["token_type_ids"] = tokenTypeIdsOrt
        };

        using var runOptions = new RunOptions();
        using var outputs = _session.Run(runOptions, inputs, _session.OutputNames);

        var embeddingOutput = outputs[0];
        var embedding = embeddingOutput.GetTensorDataAsSpan<float>().ToArray();

        var pooled = MeanPool(embedding, tokenized.AttentionMask, _maxLength, _embeddingDim);
        L2Normalize(pooled);

        return pooled;
    }

    public static float CosineSimilarity(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length != b.Length)
            throw new ArgumentException("Embedding dimensions must match");

        float dotProduct = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        float magnitude = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return magnitude < 1e-9f ? 0 : dotProduct / magnitude;
    }

    private (long[] InputIds, long[] AttentionMask, long[] TokenTypeIds) Tokenize(string text)
    {
        if (_vocab == null)
            throw new InvalidOperationException("Vocab not loaded");

        var tokens = WordPieceTokenize(text);
        var inputIds = new long[_maxLength];
        var attentionMask = new long[_maxLength];
        var tokenTypeIds = new long[_maxLength];

        var clsId = _vocab.GetValueOrDefault("[CLS]", 101);
        var sepId = _vocab.GetValueOrDefault("[SEP]", 102);
        var padId = _vocab.GetValueOrDefault("[PAD]", 0);

        inputIds[0] = clsId;
        attentionMask[0] = 1;

        int pos = 1;
        foreach (var token in tokens)
        {
            if (pos >= _maxLength - 1)
                break;

            inputIds[pos] = _vocab.GetValueOrDefault(token, _vocab.GetValueOrDefault("[UNK]", 100));
            attentionMask[pos] = 1;
            pos++;
        }

        if (pos < _maxLength)
        {
            inputIds[pos] = sepId;
            attentionMask[pos] = 1;
            pos++;
        }

        while (pos < _maxLength)
        {
            inputIds[pos] = padId;
            pos++;
        }

        return (inputIds, attentionMask, tokenTypeIds);
    }

    private List<string> WordPieceTokenize(string text)
    {
        if (_vocab == null)
            return [];

        var tokens = new List<string>();
        text = text.Normalize(System.Text.NormalizationForm.FormKD);
        text = text.ToLowerInvariant();

        var splitWords = SplitOnPunctAndWhitespace(text);

        foreach (var word in splitWords)
        {
            if (_vocab.ContainsKey(word))
            {
                tokens.Add(word);
            }
            else
            {
                var subTokens = WordPiece(word);
                tokens.AddRange(subTokens);
            }
        }

        return tokens;
    }

    private static List<string> SplitOnPunctAndWhitespace(string text)
    {
        var words = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsPunctuation(c))
            {
                if (current.Length > 0)
                {
                    words.Add(current.ToString());
                    current.Clear();
                }
                if (!char.IsWhiteSpace(c))
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

    private List<string> WordPiece(string word)
    {
        if (_vocab == null)
            return ["[UNK]"];

        var tokens = new List<string>();
        var remaining = word;

        while (remaining.Length > 0)
        {
            int longestMatch = 0;
            string? bestSub = null;

            for (int len = remaining.Length; len >= 1; len--)
            {
                string sub = tokens.Count == 0
                    ? remaining[..len]
                    : "##" + remaining[..len];

                if (_vocab.ContainsKey(sub))
                {
                    longestMatch = len;
                    bestSub = sub;
                    break;
                }
            }

            if (bestSub != null)
            {
                tokens.Add(bestSub);
                remaining = remaining[longestMatch..];
            }
            else
            {
                tokens.Add("[UNK]");
                break;
            }
        }

        return tokens;
    }

    private static float[] MeanPool(float[] sequenceOutput, long[] attentionMask, int seqLength, int embeddingDim)
    {
        var pooled = new float[embeddingDim];
        float maskSum = 0;

        for (int i = 0; i < seqLength; i++)
        {
            if (attentionMask[i] == 0) continue;
            maskSum++;
            for (int j = 0; j < embeddingDim; j++)
            {
                pooled[j] += sequenceOutput[i * embeddingDim + j];
            }
        }

        if (maskSum > 0)
        {
            for (int j = 0; j < embeddingDim; j++)
                pooled[j] /= maskSum;
        }

        return pooled;
    }

    private static void L2Normalize(float[] vector)
    {
        float sum = 0;
        for (int i = 0; i < vector.Length; i++)
            sum += vector[i] * vector[i];

        float norm = MathF.Sqrt(sum);
        if (norm > 1e-9f)
        {
            for (int i = 0; i < vector.Length; i++)
                vector[i] /= norm;
        }
    }

    private static Dictionary<string, int> LoadVocab(string vocabPath)
    {
        var vocab = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StreamReader(vocabPath);
        string? line;
        int index = 0;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !vocab.ContainsKey(trimmed))
                vocab[trimmed] = index;
            index++;
        }
        return vocab;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
