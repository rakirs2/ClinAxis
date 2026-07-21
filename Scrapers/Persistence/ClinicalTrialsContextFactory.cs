using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Scrapers.Persistence;

public class ClinicalTrialsContextFactory : IDesignTimeDbContextFactory<ClinicalTrialsContext>
{
    public ClinicalTrialsContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClinicalTrialsContext>();
        optionsBuilder.ConfigureNpgsql(ConnectionStringProvider.Default);
        return new ClinicalTrialsContext(optionsBuilder.Options);
    }
}
