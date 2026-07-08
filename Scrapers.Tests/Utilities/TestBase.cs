using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using System;
using System.Threading.Tasks;

namespace Scrapers.Tests.Utilities
{
    public abstract class TestBase
    {
        protected DbContextOptions<ClinicalTrialsContext> DbContextOptions { get; private set; } = null!;
        protected ClinicalTrialsContext Context { get; private set; } = null!;

        private IDbContextTransaction? _transaction;

        [TestInitialize]
        public async Task Initialize()
        {
            DbContextOptions = new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING") ?? $"Host=localhost;Port=5432;Database=clinical_trial_data;Username={Environment.UserName}")
                .Options;

            Context = new ClinicalTrialsContext(DbContextOptions);
            await Context.Database.MigrateAsync();

            _transaction = await Context.Database.BeginTransactionAsync();
        }

        [TestCleanup]
        public async Task Cleanup()
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
