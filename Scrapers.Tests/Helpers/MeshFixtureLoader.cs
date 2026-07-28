namespace Scrapers.Tests.Helpers;

internal static class MeshFixtureLoader
{
    public static string LoadMeshJson(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Mesh", fileName);
        return !File.Exists(path) ? throw new FileNotFoundException($"Mesh fixture not found: {path}") : path;
    }
}
