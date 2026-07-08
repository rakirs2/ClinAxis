using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;

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
                .UseNpgsql(ConnectionStringProvider.Default)
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
