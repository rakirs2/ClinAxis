using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Scrapers.Tests.Utilities
{
    public sealed class EphemeralPostgresDatabase : IAsyncDisposable
    {
        public string DatabaseName { get; }
        public string ConnectionString { get; }

        public EphemeralPostgresDatabase()
        {
            DatabaseName = "clinical_trial_data_test_" + Guid.NewGuid().ToString("N").ToLowerInvariant();
            ConnectionString = $"Host=localhost;Port=5432;Database={DatabaseName};Username={Environment.UserName};Pooling=false;";
            CreateDatabaseAsync().GetAwaiter().GetResult();
        }

        private async Task CreateDatabaseAsync()
        {
            var createProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "createdb",
                    Arguments = $"--host localhost --port 5432 {DatabaseName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            createProcess.Start();
            await createProcess.WaitForExitAsync();

            if (createProcess.ExitCode != 0)
            {
                string error = await createProcess.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Failed to create database {DatabaseName}: {error}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            var dropProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dropdb",
                    Arguments = $"--host localhost --port 5432 {DatabaseName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            dropProcess.Start();
            await dropProcess.WaitForExitAsync();

            if (dropProcess.ExitCode != 0)
            {
                string error = await dropProcess.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Failed to drop database {DatabaseName}: {error}");
            }
        }
    }
}
