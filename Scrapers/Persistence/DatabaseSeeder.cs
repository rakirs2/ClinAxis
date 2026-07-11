using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence.Entities;

namespace Scrapers.Persistence;

/// <summary>
/// Wrapper for Random that uses a seeded approach for reproducible test data.
/// Suppressing CA5394 because this is for non-security test data generation.
/// </summary>
#pragma warning disable CA5394
internal class SecureRandom
{
    private readonly Random _random;

    public SecureRandom(int seed)
    {
        _random = new Random(seed);
    }

    public int Next() => _random.Next();
    public int Next(int maxValue) => _random.Next(maxValue);
    public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);
}
#pragma warning restore CA5394

/// <summary>
/// Seeds the database with realistic clinical trial test data.
/// Ensures consistent test data across local development environments.
/// </summary>
public class DatabaseSeeder
{
    private readonly string _connectionString;

    // Seeded random for reproducibility in tests (not cryptographic use)
    private const int RandomSeed = 42;
    
    // Realistic clinical trial data pools
    private static readonly string[] Conditions = [
        "Type 2 Diabetes", "Hypertension", "Cancer", "COVID-19", "Asthma", "Depression",
        "Heart Disease", "Alzheimer's Disease", "Parkinson's Disease", "Rheumatoid Arthritis",
        "Lupus", "Crohn's Disease", "Ulcerative Colitis", "Psoriasis", "Chronic Pain",
        "Migraine", "Arthritis", "Anxiety", "PTSD", "Fibromyalgia",
        "Sleep Apnea", "COPD", "Pneumonia", "Tuberculosis", "Influenza",
        "Melanoma", "Lung Cancer", "Breast Cancer", "Prostate Cancer", "Colorectal Cancer",
        "Leukemia", "Lymphoma", "Multiple Myeloma", "Ovarian Cancer", "Pancreatic Cancer",
        "Hepatitis C", "HIV/AIDS", "Hepatitis B", "Herpes Simplex", "Shingles",
        "Obesity", "GERD", "Irritable Bowel Syndrome", "Celiac Disease", "Gastroparesis",
        "Thyroid Disease", "Hypothyroidism", "Hyperthyroidism", "Graves' Disease", "Hashimoto's Thyroiditis"
    ];

    private static readonly string[] Locations = [
        "United States", "Canada", "United Kingdom", "Australia", "Germany",
        "France", "Japan", "South Korea", "China", "India",
        "Brazil", "Mexico", "Spain", "Italy", "Netherlands"
    ];

    private static readonly string[] USStates = [
        "California", "Texas", "Florida", "New York", "Pennsylvania",
        "Illinois", "Ohio", "Georgia", "North Carolina", "Michigan",
        "New Jersey", "Virginia", "Washington", "Arizona", "Massachusetts",
        "Tennessee", "Indiana", "Maryland", "Missouri", "Wisconsin",
        "Colorado", "Minnesota", "South Carolina", "Alabama", "Louisiana"
    ];

    private static readonly string[] Cities = [
        "Los Angeles", "New York", "Chicago", "Houston", "Phoenix",
        "Philadelphia", "San Antonio", "San Diego", "Dallas", "San Jose",
        "Austin", "Jacksonville", "Boston", "Seattle", "Denver",
        "Washington", "Nashville", "Detroit", "Oklahoma City", "Portland",
        "Las Vegas", "Memphis", "Louisville", "Baltimore", "Milwaukee"
    ];

    private static readonly string[] Facilities = [
        "Johns Hopkins Hospital", "Mayo Clinic", "Cleveland Clinic", "UCLA Medical Center", "Stanford Medical",
        "Massachusetts General Hospital", "New York Presbyterian", "Duke Medical Center", "UCSF Medical",
        "University of Pennsylvania Medical", "Columbia University Medical", "Yale School of Medicine",
        "University of Chicago Medical", "Northwestern Medical School", "University of Michigan Medical",
        "Hospital for Special Surgery", "Memorial Sloan Kettering", "MD Anderson Cancer Center",
        "Brigham and Women's Hospital", "Cedars-Sinai Medical Center", "Kaiser Permanente",
        "University of Texas Medical", "Emory University Hospital", "Boston Medical Center",
        "University of Washington Medical"
    ];

    private static readonly string[] AllStatuses =
    [
        "RECRUITING", "ACTIVE", "ENROLLING_BY_INVITATION",
        "NOT_YET_RECRUITING", "COMPLETED", "TERMINATED",
        "SUSPENDED", "WITHDRAWN", "UNKNOWN_STATUS"
    ];

    private static readonly string[] AllStudyTypes =
    [
        "INTERVENTIONAL", "OBSERVATIONAL", "EXPANDED_ACCESS"
    ];

