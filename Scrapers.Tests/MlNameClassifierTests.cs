using System.Globalization;
using System.Security.Cryptography;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class MlNameClassifierTests
{
    private sealed class NameData
    {
        public string Name { get; set; } = string.Empty;
        public bool Label { get; set; }
        public string? Role { get; set; }
    }

    private sealed class NamePrediction
    {
        public bool PredictedLabel { get; set; }
        public float Score { get; set; }
        public float Probability { get; set; }
    }

    private static readonly HashSet<string> OldOrgKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "UNIVERSITY", "COLLEGE", "INSTITUTE", "HOSPITAL", "CLINIC",
        "LABORATORY", "LABORATORIES", "LAB", "FOUNDATION",
        "DEPARTMENT", "COMMITTEE", "ASSOCIATION", "CORPORATION",
        "COMPANY", "PHARMA", "PHARMACEUTICAL", "BIOTECH",
        "BIOSCIENCE", "BIOSCIENCES", "THERAPEUTICS",
        "MEDICAL CENTER", "CANCER CENTER", "HEALTH SYSTEM",
        "HEALTHCARE", "NATIONAL INSTITUTE", "SCHOOL OF",
        "COLLEGE OF", "OFFICE OF", "CENTER FOR", "CENTRE FOR",
        "INSTITUTE OF", "DIVISION OF", "DEPARTMENT OF",
        "BOARD OF", "MINISTRY OF", "FUND FOR",
        "RESEARCH INSTITUTE", "RESEARCH CENTER", "RESEARCH CENTRE",
        "CLINICAL RESEARCH", "CLINICAL TRIAL", "CLINICAL TRIALS",
        "LIMITED LIABILITY", "SOCIETE", "GESELLSCHAFT", "GMBH",
        "AKTIENGESELLSCHAFT", "AG", "NV", "PTY", "PTY LTD",
        "AND ASSOCIATES", "AND COMPANY", "& CO", "& ASSOCIATES",
        "DIRECTOR", "MEDICAL", "STUDY", "CLINICAL",
        "REGISTRY", "MONITOR", "COORDINATOR",
        "MANAGEMENT", "RESPONSIBLE", "CALL CENTER",
        "CENTER", "CORPORATE", "CARE", "TBD",
        "SPONSOR",
    };

    private static readonly HashSet<string> PharmaBlocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "PFIZER", "ROCHE", "NOVARTIS", "ASTRAZENECA", "MERCK",
        "SANOFI", "BAYER", "TAKEDA", "AMGEN", "GILEAD",
        "BIOGEN", "ABBVIE", "REGENERON", "MODERNA", "BIONTECH",
        "CELGENE", "MYLAN", "TEVA", "SANDOZ",
        "MEDTRONIC", "STRYKER", "BAUSCH",
        "VIATRIS", "BOEHRINGER",
        "GSK", "CHUGAI", "UCB",
        "BRISTOL-MYERS", "BRISTOL",
        "MEDIMMUNE",
    };

    private static readonly HashSet<string> KnownNonHumanNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "USE CENTRAL CONTACT",
        "BRISTOL-MYERS SQUIBB",
        "BRISTOL MYERS SQUIBB",
    };

    private static readonly HashSet<string> RolePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CONTACT FOR", "CONTACT", "STUDY DIRECTOR",
        "SCIENTIFIC CONTACT", "PUBLIC QUERIES", "PUBLIC CONTACT",
        "SPONSOR",
    };

    private static readonly HashSet<string> KnownPiRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "PRINCIPAL_INVESTIGATOR", "SUB_INVESTIGATOR", "STUDY_DIRECTOR",
        "STUDY_CHAIR", "STUDY_CO_CHAIR", "STUDY_COORDINATOR",
        "INVESTIGATOR", "CO_INVESTIGATOR",
    };

    [TestMethod]
    [TestCategory("LocalOnly")]
    public void ExtractAndCompare()
    {
        var outputDir = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Data"));
        Directory.CreateDirectory(outputDir);

        var samples = LoadFromDatabase();
        var accepted = samples.Where(s => s.Label).Select(s => s.Name).Distinct().ToList();
        var rejected = samples.Where(s => !s.Label).Select(s => s.Name).Distinct().ToList();

        Console.WriteLine($"Loaded {samples.Count} total rows, {accepted.Count} unique accepted, {rejected.Count} unique rejected");

        if (accepted.Count == 0 || rejected.Count == 0)
        {
            Console.WriteLine("Skipping: need both positive and negative samples to train");
            return;
        }

        DumpTrainingData(outputDir, accepted, rejected);

        var balanced = BalanceForTraining(accepted, rejected, 5000);

        var allNamesPath = Path.Combine(outputDir, "training_data_all.txt");
        File.WriteAllLines(allNamesPath, balanced.Select(s => $"{s.Name}\t{(s.Label ? "ACCEPTED" : "REJECTED")}"));

        var comparisonPath = Path.Combine(outputDir, "comparison_side_by_side.txt");
        RunComparison(balanced, comparisonPath, outputDir);

        Console.WriteLine($"\nFiles written:");
        Console.WriteLine($"  {allNamesPath} — {balanced.Count} training samples");
        Console.WriteLine($"  {comparisonPath} — side-by-side comparison");
    }

    private static bool IsDefinitelyNonHuman(string name)
    {
        var trimmed = name.Trim();
        var upper = trimmed.ToUpperInvariant();

        if (upper.StartsWith('+') && trimmed.Any(c => c is >= '0' and <= '9'))
            return true;

        var firstWord = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        if (firstWord.All(c => c is >= '0' and <= '9'))
            return true;

        if (KnownNonHumanNames.Contains(upper))
            return true;

        if (RolePrefixes.Any(p => upper.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (PharmaBlocklist.Contains(firstWord.TrimEnd(',', '.')))
            return true;

        var lastWord = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[^1].TrimEnd(',', '.').ToUpperInvariant();
        if (lastWord is "INC" or "INC." or "LTD" or "LTD." or "LLC" or "CORP"
            or "CORP." or "CORPORATION" or "GMBH" or "AG" or "NV" or "PLC"
            or "SA" or "SARL" or "PTY" or "LIMITED" or "COMPANY" or "CO")
            return true;

        if (trimmed.Contains(" & ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed.Split(" & ", StringSplitOptions.TrimEntries);
            var suffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MD", "M.D", "PHD", "PH.D", "MBA", "DO", "DDS", "FRCPC", "FAAN", "L.AC", "LAC" };
            if (parts.Length > 1 && parts.All(p => suffixes.Contains(p.TrimEnd(',').TrimEnd('.').Trim())))
                return false;
            return true;
        }

        var nonHumanPatterns = new[]
        {
            @"STUDY DIRECTOR", @"MEDICAL DIRECTOR", @"MEDICAL MONITOR",
            @"CLINICAL TRIAL", @"CALL CENTER", @"CONTACT FOR",
            @"PUBLIC QUERIES", @"USE CENTRAL CONTACT",
            @"DIRECTOR OF ", @"HEAD OF ", @"VICE PRESIDENT",
            @"DEPARTMENT OF ", @"DIVISION OF ", @"OFFICE OF ",
            @"BOARD OF ", @"MINISTRY OF ", @"FUND FOR ",
            @"MANAGER\b", @"LEAD CSM\b", @"LEAD CRA\b",
            @"COORDINATOR\b", @"ADMINISTRATOR\b", @"SPECIALIST\b",
            @"ANALYST\b", @"OFFICER\b", @"SUPERVISOR\b",
            @"GMBH\b", @"AKTIENGESELLSCHAFT\b",
        };

        foreach (var pattern in nonHumanPatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(upper, pattern))
                return true;
        }

        return false;
    }

    private static List<NameData> LoadFromDatabase()
    {
        var result = new List<NameData>();
        try
        {
            var cs = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
                     ?? $"Host=localhost;Port=5432;Database=clinical_trial_data;Username={Environment.UserName}";
            var optionsBuilder = new DbContextOptionsBuilder<ClinicalTrialsContext>();
            optionsBuilder.UseNpgsql(cs);
            using var ctx = new ClinicalTrialsContext(optionsBuilder.Options);

            var investigatorNames = ctx.InvestigatorPersons
                .Where(p => p.FullName.Length >= 3 && p.FullName.Length <= 100)
                .Select(p => p.FullName)
                .Distinct()
                .AsEnumerable()
                .ToList();

            var rejectedNames = ctx.RejectedEntities
                .Where(r => r.EntityType == "investigator_name" && r.Value.Length >= 3 && r.Value.Length <= 100)
                .Select(r => r.Value)
                .Distinct()
                .AsEnumerable()
                .ToList();

            Console.WriteLine($"DB: {investigatorNames.Count} investigator_persons, {rejectedNames.Count} rejected_entities unique names");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var acceptedNames = new List<string>();
            foreach (var name in investigatorNames)
            {
                if (!seen.Add(name)) continue;
                if (!IsDefinitelyNonHuman(name))
                    acceptedNames.Add(name);
                else
                    Console.WriteLine($"  INVESTIGATOR_PERSON false positive: {name}");
            }

            var rejectedClean = new List<string>();
            foreach (var name in rejectedNames)
            {
                if (!seen.Add(name)) continue;
                if (IsDefinitelyNonHuman(name))
                    rejectedClean.Add(name);
                else
                    acceptedNames.Add(name);
            }

            Console.WriteLine($"After cleaning: {acceptedNames.Count} accepted, {rejectedClean.Count} rejected");

            foreach (var name in acceptedNames)
                result.Add(new NameData { Name = name, Label = true });
            foreach (var name in rejectedClean)
                result.Add(new NameData { Name = name, Label = false });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DB error: {ex.Message}");
        }
        return result;
    }

    private static void DumpTrainingData(string outputDir, List<string> accepted, List<string> rejected)
    {
        var posPath = Path.Combine(outputDir, "training_data_positives.txt");
        File.WriteAllLines(posPath, accepted.OrderBy(n => n).Select(n =>
            $"{n}\tACCEPTED"));

        var negPath = Path.Combine(outputDir, "training_data_negatives.txt");
        File.WriteAllLines(negPath, rejected.OrderBy(n => n).Select(n =>
            $"{n}\tREJECTED"));

        Console.WriteLine($"Positives written: {accepted.Count} to {posPath}");
        Console.WriteLine($"Negatives written: {rejected.Count} to {negPath}");
    }

    private static List<NameData> BalanceForTraining(List<string> accepted, List<string> rejected, int targetTotal)
    {
        if (accepted.Count == 0 || rejected.Count == 0)
            return new List<NameData>();

        var targetPerClass = targetTotal / 2;

        var acceptedBalanced = accepted.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).Take(targetPerClass).ToList();
        if (acceptedBalanced.Count < targetPerClass)
        {
            while (acceptedBalanced.Count < targetPerClass)
                acceptedBalanced.Add(accepted[RandomNumberGenerator.GetInt32(accepted.Count)]);
        }

        var rejectedBalanced = new List<string>();
        if (rejected.Count >= targetPerClass)
        {
            rejectedBalanced = rejected.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).Take(targetPerClass).ToList();
        }
        else
        {
            while (rejectedBalanced.Count < targetPerClass)
                foreach (var r in rejected.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)))
                {
                    rejectedBalanced.Add(r);
                    if (rejectedBalanced.Count >= targetPerClass) break;
                }
        }

        var result = new List<NameData>();
        result.AddRange(acceptedBalanced.Select(n => new NameData { Name = n, Label = true }));
        result.AddRange(rejectedBalanced.Select(n => new NameData { Name = n, Label = false }));
        return result.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();
    }

    private static void RunComparison(List<NameData> allSamples, string outputPath, string outputDir)
    {
        var mlContext = new MLContext(seed: 42);
        var data = mlContext.Data.LoadFromEnumerable(allSamples);
        var trainTest = mlContext.Data.TrainTestSplit(data, testFraction: 0.5, seed: 42);

        var pipeline = mlContext.Transforms.Text.FeaturizeText("Features", nameof(NameData.Name))
            .Append(mlContext.BinaryClassification.Trainers
                .SdcaLogisticRegression("Label", "Features"));

        Console.WriteLine("\n=== TRAINING ML MODEL ===");
        var model = pipeline.Fit(trainTest.TrainSet);
        var predictions = model.Transform(trainTest.TestSet);
        var mlMetrics = mlContext.BinaryClassification.Evaluate(predictions, "Label");

        var testSamples = mlContext.Data.CreateEnumerable<NameData>(trainTest.TestSet, reuseRowObject: false).ToList();
        var testPredictions = mlContext.Data.CreateEnumerable<NamePrediction>(predictions, reuseRowObject: false).ToList();

        var trainSamples = mlContext.Data.CreateEnumerable<NameData>(trainTest.TrainSet, reuseRowObject: false).ToList();

        var trainPath = Path.Combine(outputDir, "training_set_5000.txt");
        using (var writer = new StreamWriter(trainPath))
        {
            writer.WriteLine("Name\tLabel");
            foreach (var s in trainSamples)
                writer.WriteLine($"{s.Name}\t{(s.Label ? "HUMAN" : "NON_HUMAN")}");
        }

        var header = $"{"Name",-60} {"GT",-6} {"OldRules",-10} {"NewRules",-10} {"ML",-10} {"ML_Prob",-10} {"Notes"}";

        int gtOldCorr = 0, gtNewCorr = 0, gtMlCorr = 0;
        int oldFp = 0, oldFn = 0, newFp = 0, newFn = 0, mlFp = 0, mlFn = 0;

        using (var writer = new StreamWriter(outputPath))
        {
            writer.WriteLine("=== 3-WAY COMPARISON: Old Rules vs New Rules vs ML.NET ===");
            writer.WriteLine($"Test set: {testSamples.Count} samples ({testSamples.Count(s => s.Label)} human, {testSamples.Count(s => !s.Label)} non-human)");
            writer.WriteLine($"Ground truth cleaned via IsDefinitelyNonHuman + investigator_persons source");
            writer.WriteLine(new string('=', 120));
            writer.WriteLine(header);
            writer.WriteLine(new string('-', 120));

            for (int i = 0; i < testSamples.Count; i++)
            {
                var s = testSamples[i];
                var mlP = testPredictions[i];
                var oldResult = OldRules(s.Name, null);
                var newResult = NameFilter.IsHumanName(s.Name, null);

                var oldStr = oldResult.IsHuman ? "ACCEPT   " : "REJECT   ";
                var newStr = newResult.IsHuman ? "ACCEPT   " : "REJECT   ";
                var mlStr = mlP.PredictedLabel ? "ACCEPT   " : "REJECT   ";
                var mlProbStr = mlP.PredictedLabel
                    ? mlP.Probability.ToString("P2", CultureInfo.InvariantCulture)
                    : (1 - mlP.Probability).ToString("P2", CultureInfo.InvariantCulture);

                if (oldResult.IsHuman == s.Label) gtOldCorr++; else { if (oldResult.IsHuman) oldFp++; else oldFn++; }
                if (newResult.IsHuman == s.Label) gtNewCorr++; else { if (newResult.IsHuman) newFp++; else newFn++; }
                if (mlP.PredictedLabel == s.Label) gtMlCorr++; else { if (mlP.PredictedLabel) mlFp++; else mlFn++; }

                var gtStr = s.Label ? "HUMAN " : "NON-HM";
                var disagreements = new List<string>();
                if (oldResult.IsHuman != s.Label) disagreements.Add("OLD_WRONG");
                if (newResult.IsHuman != s.Label) disagreements.Add("NEW_WRONG");
                if (mlP.PredictedLabel != s.Label) disagreements.Add("ML_WRONG");
                var notes = disagreements.Count > 0 ? string.Join(",", disagreements) : "ALL_OK";

                writer.WriteLine($"{s.Name,-60} {gtStr,-6} {oldStr,-10} {newStr,-10} {mlStr,-10} {mlProbStr,-10} {notes}");
            }

            var oldPrec = gtOldCorr > 0 ? (double)(testSamples.Count(s => s.Label) - oldFn) / (testSamples.Count(s => s.Label) - oldFn + oldFp) : 0;
            var newPrec = gtNewCorr > 0 ? (double)(testSamples.Count(s => s.Label) - newFn) / (testSamples.Count(s => s.Label) - newFn + newFp) : 0;
            var oldRec = (double)(testSamples.Count(s => s.Label) - oldFn) / (testSamples.Count(s => s.Label));
            var newRec = (double)(testSamples.Count(s => s.Label) - newFn) / (testSamples.Count(s => s.Label));
            var oldF1 = oldPrec + oldRec > 0 ? 2 * oldPrec * oldRec / (oldPrec + oldRec) : 0;
            var newF1 = newPrec + newRec > 0 ? 2 * newPrec * newRec / (newPrec + newRec) : 0;

            writer.WriteLine(new string('-', 120));
            writer.WriteLine();
            writer.WriteLine("=== SUMMARY ===");
            writer.WriteLine($"{"",-60} {"OldRules",-15} {"NewRules",-15} {"ML.NET",-15}");
            writer.WriteLine($"{"Accuracy",-60} {((double)gtOldCorr / testSamples.Count),-15:P2} {((double)gtNewCorr / testSamples.Count),-15:P2} {mlMetrics.Accuracy,-15:P2}");
            writer.WriteLine($"{"Precision",-60} {oldPrec,-15:P2} {newPrec,-15:P2} {mlMetrics.PositivePrecision,-15:P2}");
            writer.WriteLine($"{"Recall",-60} {oldRec,-15:P2} {newRec,-15:P2} {mlMetrics.PositiveRecall,-15:P2}");
            writer.WriteLine($"{"F1",-60} {oldF1,-15:P2} {newF1,-15:P2} {mlMetrics.F1Score,-15:P2}");
            writer.WriteLine($"{"False Positives",-60} {oldFp,-15} {newFp,-15} {mlFp,-15}");
            writer.WriteLine($"{"False Negatives",-60} {oldFn,-15} {newFn,-15} {mlFn,-15}");
            writer.WriteLine();
            writer.WriteLine("=== DISAGREEMENT ANALYSIS ===");
            writer.WriteLine($"  All three agree:     {testSamples.Count(s => { var o = OldRules(s.Name, null); var n = NameFilter.IsHumanName(s.Name, null); return o.IsHuman == s.Label && n.IsHuman == s.Label; })}");

            Console.WriteLine("\n=== CONSOLE SUMMARY ===");
            Console.WriteLine($"Test samples: {testSamples.Count} ({testSamples.Count(s => s.Label)} human, {testSamples.Count(s => !s.Label)} non-human)");
            Console.WriteLine();
            Console.WriteLine($"{"",-50} {"OldRules",-15} {"NewRules",-15} {"ML.NET",-15}");
            Console.WriteLine($"{"Accuracy",-50} {((double)gtOldCorr / testSamples.Count),-15:P2} {((double)gtNewCorr / testSamples.Count),-15:P2} {mlMetrics.Accuracy,-15:P2}");
            Console.WriteLine($"{"Precision",-50} {oldPrec,-15:P2} {newPrec,-15:P2} {mlMetrics.PositivePrecision,-15:P2}");
            Console.WriteLine($"{"Recall",-50} {oldRec,-15:P2} {newRec,-15:P2} {mlMetrics.PositiveRecall,-15:P2}");
            Console.WriteLine($"{"F1",-50} {oldF1,-15:P2} {newF1,-15:P2} {mlMetrics.F1Score,-15:P2}");
            Console.WriteLine($"{"FP/FN",-50} {oldFp}/{oldFn,-13} {newFp}/{newFn,-13} {mlFp}/{mlFn,-13}");
        }

        var modelPath = Path.Combine(outputDir, "name-classifier.zip");
        mlContext.Model.Save(model, trainTest.TrainSet.Schema, modelPath);
        Console.WriteLine($"\nModel saved: {modelPath}");
        Console.WriteLine($"Comparison saved: {outputPath}");
    }

    private static NameFilterResult OldRules(string name, string? role)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new NameFilterResult(false, "EmptyOrNull");

        var trimmed = name.Trim();
        var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var upperName = trimmed.ToUpperInvariant();

        if (HasNonLatin(words))
            return trimmed.Length >= 2 && words.Length <= 6
                ? new NameFilterResult(true, null)
                : new NameFilterResult(false, "NonLatinName");

        if (trimmed.Length < 3) return new NameFilterResult(false, "TooShort");
        if (trimmed.Length > 100) return new NameFilterResult(false, "TooLong");
        if (words.Length > 6) return new NameFilterResult(false, "TooManyWords");

        if (KnownNonHumanNames.Contains(upperName))
            return new NameFilterResult(false, $"KnownNonHumanName:{upperName}");

        var matchedPrefix = RolePrefixes.FirstOrDefault(p => upperName.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        if (matchedPrefix != null) return new NameFilterResult(false, $"RolePrefix:{matchedPrefix}");

        var lastWord = words[^1].TrimEnd(',', '.').ToUpperInvariant();
        if (lastWord is "INC" or "INC." or "LTD" or "LTD." or "LLC" or "CORP"
            or "CORP." or "CORPORATION" or "GMBH" or "AG" or "NV" or "PLC"
            or "SA" or "SARL" or "PTY" or "LIMITED" or "COMPANY" or "CO")
            return new NameFilterResult(false, $"CorporateSuffix:{lastWord}");

        if (trimmed.Contains(" & ", StringComparison.OrdinalIgnoreCase))
            return new NameFilterResult(false, "Ampersand");

        if (PharmaBlocklist.Contains(words[0].TrimEnd(',', '.')))
            return new NameFilterResult(false, $"PharmaBlocklist:{words[0].TrimEnd(',', '.')}");

        var matchedOrgKw = OldOrgKeywords.FirstOrDefault(kw => upperName.Contains(kw, StringComparison.OrdinalIgnoreCase));
        if (matchedOrgKw != null)
            return new NameFilterResult(false, $"OrgKeywords:{matchedOrgKw}");

        if (role != null && KnownPiRoles.Contains(role.Trim()))
            return new NameFilterResult(true, null);

        if (words.Length == 1 && trimmed.Length > 20)
            return new NameFilterResult(false, "SingleLongWord");

        return new NameFilterResult(true, null);
    }

    private static bool HasNonLatin(string[] words)
    {
        foreach (var w in words)
            foreach (var c in w)
                if (char.IsLetter(c) && !((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
                    return true;
        return false;
    }


}
