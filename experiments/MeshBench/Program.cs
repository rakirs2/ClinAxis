using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Scrapers.Services;

namespace MeshBench;

internal static class Program
{
    private const int DefaultPasses = 3;
    private const int DefaultBatchSize = 16;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static int Main(string[] args)
    {
        var options = BenchmarkOptions.Parse(args);
        var resourcesPath = ResolveResourcesPath(options.ResourcesPath);
        var terms = options.FixturePath == null
            ? BuildSyntheticTermSet()
            : LoadFixtureTerms(options.FixturePath);

        Console.WriteLine($"Resources: {resourcesPath}");
        Console.WriteLine($"Terms: {terms.Count} distinct values");
        Console.WriteLine($"Passes: {options.Passes}; batch sizes: {string.Join(", ", options.BatchSizes)}");

        var report = new BenchmarkReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            ResourcesPath = resourcesPath,
            FixturePath = options.FixturePath,
            TermCount = terms.Count,
            Passes = options.Passes,
            Results = [],
        };

        if (!options.QualityOnly)
        {
            foreach (int batchSize in options.BatchSizes)
            {
                for (int pass = 1; pass <= options.Passes; pass++)
                {
                    using var matcher = new MeSHMatcher(resourcesPath);
                    var cold = RunPass(matcher, terms, batchSize);
                    var warm = RunPass(matcher, terms, batchSize);

                    var result = new BenchmarkResult
                    {
                        BatchSize = batchSize,
                        Pass = pass,
                        Cold = cold,
                        Warm = warm,
                        CacheHitsAfterWarm = matcher.CacheHits,
                    };
                    report.Results.Add(result);

                    Console.WriteLine(
                        $"batch={batchSize,2} pass={pass}: " +
                        $"cold total={cold.TotalMs,9:F1}ms " +
                        $"p50={cold.P50Ms,7:F2}ms/term " +
                        $"p95={cold.P95Ms,7:F2}ms/term; " +
                        $"warm total={warm.TotalMs,9:F1}ms " +
                        $"p95={warm.P95Ms,7:F2}ms/term " +
                        $"cacheHits={matcher.CacheHits}");
                }
            }
        }

        if (options.ReferenceResourcesPath != null)
        {
            var quality = CompareQuality(resourcesPath, options.ReferenceResourcesPath, terms, options.BatchSizes[0]);
            report.Quality = quality;
            Console.WriteLine(
                $"quality: candidateAccepted={quality.CandidateAccepted}/{quality.TermCount}, " +
                $"referenceAccepted={quality.ReferenceAccepted}/{quality.TermCount}, " +
                $"acceptedDisagreements={quality.AcceptedDisagreements}, " +
                $"cuiDisagreements={quality.CuiDisagreements}, " +
                $"maxSimilarityDelta={quality.MaxSimilarityDelta:F6}");
        }

        if (options.LabelsPath != null)
        {
            var labels = EvaluateLabels(resourcesPath, options.LabelsPath);
            report.Labels = labels;
            Console.WriteLine(
                $"labels: precision={labels.Precision:F3}, recall={labels.Recall:F3}, " +
                $"f1={labels.F1:F3}, tp={labels.TruePositive}, fp={labels.FalsePositive}, " +
                $"fn={labels.FalseNegative}, tn={labels.TrueNegative}");
        }

        if (options.OutputPath != null)
        {
            var json = JsonSerializer.Serialize(report, JsonOptions);
            File.WriteAllText(options.OutputPath, json);
            Console.WriteLine($"Report: {options.OutputPath}");
        }

