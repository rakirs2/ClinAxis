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

    public static string LoadCmsMedicareJson(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "CmsMedicare", fileName);

        return !File.Exists(path) ? throw new FileNotFoundException($"Fixture not found: {path}") : File.ReadAllText(path);
    }

    public static string LoadSemanticScholarJson(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "SemanticScholar", fileName);

        return !File.Exists(path) ? throw new FileNotFoundException($"Fixture not found: {path}") : File.ReadAllText(path);
    }
}
