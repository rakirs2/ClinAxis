using Scrapers;
using Scrapers.Persistence;
using Scrapers.Services;

var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("POSTGRES_CONNECTION_STRING environment variable is not set.");
    return 1;
}

if (args.Length > 0 && args[0] is "--help" or "-h")
{
    Console.WriteLine("Usage: dotnet run --project IngestionApp [count]");
    return 0;
}

var count = 5;
if (args.Length > 0 && int.TryParse(args[0], out var parsed) && parsed > 0)
{
    count = parsed;
}

var clinicalTrialsClient = new ClinicalTrialsGov();
var repository = new PostgresStudyRepository(connectionString);
var service = new ClinicalTrialsIngestionService(clinicalTrialsClient, repository);

Console.WriteLine($"Fetching and persisting {count} studies...");
try
{
    var saved = await service.IngestAsync(count);
    var studyCount = await repository.CountStudiesAsync();
    Console.WriteLine($"Persisted {saved} studies. Current study count: {studyCount}.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Ingestion failed: {ex.Message}");
    return 1;
}