        return 0;
    }

    private static BenchmarkPass RunPass(MeSHMatcher matcher, List<string> terms, int batchSize)
    {
        var samples = new List<double>((terms.Count + batchSize - 1) / batchSize);
        var total = Stopwatch.StartNew();

        for (int offset = 0; offset < terms.Count; offset += batchSize)
        {
            int count = Math.Min(batchSize, terms.Count - offset);
            var chunk = new List<string>(count);
            for (int i = 0; i < count; i++)
                chunk.Add(terms[offset + i]);

            var sw = Stopwatch.StartNew();
            matcher.MatchBatch(chunk, "benchmark", "BENCH");
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds / count);
        }

        total.Stop();
        return new BenchmarkPass
        {
            TotalMs = total.Elapsed.TotalMilliseconds,
            P50Ms = BenchmarkStatistics.Percentile(samples, 50),
            P95Ms = BenchmarkStatistics.Percentile(samples, 95),
            SampleCount = samples.Count,
        };
    }

    private static QualityComparison CompareQuality(
        string candidateResourcesPath,
        string referenceResourcesPath,
        List<string> terms,
        int batchSize)
    {
        using var candidate = new MeSHMatcher(candidateResourcesPath);
        using var reference = new MeSHMatcher(referenceResourcesPath);
        int candidateAccepted = 0;
        int referenceAccepted = 0;
        int acceptedDisagreements = 0;
        int cuiDisagreements = 0;
        double maxSimilarityDelta = 0;

        for (int offset = 0; offset < terms.Count; offset += batchSize)
        {
            int count = Math.Min(batchSize, terms.Count - offset);
            var chunk = new List<string>(count);
            for (int i = 0; i < count; i++)
                chunk.Add(terms[offset + i]);

            var candidateResults = candidate.MatchBatch(chunk, "benchmark", "BENCH");
            var referenceResults = reference.MatchBatch(chunk, "benchmark", "BENCH");
            for (int i = 0; i < count; i++)
            {
                var candidateResult = candidateResults[i];
                var referenceResult = referenceResults[i];
                if (candidateResult.Accepted) candidateAccepted++;
                if (referenceResult.Accepted) referenceAccepted++;
                if (candidateResult.Accepted != referenceResult.Accepted)
                    acceptedDisagreements++;
                if (!string.Equals(candidateResult.MeshCui, referenceResult.MeshCui, StringComparison.Ordinal))
                    cuiDisagreements++;

                maxSimilarityDelta = Math.Max(
                    maxSimilarityDelta,
                    Math.Abs(candidateResult.Similarity - referenceResult.Similarity));
            }
        }

        return new QualityComparison
        {
            TermCount = terms.Count,
            CandidateAccepted = candidateAccepted,
            ReferenceAccepted = referenceAccepted,
            AcceptedDisagreements = acceptedDisagreements,
            CuiDisagreements = cuiDisagreements,
            MaxSimilarityDelta = maxSimilarityDelta,
        };
    }

    private static LabelMetrics EvaluateLabels(string resourcesPath, string labelsPath)
    {
        var rows = File.ReadLines(labelsPath)
            .Skip(1)
            .Select(ParseCsvLine)
            .Where(fields => fields.Length >= 2 && !string.IsNullOrWhiteSpace(fields[0]))
            .Select(fields => (Value: fields[0].Trim(), IsValid: fields[1].Trim() == "1"))
            .ToList();

        using var matcher = new MeSHMatcher(resourcesPath);
        var results = matcher.MatchBatch(rows.Select(row => row.Value), "keyword", "BENCH");
        int truePositive = 0;
        int falsePositive = 0;
        int falseNegative = 0;
        int trueNegative = 0;

        for (int i = 0; i < rows.Count; i++)
        {
            bool predicted = results[i].Accepted;
            if (predicted && rows[i].IsValid) truePositive++;
            else if (predicted) falsePositive++;
            else if (rows[i].IsValid) falseNegative++;
            else trueNegative++;
        }

        double precision = truePositive + falsePositive == 0
            ? 0
            : (double)truePositive / (truePositive + falsePositive);
        double recall = truePositive + falseNegative == 0
            ? 0
            : (double)truePositive / (truePositive + falseNegative);
        double f1 = precision + recall == 0
            ? 0
            : 2 * precision * recall / (precision + recall);

        return new LabelMetrics
        {
            SampleCount = rows.Count,
            Precision = precision,
            Recall = recall,
            F1 = f1,
            TruePositive = truePositive,
            FalsePositive = falsePositive,
            FalseNegative = falseNegative,
            TrueNegative = trueNegative,
        };
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char character = line[i];
            if (character == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        fields.Add(field.ToString());
        return fields.ToArray();
    }

    private static List<string> LoadFixtureTerms(string fixturePath)
    {
        var terms = new List<string>();
        using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));

        foreach (var study in document.RootElement.GetProperty("studies").EnumerateArray())
        {
            var protocol = study.GetProperty("protocolSection");
            if (protocol.TryGetProperty("conditionsModule", out var conditionsModule))
            {
                AddStrings(conditionsModule, "conditions", terms);
                AddStrings(conditionsModule, "keywords", terms);
            }

            if (protocol.TryGetProperty("armsInterventionsModule", out var interventionsModule) &&
                interventionsModule.TryGetProperty("interventions", out var interventions))
            {
                foreach (var intervention in interventions.EnumerateArray())
                {
                    if (intervention.TryGetProperty("name", out var name))
                        AddTerm(name.GetString(), terms);
                }
            }
        }

        return terms.Distinct(StringComparer.Ordinal).ToList();
    }

    private static void AddStrings(JsonElement parent, string propertyName, ICollection<string> terms)
    {
        if (!parent.TryGetProperty(propertyName, out var values))
            return;

        foreach (var value in values.EnumerateArray())
            AddTerm(value.GetString(), terms);
    }

    private static void AddTerm(string? value, ICollection<string> terms)
    {
        if (!string.IsNullOrWhiteSpace(value))
            terms.Add(value.Trim());
    }

    private static List<string> BuildSyntheticTermSet()
    {
        var bases = new[]
        {
            "diabetes", "hypertension", "asthma", "cancer", "breast cancer", "lung cancer",
            "heart failure", "coronary artery disease", "stroke", "depression", "anxiety",
            "rheumatoid arthritis", "osteoarthritis", "migraine", "epilepsy", "alzheimers disease",
            "parkinsons disease", "multiple sclerosis", "crohns disease", "ulcerative colitis",
            "psoriasis", "eczema", "copd", "pneumonia", "tuberculosis", "hepatitis",
            "hiv", "aids", "malaria", "dengue", "sepsis", "pulmonary fibrosis",
            "renal failure", "kidney disease", "liver cirrhosis", "fatty liver", "obesity",
            "anemia", "leukemia", "lymphoma", "melanoma", "thyroid cancer", "prostate cancer",
            "colorectal cancer", "ovarian cancer", "pancreatic cancer", "gastric cancer",
            "bladder cancer", "kidney cancer", "sarcoma", "glioma", "glioblastoma",
        };
        var qualifiers = new[] { "advanced", "metastatic", "refractory", "severe", "moderate", "early-stage" };

        return bases
            .SelectMany(value => new[] { value }.Concat(qualifiers.Select(qualifier => $"{value} {qualifier}")))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string ResolveResourcesPath(string? configuredPath)
    {
        if (configuredPath != null)
            return Path.GetFullPath(configuredPath);

        var outputPath = Path.Combine(AppContext.BaseDirectory, "Resources", "mesh");
        if (File.Exists(Path.Combine(outputPath, "model.onnx")))
            return outputPath;

        return Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
            "Scrapers", "Resources", "mesh"));
    }

    private sealed class BenchmarkOptions
    {
        public string? ResourcesPath { get; private init; }
        public string? FixturePath { get; private init; }
        public string? ReferenceResourcesPath { get; private init; }
        public string? LabelsPath { get; private init; }
        public string? OutputPath { get; private init; }
        public bool QualityOnly { get; private init; }
        public int Passes { get; private init; } = DefaultPasses;
        public List<int> BatchSizes { get; private init; } = [DefaultBatchSize];

        public static BenchmarkOptions Parse(string[] args)
        {
            string? resourcesPath = null;
            string? fixturePath = null;
            string? referenceResourcesPath = null;
            string? labelsPath = null;
            string? outputPath = null;
            bool qualityOnly = false;
            int passes = DefaultPasses;
            var batchSizes = new List<int> { DefaultBatchSize };

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--resources":
                        resourcesPath = RequireValue(args, ref i);
                        break;
                    case "--fixture":
                        fixturePath = Path.GetFullPath(RequireValue(args, ref i));
                        break;
                    case "--reference-resources":
                        referenceResourcesPath = Path.GetFullPath(RequireValue(args, ref i));
                        break;
                    case "--labels":
                        labelsPath = Path.GetFullPath(RequireValue(args, ref i));
                        break;
                    case "--synthetic":
                        fixturePath = null;
                        break;
                    case "--output":
                        outputPath = Path.GetFullPath(RequireValue(args, ref i));
                        break;
                    case "--quality-only":
                        qualityOnly = true;
                        break;
                    case "--passes":
                        passes = ParsePositiveInt(RequireValue(args, ref i), "passes");
                        break;
                    case "--batch-sizes":
                        batchSizes = RequireValue(args, ref i)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(value => ParsePositiveInt(value, "batch size"))
                            .Distinct()
                            .ToList();
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{args[i]}'");
                }
            }

            if (batchSizes.Any(size => size > 16))
                throw new ArgumentException("Batch sizes must be between 1 and 16");

            return new BenchmarkOptions
            {
                ResourcesPath = resourcesPath,
                FixturePath = fixturePath,
                ReferenceResourcesPath = referenceResourcesPath,
                LabelsPath = labelsPath,
                OutputPath = outputPath,
                QualityOnly = qualityOnly,
                Passes = passes,
                BatchSizes = batchSizes,
            };
        }

        private static string RequireValue(string[] args, ref int index)
        {
            if (++index >= args.Length)
                throw new ArgumentException($"Missing value for '{args[index - 1]}'");
            return args[index];
        }

        private static int ParsePositiveInt(string value, string name)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result) || result <= 0)
                throw new ArgumentException($"{name} must be a positive integer");
            return result;
        }
    }

    private sealed class BenchmarkReport
    {
        public DateTimeOffset GeneratedAtUtc { get; init; }
        public string ResourcesPath { get; init; } = "";
        public string? FixturePath { get; init; }
        public int TermCount { get; init; }
        public int Passes { get; init; }
        public List<BenchmarkResult> Results { get; init; } = [];
        public QualityComparison? Quality { get; set; }
        public LabelMetrics? Labels { get; set; }
    }

    private sealed class BenchmarkResult
    {
        public int BatchSize { get; init; }
        public int Pass { get; init; }
        public BenchmarkPass Cold { get; init; } = new();
        public BenchmarkPass Warm { get; init; } = new();
        public int CacheHitsAfterWarm { get; init; }
    }

    private sealed class BenchmarkPass
    {
        public double TotalMs { get; init; }
        public double P50Ms { get; init; }
        public double P95Ms { get; init; }
        public int SampleCount { get; init; }
    }

    private sealed class QualityComparison
    {
        public int TermCount { get; init; }
        public int CandidateAccepted { get; init; }
        public int ReferenceAccepted { get; init; }
        public int AcceptedDisagreements { get; init; }
        public int CuiDisagreements { get; init; }
        public double MaxSimilarityDelta { get; init; }
    }

    private sealed class LabelMetrics
    {
        public int SampleCount { get; init; }
        public double Precision { get; init; }
        public double Recall { get; init; }
        public double F1 { get; init; }
        public int TruePositive { get; init; }
        public int FalsePositive { get; init; }
        public int FalseNegative { get; init; }
        public int TrueNegative { get; init; }
    }
}
