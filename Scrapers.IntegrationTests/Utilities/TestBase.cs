using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using System;
using System.Threading.Tasks;

namespace Scrapers.IntegrationTests.Utilities
{
    public abstract class TestBase
    {
        protected DbContextOptions<ClinicalTrialsContext> DbContextOptions = null!;
        protected ClinicalTrialsContext Context = null!;
        private IDbContextTransaction? _transaction;

        [TestInitialize]
        public async Task InitializeAsync()
        {
            DbContextOptions = new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING") ?? $"Host=localhost;Port=5432;Database=clinical_trial_data;Username={Environment.UserName}")
                .Options;
            Context = new ClinicalTrialsContext(DbContextOptions);
            await Context.Database.MigrateAsync();
            _transaction = await Context.Database.BeginTransactionAsync();
        }

        [TestCleanup]
        public async Task CleanupAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }

            if (Context != null)
            {
                await Context.DisposeAsync();
                Context = null!;
            }
        }
    }
}
