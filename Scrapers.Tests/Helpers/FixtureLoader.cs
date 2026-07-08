namespace Scrapers.Tests.Helpers;

internal static class FixtureLoader
{
    public static string LoadClinicalTrialsGovJson(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "ClinicalTrialsGov", fileName);

        return !File.Exists(path) ? throw new FileNotFoundException($"Fixture not found: {path}") : File.ReadAllText(path);
    }
}
