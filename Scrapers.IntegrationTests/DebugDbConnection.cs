using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class DebugDbConnection : DbTestBase
{
    [TestMethod]
    [TestCategory("Debug")]
    public async Task InspectDatabase()
    {
        Console.WriteLine($"Connection: {ConnectionString}");
        Console.WriteLine($"Studies: {await Context.Studies.CountAsync()}");
        Console.WriteLine($"Investigator Persons: {await Context.InvestigatorPersons.CountAsync()}");
        // Connect via pgAdmin4 using the connection string printed above.
        Assert.IsTrue(true);
    }
}
