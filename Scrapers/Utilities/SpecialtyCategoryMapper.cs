namespace Scrapers.Utilities;

/// <summary>
/// Maps free-text clinical terms (MeSH descriptor names, NPPES taxonomy descriptions)
/// to coarse clinical specialty categories via keyword matching.
/// Used as a corroborating signal when disambiguating NPI candidates.
/// </summary>
internal static class SpecialtyCategoryMapper
{
    private static readonly Dictionary<string, string[]> KeywordsByCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["oncology"] = ["cancer", "oncolog", "neoplasm", "tumor", "tumour", "carcinoma", "sarcoma", "leukemia", "leukaemia", "lymphoma", "melanoma", "myeloma"],
        ["cardiology"] = ["cardiac", "cardiovascular", "cardiology", "heart", "arrhythmia", "coronary", "myocardial", "angina", "heart failure", "cardio"],
        ["neurology"] = ["neurolog", "neurological", "brain", "stroke", "epilepsy", "seizure", "alzheimer", "parkinson", "multiple sclerosis", "neuropathy", "migraine"],
        ["endocrinology"] = ["diabetes", "thyroid", "endocrin", "metabolic", "hormone", "pituitary", "adrenal"],
        ["pulmonology"] = ["pulmonary", "lung", "respiratory", "asthma", "copd", "bronch", "sleep apnea"],
        ["gastroenterology"] = ["gastro", "liver", "hepatic", "colon", "intestinal", "bowel", "pancreat", "esophag", "hepatitis"],
        ["nephrology"] = ["renal", "kidney", "nephro", "dialysis"],
        ["hematology"] = ["hematolog", "haematolog", "blood", "anemia", "anaemia", "coagulation", "platelet"],
        ["psychiatry"] = ["psychiatr", "depress", "anxiety", "schizophrenia", "bipolar", "psycholog", "mental health", "autism", "adhd"],
        ["immunology"] = ["immunolog", "autoimmune", "lupus", "rheumatoid", "allerg", "asthma", "immune"],
        ["infectious_disease"] = ["infectious", "infection", "hiv", "hepatitis", "covid", "virus", "bacterial", "tuberculosis", "sepsis", "antimicrobial"],
        ["orthopedics"] = ["orthopedic", "orthopaedic", "bone", "joint", "arthritis", "spine", "fracture", "knee", "hip", "shoulder"],
        ["dermatology"] = ["dermatolog", "skin", "psoriasis", "eczema", "acne"],
        ["ophthalmology"] = ["ophthalmolog", "eye", "vision", "retina", "glaucoma", "cataract", "corneal"],
        ["urology"] = ["urolog", "prostate", "bladder", "kidney stone", "urinary"],
        ["obstetrics_gynecology"] = ["obstetric", "pregnancy", "maternal", "fetal", "gyn", "menopaus", "fertility", "endometri"],
        ["pediatrics"] = ["pediatr", "paediatr", "child", "neonatal", "infant", "adolescent"],
        ["geriatrics"] = ["geriatr", "elder", "aging", "ageing", "dementia"],
        ["surgery"] = ["surg", "transplant", "operation", "bypass", "laparoscopic"],
        ["radiology"] = ["radiology", "radiolog", "imaging", "magnetic resonance", "ct scan", "radiation", "ultrasound", "x-ray"],
        ["genetics"] = ["genetic", "gene", "inherited", "chromosom", "genomic", "hereditary"],
        ["emergency_medicine"] = ["emergency", "trauma", "critical care", "resuscitation", "intensive care"],
        ["anesthesiology"] = ["anesthesiolog", "anaesthesiolog", "anesthesia", "anaesthesia", "sedation"],
        ["rheumatology"] = ["rheumatolog", "arthritis", "lupus", "gout", "connective tissue", "fibromyalgia"],
        ["otolaryngology"] = ["otolaryngolog", "ent ", "ear, nose", "sinusitis", "hearing", "auditory", "voice"],
        ["dentistry"] = ["dental", "dentist", "oral", "periodont", "orthodont", "tooth", "gingiv"],
    };

    private static readonly string[] CategoryNames = KeywordsByCategory.Keys.OrderBy(k => k).ToArray();

    /// <summary>
    /// Returns the set of specialty categories whose keywords appear in <paramref name="text"/>.
    /// Empty when nothing matches.
    /// </summary>
    public static HashSet<string> MapToCategories(string text)
    {
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return categories;
        }

        foreach (var (category, keywords) in KeywordsByCategory)
        {
            foreach (var keyword in keywords)
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    categories.Add(category);
                    break;
                }
            }
        }

        return categories;
    }

    /// <summary>
    /// Returns true when the two category sets share at least one category.
    /// </summary>
    public static bool AnyOverlap(IEnumerable<string> left, IEnumerable<string> right)
    {
        var rightSet = right.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return left.Any(rightSet.Contains);
    }

    /// <summary>
    /// All known category names (used by the ML corpus export).
    /// </summary>
    public static IReadOnlyList<string> AllCategories => CategoryNames;
}
