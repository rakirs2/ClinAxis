using System.Reflection;
using DataApi;
using DataApi.Endpoints;
using DataApi.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scrapers;
using Scrapers.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5003");

var connectionString = ConnectionStringProvider.WithPoolLimits(ConnectionStringProvider.Default);

var startupRepo = new StudyRepository(connectionString);

if (args.Contains("--reset-db"))
{
    await startupRepo.ResetDatabaseAsync();
    await Console.Out.WriteLineAsync("Database reset complete.");
    await SeedMeshDescriptors(startupRepo);
    await startupRepo.BackfillTreePathsAsync();
    return;
}

var maxRetries = 5;
var retryDelay = TimeSpan.FromSeconds(3);
for (int attempt = 1; attempt <= maxRetries; attempt++)
{
    try
    {
        await startupRepo.MigrateSchemaAsync();
    await startupRepo.BackfillTreePathsAsync();
    break;
    }
    catch (Exception ex) when (attempt < maxRetries)
    {
        await Console.Error.WriteLineAsync($"Database connection failed (attempt {attempt}/{maxRetries}): {ex.Message}");
        await Task.Delay(retryDelay);
    }
}

await SeedMeshDescriptors(startupRepo);

builder.Services.AddHealthChecks()
    .AddCheck("database", new DatabaseHealthCheck(connectionString), failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);
builder.Services.AddMemoryCache();

var meshTreeStore = new MeshTreeStore(connectionString);
await meshTreeStore.InitializeAsync();

var meshJsonPath = ResolveMeshJsonPath();
if (meshJsonPath != null)
    await meshTreeStore.LoadSynonymsAsync(meshJsonPath);

builder.Services.AddSingleton(meshTreeStore);

var piModelPath = ResolvePiModelPath();
if (piModelPath != null)
    builder.Services.AddSingleton(_ => PiCompletionModel.TryLoad(piModelPath)!);
else
    await Console.Out.WriteLineAsync("  [PI Model] model.onnx not found, ModelScore will be null");

_ = Task.Run(async () =>
{
    await Task.Delay(TimeSpan.FromSeconds(15));
    meshTreeStore.GetOrBuildTree("mesh||1|5", () => meshTreeStore.BuildTree(null, 1, 5));
});

builder.Services.AddHostedService<MeshTreeCountRefreshService>();

WebApplication app = builder.Build();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var assembly = typeof(Program).Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0.0";
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;
        var response = new
        {
            status = report.Status.ToString(),
            application = "DataApi",
            version,
            informationalVersion = infoVersion,
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
        };
        context.Response.ContentType = "application/json";
        await System.Text.Json.JsonSerializer.SerializeAsync(context.Response.Body, response).ConfigureAwait(false);
    }
});

app.MapStudiesEndpoints(connectionString, meshTreeStore);
app.MapInvestigatorsEndpoints(connectionString);
app.MapEventQueueEndpoints(connectionString);
app.MapStatsEndpoints(connectionString);
app.MapPipelineEndpoints(connectionString);
app.MapTrainingExportEndpoints(connectionString);
app.MapExportEndpoints(connectionString);
app.MapInvestigatorFinderEndpoints(connectionString, app.Services.GetService<PiCompletionModel>());

await app.RunAsync();

static string? ResolveMeshJsonPath()
{
    var baseDir = AppContext.BaseDirectory;
    var possiblePaths = new[]
    {
        Path.Combine(baseDir, "Resources", "mesh", "mesh_terms.json"),
        Path.Combine(baseDir, "..", "..", "..", "..", "Scrapers", "Resources", "mesh", "mesh_terms.json"),
        Path.Combine(baseDir, "..", "..", "..", "..", "..", "Scrapers", "Resources", "mesh", "mesh_terms.json"),
    };

    foreach (var path in possiblePaths)
    {
        var full = Path.GetFullPath(path);
        if (File.Exists(full))
            return full;
    }
    return null;
}

static string? ResolvePiModelPath()
{
    var baseDir = AppContext.BaseDirectory;
    var possiblePaths = new[]
    {
        Path.Combine(baseDir, "Resources", "pi-model"),
        Path.Combine(baseDir, "..", "..", "..", "..", "Scrapers", "Resources", "pi-model"),
        Path.Combine(baseDir, "..", "..", "..", "..", "..", "Scrapers", "Resources", "pi-model"),
    };

    foreach (var path in possiblePaths)
    {
        if (Directory.Exists(path))
            return path;
    }
    return null;
}

static async Task SeedMeshDescriptors(StudyRepository repo)
{
    var full = ResolveMeshJsonPath();
    if (full != null)
        await repo.SeedMeshDescriptorsAsync(full, CancellationToken.None);
    else
        await Console.Out.WriteLineAsync("  [MeSH] mesh_terms.json not found, skipping descriptor seed");
}

namespace DataApi
{
    partial class Program { }
}