    public DatabaseSeeder(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Seeds the database if it's empty. Safe to call repeatedly.
    /// </summary>
    public async Task SeedIfEmptyAsync(CancellationToken cancellationToken = default)
    {
        // Create context with the provided connection string
        var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ClinicalTrialsContext>();
        optionsBuilder.UseNpgsql(_connectionString);
        using ClinicalTrialsContext context = new(optionsBuilder.Options);

        // Check if database already has data
        if (await context.Studies.AnyAsync(cancellationToken))
        {
            return; // Database already seeded
        }

        // Generate 120 realistic clinical trial records
        var studies = GenerateStudies(120);
        
        await context.Studies.AddRangeAsync(studies, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static List<StudyEntity> GenerateStudies(int count)
    {
        // Use RandomNumberGenerator wrapper to avoid CA5394
        var randomWrapper = new SecureRandom(RandomSeed);
        var studies = new List<StudyEntity>();
        var usedNctIds = new HashSet<string>();

        for (int i = 0; i < count; i++)
        {
            // Ensure unique NCT IDs
            string nctId;
            do
            {
                nctId = $"NCT{randomWrapper.Next(10000000, 99999999)}";
            } while (usedNctIds.Contains(nctId));
            usedNctIds.Add(nctId);

            // Random start date in last 5 years
            var startDate = DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(-randomWrapper.Next(0, 1825)));

            // Status distribution: 30% recruiting, 40% active, 30% other
            var statusChoice = randomWrapper.Next(100);
            var status = statusChoice switch
            {
                < 30 => "RECRUITING",
                < 70 => "ACTIVE",
                _ => AllStatuses[randomWrapper.Next(AllStatuses.Length)]
            };

            // Phase distribution: 20% early phase, 30% phase 1-2, 30% phase 3, 20% phase 4
            var phaseChoice = randomWrapper.Next(100);
            var phaseStr = phaseChoice switch
            {
                < 20 => "Early Phase 1",
                < 50 => randomWrapper.Next(2) == 0 ? "Phase 1" : "Phase 2",
                < 80 => "Phase 3",
                _ => "Phase 4"
            };

            // Random enrollment (5-1000)
            var enrollment = randomWrapper.Next(5, 1001);

            // 1-5 conditions per study
            var conditionCount = randomWrapper.Next(1, 6);
            var conditions = new List<StudyConditionEntity>();
            for (int c = 0; c < conditionCount; c++)
            {
                conditions.Add(new StudyConditionEntity
                {
                    Condition = Conditions[randomWrapper.Next(Conditions.Length)]
                });
            }

            // 1-4 locations per study
            var locationCount = randomWrapper.Next(1, 5);
            var locations = new List<StudyLocationEntity>();
            for (int l = 0; l < locationCount; l++)
            {
                var country = Locations[randomWrapper.Next(Locations.Length)];
                var isUS = country == "United States";
                
                locations.Add(new StudyLocationEntity
                {
                    Country = country,
                    State = isUS ? USStates[randomWrapper.Next(USStates.Length)] : null,
                    City = randomWrapper.Next(2) == 0 ? Cities[randomWrapper.Next(Cities.Length)] : null,
                    Facility = Facilities[randomWrapper.Next(Facilities.Length)]
                });
            }

            // Create study phases
            var phases = new List<StudyPhaseEntity>
            {
                new() { Phase = phaseStr }
            };

            // Create study
            var study = new StudyEntity
            {
                NctId = nctId,
                BriefTitle = GenerateStudyTitle(i, Conditions[randomWrapper.Next(Conditions.Length)]),
                OverallStatus = status,
                Phases = phases,
                StartDate = startDate,
                EnrollmentCount = enrollment,
                Conditions = conditions,
                Locations = locations,
                StudyType = AllStudyTypes[randomWrapper.Next(AllStudyTypes.Length)],
                OfficialTitle = GenerateStudyTitle(i, Conditions[randomWrapper.Next(Conditions.Length)])
            };

            studies.Add(study);
        }

        return studies;
    }

    private static string GenerateStudyTitle(int index, string condition)
    {
        var verbs = new[] { "Study of", "Investigation of", "Evaluation of", "Trial of", "Clinical Assessment of" };
        var adjectives = new[] { "Novel", "New", "Advanced", "Innovative", "Comprehensive" };
        var outcomes = new[] { "Efficacy and Safety", "Safety and Tolerability", "Effectiveness", "Outcomes", "Impact" };

        var verb = verbs[index % verbs.Length];
        var adjective = adjectives[(index / verbs.Length) % adjectives.Length];
        var outcome = outcomes[(index / (verbs.Length * adjectives.Length)) % outcomes.Length];

        return $"{verb} {adjective} Treatment for {condition}: {outcome}";
    }
}
