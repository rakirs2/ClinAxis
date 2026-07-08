namespace Scrapers.Tests.Helpers;

internal static class FixtureLoader
{
    public static string LoadClinicalTrialsGovJson(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "ClinicalTrialsGov", fileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Fixture not found: {path}");
        }

        return File.ReadAllText(path);
    }
}
