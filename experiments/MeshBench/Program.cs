using System.Diagnostics;
using Scrapers.Services;

var resourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources", "mesh");
if (!File.Exists(Path.Combine(resourcesPath, "model.onnx")))
{
    resourcesPath = Path.GetFullPath(Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
        "Scrapers", "Resources", "mesh"));
}

Console.WriteLine($"Loading MeSH matcher from {resourcesPath} ...");
using var matcher = new MeSHMatcher(resourcesPath);

var terms = BuildTermSet();
Console.WriteLine($"\nBenchmarking {terms.Count} distinct non-exact terms x2 passes (cold = inference, warm = memo-cache)");

var cold = RunPass(matcher, terms);
Console.WriteLine($"\nCold pass (no cache):  {cold.TotalMs,6:F1} ms total  {cold.MsPerTerm:F2} ms/term  ({terms.Count} inferences)");

var warm = RunPass(matcher, terms);
Console.WriteLine($"Warm pass (cached):   {warm.TotalMs,6:F1} ms total  {warm.MsPerTerm:F2} ms/term  ({matcher.CacheHits} cache hits)");

var speedup = cold.MsPerTerm / Math.Max(warm.MsPerTerm, 0.001);
Console.WriteLine($"\nSpeedup on repeated terms: {speedup:F1}x");

return 0;

static List<string> BuildTermSet()
{
    var bases = new List<string>
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

    var qualifiers = new List<string>
    {
        "advanced", "metastatic", "refractory", "severe", "moderate", "early-stage",
        "recurrent", "uncontrolled", "chronic", "acute", "progressive", "stable",
        "type 2", "type 1", "stage iv", "high-risk", "treatment-naive", "with comorbidity",
        "in adults", "in children", "after surgery", "receiving chemotherapy",
    };

    var terms = new List<string>();
    foreach (var b in bases)
    {
        terms.Add(b);
        foreach (var q in qualifiers.Take(6))
        {
            terms.Add($"{b} {q}");
        }
    }

    return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

static (double TotalMs, double MsPerTerm) RunPass(MeSHMatcher matcher, List<string> terms)
{
    var sw = Stopwatch.StartNew();
    foreach (var term in terms)
    {
        matcher.Match(term, "condition", "BENCH");
    }

    sw.Stop();
    return (sw.Elapsed.TotalMilliseconds, sw.Elapsed.TotalMilliseconds / terms.Count);
}
