using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Scrapers.Models;
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

    private static readonly List<MeshDescriptorEntity> SeededDescriptors = GenerateSeededDescriptors();

    private static List<MeshDescriptorEntity> GenerateSeededDescriptors()
    {
        var descs = new List<MeshDescriptorEntity>();
        for (int i = 0; i < Conditions.Length; i++)
        {
            descs.Add(new MeshDescriptorEntity
            {
                Id = 2000 + i,
                Cui = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                Name = Conditions[i],
                TreeNumbers = ["Z99.999"],
                Category = "disease"
            });
        }
        return descs;
    }

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

    // Investigator name components for generating realistic names
    private static readonly string[] FirstNames =
    [
        "James", "Sarah", "Michael", "Jennifer", "David", "Emma", "Robert", "Lisa",
        "William", "Mary", "Richard", "Patricia", "Joseph", "Barbara", "Thomas", "Susan",
        "Charles", "Jessica", "Christopher", "Sarah", "Daniel", "Karen", "Matthew", "Nancy",
        "Anthony", "Margaret", "Mark", "Betty", "Donald", "Donna", "Steven", "Dorothy",
        "Paul", "Carol", "Andrew", "Ruth", "Joshua", "Sharon", "Kenneth", "Anna",
        "Kevin", "Brenda", "Brian", "Pamela", "George", "Nicole", "Edward", "Samantha"
    ];

    private static readonly string[] LastNames =
    [
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis",
        "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson",
        "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson",
        "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson", "Walker",
        "Young", "Allen", "King", "Wright", "Scott", "Torres", "Peterson", "Phillips",
        "Campbell", "Parker", "Evans", "Edwards", "Collins", "Reeves", "Morris", "Murphy",
        "Chen", "Kim", "Patel", "Singh"
    ];

    private static readonly string[] Titles =
    [
        "Dr.", "Prof.", "Prof. Dr."
    ];

    private static readonly string[] Degrees =
    [
        "MD", "PhD", "MD, PhD", "MSc", "MD, MSc", "DM"
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
        optionsBuilder.ConfigureNpgsql(_connectionString);
        using ClinicalTrialsContext context = new(optionsBuilder.Options);

        // Check if database already has data
        if (await context.Studies.AnyAsync(cancellationToken))
        {
            return; // Database already seeded
        }

        // Generate 120 realistic clinical trial records
        var (studies, persons) = GenerateStudies(120);
        
        await context.MeshDescriptors.AddRangeAsync(SeededDescriptors, cancellationToken);
        await context.InvestigatorPersons.AddRangeAsync(persons, cancellationToken);
        await context.Studies.AddRangeAsync(studies, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Drops the database, recreates the schema, and seeds with the current corpus
    /// (120 auto-generated studies). Useful for testing and development.
    /// </summary>
    public async Task InitializeAndSeedAsync(CancellationToken cancellationToken = default)
    {
        var repo = new StudyRepository(_connectionString);
        await repo.ResetDatabaseAsync(cancellationToken);

        // Database is now empty — seed with current corpus
        var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ClinicalTrialsContext>();
        optionsBuilder.ConfigureNpgsql(_connectionString);
        using ClinicalTrialsContext context = new(optionsBuilder.Options);
        var (studies, persons) = GenerateStudies(120);
        await context.MeshDescriptors.AddRangeAsync(SeededDescriptors, cancellationToken);
        await context.InvestigatorPersons.AddRangeAsync(persons, cancellationToken);
        await context.Studies.AddRangeAsync(studies, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static (List<StudyEntity> Studies, List<InvestigatorPersonEntity> Persons) GenerateStudies(int count)
    {
        // Use RandomNumberGenerator wrapper to avoid CA5394
        var randomWrapper = new SecureRandom(RandomSeed);
        var studies = new List<StudyEntity>();
        var usedNctIds = new HashSet<string>();

        // Pre-generate a pool of unique investigator names for reuse across studies
        var investigatorPool = GenerateInvestigatorPool(100);
        
        // Pre-generate InvestigatorPersonEntity for each unique name
        var personMap = new Dictionary<string, InvestigatorPersonEntity>(StringComparer.OrdinalIgnoreCase);
        var persons = new List<InvestigatorPersonEntity>();
        foreach (var name in investigatorPool)
        {
            var person = new InvestigatorPersonEntity
            {
                Id = Guid.NewGuid(),
                FullName = name,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            personMap[name] = person;
            persons.Add(person);
        }

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
                < 20 => PhaseConstants.EarlyPhase1,
                < 50 => randomWrapper.Next(2) == 0 ? PhaseConstants.Phase1 : PhaseConstants.Phase2,
                < 80 => PhaseConstants.Phase3,
                _ => PhaseConstants.Phase4
            };

            // Random enrollment (5-1000)
            var enrollment = randomWrapper.Next(5, 1001);

            // 1-5 conditions per study
            var conditionCount = randomWrapper.Next(1, 6);
            var conditions = new List<StudyConditionEntity>();
            for (int c = 0; c < conditionCount; c++)
            {
                var idx = randomWrapper.Next(SeededDescriptors.Count);
                conditions.Add(new StudyConditionEntity
                {
                    MeshDescriptorId = SeededDescriptors[idx].Id
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

            // Create study investigators (1 PI, 0-2 co-investigators)
            var studyInvestigators = new List<StudyInvestigatorEntity>();
            
            // Always add 1 principal investigator from the pool
            var piName = investigatorPool[randomWrapper.Next(investigatorPool.Count)];
            studyInvestigators.Add(new StudyInvestigatorEntity
            {
                StudyNctId = nctId,
                InvestigatorPersonId = personMap[piName].Id,
                RoleOnStudy = "PRINCIPAL_INVESTIGATOR",
                IsOverallOfficial = true
            });

            // Add 1-2 co-investigators from the pool
            int coInvestigatorCount = randomWrapper.Next(1, 3);
            var usedInvestigators = new HashSet<string> { piName };
            for (int inv = 0; inv < coInvestigatorCount; inv++)
            {
                string coiName;
                do
                {
                    coiName = investigatorPool[randomWrapper.Next(investigatorPool.Count)];
                } while (usedInvestigators.Contains(coiName));
                usedInvestigators.Add(coiName);

                studyInvestigators.Add(new StudyInvestigatorEntity
                {
                    StudyNctId = nctId,
                    InvestigatorPersonId = personMap[coiName].Id,
                    RoleOnStudy = "CO_INVESTIGATOR",
                    IsOverallOfficial = false
                });
            }

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
                StudyInvestigators = studyInvestigators,
                StudyType = AllStudyTypes[randomWrapper.Next(AllStudyTypes.Length)],
                OfficialTitle = GenerateStudyTitle(i, Conditions[randomWrapper.Next(Conditions.Length)])
            };

            studies.Add(study);
        }

        return (studies, persons);
    }

    private static List<string> GenerateInvestigatorPool(int poolSize)
    {
        var randomWrapper = new SecureRandom(RandomSeed);
        var pool = new HashSet<string>();
        
        // Add some guaranteed names for testing purposes (with degrees for test assertions)
        pool.Add("Dr. Anna Smith, MD");
        pool.Add("Dr. Campbell Lee, PhD");
        pool.Add("Dr. Joshua Wright, MD, MSc");
        pool.Add("Prof. Michael Johnson, MD");
        pool.Add("Prof. Dr. Patricia Williams, PhD");
        pool.Add("Dr. Robert Davis, MSc");
        pool.Add("Prof. Susan Miller, DM");
        
        var attempts = 0;
        const int maxAttempts = 1000; // Prevent infinite loop

        while (pool.Count < poolSize && attempts < maxAttempts)
        {
            var name = GenerateInvestigatorName(randomWrapper);
            pool.Add(name); // HashSet automatically prevents duplicates
            attempts++;
        }

        return new List<string>(pool);
    }

    private static string GenerateInvestigatorName(SecureRandom randomWrapper)
    {
        var firstName = FirstNames[randomWrapper.Next(FirstNames.Length)];
        var lastName = LastNames[randomWrapper.Next(LastNames.Length)];
        var title = Titles[randomWrapper.Next(Titles.Length)];
        var degree = Degrees[randomWrapper.Next(Degrees.Length)];

        return $"{title} {firstName} {lastName}, {degree}";
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
