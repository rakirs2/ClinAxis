using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Scrapers.Persistence;

public class ClinicalTrialsContextFactory : IDesignTimeDbContextFactory<ClinicalTrialsContext>
{
    public ClinicalTrialsContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClinicalTrialsContext>();
        optionsBuilder.UseNpgsql(ConnectionStringProvider.Default, options =>
        {
            options.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        });
        return new ClinicalTrialsContext(optionsBuilder.Options);
    }
}
