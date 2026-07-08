using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Scrapers.IntegrationTests.Utilities
{
    public sealed class EphemeralPostgresDatabase : IAsyncDisposable
    {
        public string DatabaseName { get; }
        public string ConnectionString { get; }

        public EphemeralPostgresDatabase()
        {
            var envConnectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");

            string host, port, username, password;
            if (!string.IsNullOrWhiteSpace(envConnectionString))
            {
                (host, port, username, password) = ParseConnectionString(envConnectionString);
            }
            else
            {
                host = "localhost";
                port = "5432";
                username = Environment.UserName;
                password = "";
            }

            DatabaseName = "clinical_trial_data_test_" + Guid.NewGuid().ToString("N").ToLowerInvariant();
            ConnectionString = $"Host={host};Port={port};Database={DatabaseName};Username={username};Pooling=false;{(password != "" ? $"Password={password};" : "")}";
            CreateDatabaseAsync(host, port, username, password).GetAwaiter().GetResult();
        }

        private static (string host, string port, string username, string password) ParseConnectionString(string connStr)
        {
            var host = ExtractValue(connStr, "Host") ?? "localhost";
            var port = ExtractValue(connStr, "Port") ?? "5432";
            var username = ExtractValue(connStr, "Username") ?? Environment.UserName;
            var password = ExtractValue(connStr, "Password") ?? "";
            return (host, port, username, password);
        }

        private static string? ExtractValue(string connStr, string key)
        {
            var match = Regex.Match(connStr, $@"{key}\s*=\s*([^;]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        private async Task CreateDatabaseAsync(string host, string port, string username, string password)
        {
            var args = $"--host {host} --port {port} --username {username} {DatabaseName}";
            var createProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "createdb",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!string.IsNullOrEmpty(password))
            {
                createProcess.StartInfo.EnvironmentVariables["PGPASSWORD"] = password;
            }

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
            var connStr = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
            string host, port, username, password;
            if (!string.IsNullOrWhiteSpace(connStr))
            {
                (host, port, username, password) = ParseConnectionString(connStr);
            }
            else
            {
                host = "localhost";
                port = "5432";
                username = Environment.UserName;
                password = "";
            }

            var args = $"--host {host} --port {port} --username {username} {DatabaseName}";
            var dropProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dropdb",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!string.IsNullOrWhiteSpace(password))
                dropProcess.StartInfo.EnvironmentVariables["PGPASSWORD"] = password;

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