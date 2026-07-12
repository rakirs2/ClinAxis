namespace Scrapers.Tests.Helpers;

internal static class FixtureLoader
{
    public static string LoadClinicalTrialsGovJson(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "ClinicalTrialsGov", fileName);

        return !File.Exists(path) ? throw new FileNotFoundException($"Fixture not found: {path}") : File.ReadAllText(path);
    }

    public static string LoadDataApiJson(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "DataApi", fileName);

        return !File.Exists(path) ? throw new FileNotFoundException($"Fixture not found: {path}") : File.ReadAllText(path);
    }

    public static string LoadNppesNpiRegistryJson(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "NppesNpiRegistry", fileName);
        return !File.Exists(path) ? throw new FileNotFoundException($"Fixture not found: {path}") : File.ReadAllText(path);
    }

    public static string LoadOrcidApiJson(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "OrcidApi", fileName);
        return !File.Exists(path) ? throw new FileNotFoundException($"Fixture not found: {path}") : File.ReadAllText(path);
    }
}
