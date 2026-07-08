using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;

namespace Scrapers.IntegrationTests.Utilities
{
    public abstract class EphemeralDbTestBase
    {
        protected EphemeralPostgresDatabase EphemeralDb = null!;
        protected DbContextOptions<ClinicalTrialsContext> DbContextOptions = null!;
        protected ClinicalTrialsContext Context = null!;
        protected string ConnectionString = null!;
        private IDbContextTransaction? _transaction;

        [TestInitialize]
        public async Task InitializeAsync()
        {
            EphemeralDb = new EphemeralPostgresDatabase();
            ConnectionString = EphemeralDb.ConnectionString;
            DbContextOptions = new DbContextOptionsBuilder<ClinicalTrialsContext>()
                .UseNpgsql(ConnectionString)
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

            if (EphemeralDb != null)
            {
                await EphemeralDb.DisposeAsync();
                EphemeralDb = null!;
            }
        }
    }
}
