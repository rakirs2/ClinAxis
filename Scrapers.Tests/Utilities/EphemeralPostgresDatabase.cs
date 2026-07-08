using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Scrapers.Tests.Utilities
{
    public sealed class EphemeralPostgresDatabase : IDisposable
    {
        public string DatabaseName { get; }
        public string ConnectionString { get; }

        public EphemeralPostgresDatabase()
        {
            DatabaseName = "clinical_trial_data_test_" + Guid.NewGuid().ToString("N");
            ConnectionString = $"Host=localhost;Port=5432;Database={DatabaseName};Username={Environment.UserName}";
            CreateDatabase();
        }

        private void CreateDatabase()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "createdb",
                    Arguments = DatabaseName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new Exception($"Failed to create DB {DatabaseName}: {error}");
            }
        }

        public void Dispose()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dropdb",
                    Arguments = DatabaseName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new Exception($"Failed to drop DB {DatabaseName}: {error}");
            }
        }
    }
}
