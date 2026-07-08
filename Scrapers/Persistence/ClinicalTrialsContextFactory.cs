using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Scrapers.Persistence;

public class ClinicalTrialsContextFactory : IDesignTimeDbContextFactory<ClinicalTrialsContext>
{
    public ClinicalTrialsContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClinicalTrialsContext>();
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=clinical_trial_data;Username=postgres;Password=postgres;";
        optionsBuilder.UseNpgsql(connectionString);
        return new ClinicalTrialsContext(optionsBuilder.Options);
    }
}
