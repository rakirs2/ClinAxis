#nullable disable
using System.Text;
using System.Text.Json;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Services;

var cs = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? $"Host=localhost;Port=5432;Database=clinical_trial_data;Username={Environment.UserName}";

var options = new DbContextOptionsBuilder<ClinicalTrialsContext>()
    .UseNpgsql(cs)
    .Options;

var sampleSize = 100;
var outputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../data_problems"));
Directory.CreateDirectory(outputDir);
var outputFile1 = Path.Combine(outputDir, "01_mesh_vs_raw_conditions.tsv");
var outputFile2 = Path.Combine(outputDir, "02_mesh_filtered_disease_icd10.tsv");

using var http = new HttpClient();

await using (var context = new ClinicalTrialsContext(options))
{
    var studies = await context.Studies
        .Where(s => s.Conditions.Any() && s.References.Any(r => r.Pmid != null && r.Pmid.Length > 0))
        .OrderBy(s => EF.Functions.Random())
        .Take(sampleSize)
        .Select(s => new
        {
            s.NctId,
            Conditions = s.Conditions.Select(c => c.Condition).ToList(),
            FirstPmid = s.References
                .Where(r => r.Pmid != null && r.Pmid.Length > 0)
                .Select(r => r.Pmid)
                .FirstOrDefault()
        })
        .ToListAsync()
        .ConfigureAwait(false);

    var pmids = studies.Select(s => s.FirstPmid).Where(p => p != null).Distinct().ToList();
    Console.WriteLine("Studies: " + studies.Count + ", Unique PMIDs: " + pmids.Count);

    var pmidMesh = new Dictionary<string, List<(string Name, string Qual, string UI)>>(StringComparer.OrdinalIgnoreCase);
    var allUIs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    for (int i = 0; i < pmids.Count; i++)
    {
        Console.Write("\rFetching PMID " + (i + 1) + "/" + pmids.Count + "...");
        try
        {
            var detail = await PubMedScraperService.FetchPaperDetailAsync(pmids[i], CancellationToken.None).ConfigureAwait(false);
            if (detail?.MeshHeadings != null)
            {
                var list = new List<(string, string, string)>();
                foreach (var mh in detail.MeshHeadings)
                {
                    list.Add((mh.DescriptorName, mh.QualifierName ?? "", mh.DescriptorUI ?? ""));
                    if (!string.IsNullOrEmpty(mh.DescriptorUI))
                        allUIs.Add(mh.DescriptorUI);
                }
                pmidMesh[pmids[i]] = list;
            }
        }
        catch { }
        await Task.Delay(350).ConfigureAwait(false);
    }

    Console.WriteLine("\nUnique DescriptorUIs: " + allUIs.Count);

    var uiTrees = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    var uiList = allUIs.ToList();

    for (int i = 0; i < uiList.Count; i++)
    {
        Console.Write("\rClassifying " + (i + 1) + "/" + uiList.Count + "...");
        try
        {
            var uri = new Uri("https://id.nlm.nih.gov/mesh/" + uiList[i] + ".json");
            var resp = await http.GetAsync(uri).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var doc = JsonDocument.Parse(json);
                var trees = new List<string>();
                if (doc.RootElement.TryGetProperty("treeNumber", out var tp))
                {
                    foreach (var t in tp.EnumerateArray())
                    {
                        var path = t.GetString() ?? "";
                        var parts = path.Split('/');
                        trees.Add(parts.Length > 0 ? parts[^1] : path);
                    }
                }
                uiTrees[uiList[i]] = trees;
            }
        }
        catch { }
        await Task.Delay(350).ConfigureAwait(false);
    }

    Console.WriteLine();

    bool IsDisease(string ui)
    {
        if (!uiTrees.TryGetValue(ui, out var trees)) return false;
        return trees.Any(t => t.StartsWith('C') || t.StartsWith("F03", StringComparison.Ordinal));
    }

    var icdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Breast Neoplasms"] = "C50", ["Lung Neoplasms"] = "C34",
        ["Colorectal Neoplasms"] = "C18-C20", ["Ovarian Neoplasms"] = "C56",
        ["Liver Neoplasms"] = "C22", ["Stomach Neoplasms"] = "C16",
        ["Thyroid Neoplasms"] = "C73", ["Skin Neoplasms"] = "C43-C44",
        ["Melanoma"] = "C43", ["Leukemia, Myeloid, Acute"] = "C92.0",
        ["Leukemia"] = "C91-C95", ["Lymphoma"] = "C81-C86",
        ["Prostatic Neoplasms"] = "C61", ["Pancreatic Neoplasms"] = "C25",
        ["Kidney Neoplasms"] = "C64", ["Bladder Neoplasms"] = "C67",
        ["Brain Neoplasms"] = "C71", ["Neoplasm Metastasis"] = "C77-C80",
        ["Neoplasm Recurrence, Local"] = "Z85",
        ["Atrial Fibrillation"] = "I48", ["Coronary Artery Disease"] = "I25.1",
        ["Cardiomyopathy, Hypertrophic"] = "I42.2", ["Heart Failure"] = "I50",
        ["Hypertension"] = "I10", ["Hypertension, Renal"] = "I12",
        ["Stroke"] = "I63", ["Brain Ischemia"] = "I63",
        ["Pulmonary Disease, Chronic Obstructive"] = "J44", ["Asthma"] = "J45",
        ["Respiratory Insufficiency"] = "J96", ["Pneumonia"] = "J12-J18",
        ["Diabetes Mellitus, Type 1"] = "E10", ["Diabetes Mellitus, Type 2"] = "E11",
        ["Metabolic Syndrome"] = "E88.81", ["Obesity"] = "E66",
        ["Hyperphosphatemia"] = "E83.3", ["Hypogonadism"] = "E29.1",
        ["Acute Kidney Injury"] = "N17", ["Kidney Failure, Chronic"] = "N18",
        ["End Stage Renal Disease"] = "N18.6",
        ["Benign Prostatic Hyperplasia"] = "N40", ["Urinary Tract Infections"] = "N39.0",
        ["Arthritis, Rheumatoid"] = "M05-M06", ["Osteoarthritis, Knee"] = "M17",
        ["Osteoporosis"] = "M80-M81", ["Gout"] = "M10",
        ["Low Back Pain"] = "M54.5",
        ["Systemic Lupus Erythematosus"] = "M32",
        ["Anorexia Nervosa"] = "F50.0", ["Schizophrenia"] = "F20",
        ["Bipolar Disorder"] = "F31", ["Depression"] = "F32",
        ["Major Depressive Disorder"] = "F32", ["Borderline Personality Disorder"] = "F60.3",
        ["Parkinson Disease"] = "G20",
        ["Multiple Sclerosis"] = "G35", ["Epilepsy"] = "G40",
        ["Migraine Disorders"] = "G43",
        ["HIV Infections"] = "B20",
        ["Hepatitis B, Chronic"] = "B18.1", ["Hepatitis C, Chronic"] = "B18.2",
        ["Sepsis"] = "A41", ["Septic Shock"] = "R57.2",
        ["Cystic Fibrosis"] = "E84", ["Hepatic Encephalopathy"] = "K72",
        ["Gastroesophageal Reflux"] = "K21",
        ["Inflammatory Bowel Diseases"] = "K50-K51",
        ["Crohn Disease"] = "K50", ["Ulcerative Colitis"] = "K51",
        ["Irritable Bowel Syndrome"] = "K58",
        ["Pancreatitis"] = "K85", ["Cirrhosis"] = "K74",
        ["Psoriasis"] = "L40", ["Dermatitis, Atopic"] = "L20",
        ["Glaucoma"] = "H40", ["Cataract"] = "H25",
        ["Macular Degeneration"] = "H35.3",
        ["Sinusitis"] = "J01",
        ["Anemia"] = "D50-D64",
        ["Hypothyroidism"] = "E03", ["Hyperthyroidism"] = "E05",
        ["Fractures, Bone"] = "S12-S92",
        ["Pre-Eclampsia"] = "O14",
        ["Heart Defects, Congenital"] = "Q20-Q24",
        ["Autism Spectrum Disorder"] = "F84.0",
        ["Attention Deficit Disorder with Hyperactivity"] = "F90",
        ["Alcoholism"] = "F10.2", ["Tobacco Use Disorder"] = "F17.2",
        ["Polymyalgia Rheumatica"] = "M35.3", ["Giant Cell Arteritis"] = "M31.6",
        ["Graft Rejection"] = "T86",
    };

    string TryIcd10(string name)
    {
        if (icdMap.TryGetValue(name, out var icd)) return icd;
        var slashIdx = name.IndexOf(" / ", StringComparison.Ordinal);
        if (slashIdx > 0)
        {
            var baseName = name.Substring(0, slashIdx).Trim();
            if (icdMap.TryGetValue(baseName, out var icd2)) return icd2;
        }
        return "-";
    }

    var sb1 = new StringBuilder();
    sb1.AppendLine("NCT ID\tRaw CT.gov Conditions\tFirst PMID\tAll MeSH Terms");

    var sb2 = new StringBuilder();
    sb2.AppendLine("NCT ID\tRaw CT.gov Conditions\tFirst PMID\tDisease MeSH (tree C*/F03*)\tICD-10 Match");

    var allRawConds = new HashSet<string>();
    var allDiseaseMesh = new HashSet<string>();
    var allIcdCodes = new HashSet<string>();
    var diseaseStudyCount = 0;

    foreach (var study in studies)
    {
        var raw = string.Join(" | ", study.Conditions);
        foreach (var c in study.Conditions) allRawConds.Add(c);

        var allTerms = new List<string>();
        var diseaseTerms = new List<string>();
        var icds = new HashSet<string>();

        if (study.FirstPmid != null && pmidMesh.TryGetValue(study.FirstPmid, out var meshList))
        {
            foreach (var (name, qual, ui) in meshList)
            {
                var term = qual.Length > 0 ? name + " / " + qual : name;
                allTerms.Add(term);
                if (IsDisease(ui))
                {
                    diseaseTerms.Add(term);
                    allDiseaseMesh.Add(name);
                    var icd = TryIcd10(name);
                    if (icd != "-") { icds.Add(icd); allIcdCodes.Add(icd); }
                }
            }
        }

        var allStr = allTerms.Count > 0 ? string.Join(" | ", allTerms) : "(no MeSH)";
        var diseaseStr = diseaseTerms.Count > 0 ? string.Join(" | ", diseaseTerms) : "(none)";
        var icdStr = icds.Count > 0 ? string.Join(", ", icds) : "-";
        if (diseaseTerms.Count > 0) diseaseStudyCount++;

        sb1.AppendLine(study.NctId + "\t" + raw + "\t" + (study.FirstPmid ?? "-") + "\t" + allStr);
        sb2.AppendLine(study.NctId + "\t" + raw + "\t" + (study.FirstPmid ?? "-") + "\t" + diseaseStr + "\t" + icdStr);
    }

    sb1.AppendLine();
    sb1.AppendLine("=== Summary ===");
    sb1.AppendLine("Studies sampled:\t" + studies.Count);
    sb1.AppendLine("Studies with any MeSH:\t" + pmidMesh.Count);
    sb1.AppendLine("Unique raw CT.gov conditions:\t" + allRawConds.Count);

    sb2.AppendLine();
    sb2.AppendLine("=== Summary ===");
    sb2.AppendLine("Studies sampled:\t" + studies.Count);
    sb2.AppendLine("Studies with disease MeSH:\t" + diseaseStudyCount);
    sb2.AppendLine("Studies with NO disease MeSH:\t" + (studies.Count - diseaseStudyCount));
    sb2.AppendLine("Unique raw CT.gov conditions:\t" + allRawConds.Count);
    sb2.AppendLine("Unique disease MeSH descriptors:\t" + allDiseaseMesh.Count);
    sb2.AppendLine("Unique ICD-10 codes mapped:\t" + allIcdCodes.Count);

    await File.WriteAllTextAsync(outputFile1, sb1.ToString(), Encoding.UTF8).ConfigureAwait(false);
    Console.WriteLine("Done. Output written to " + outputFile1);
    await File.WriteAllTextAsync(outputFile2, sb2.ToString(), Encoding.UTF8).ConfigureAwait(false);
    Console.WriteLine("Done. Output written to " + outputFile2);
}
