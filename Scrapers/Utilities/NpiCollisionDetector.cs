using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Scrapers.Utilities
{
    /// <summary>
    /// Classifies save failures in the NPI enrichment pipeline.
    /// </summary>
    internal static class NpiCollisionDetector
    {
        /// <summary>
        /// True when the exception is a Postgres unique-violation (23505) on the
        /// <c>investigator_persons.npi</c> index. Duplicate person rows ("John Smith"
        /// vs "John A Smith" — the person lookup dedups by exact full name only) both
        /// match the same NPPES record; the second NPI assignment violates the unique
        /// filtered index and must be recovered, not dead-lettered.
        /// </summary>
        public static bool IsNpiUniqueViolation(DbUpdateException exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            for (Exception? current = exception.InnerException; current != null; current = current.InnerException)
            {
                if (current is not PostgresException { SqlState: "23505" } pg)
                {
                    continue;
                }

                // Server-side errors always carry the constraint name. The empty
                // fallback covers synthetic exceptions (tests) — enrichment's save
                // can only violate the npi index.
                return string.IsNullOrEmpty(pg.ConstraintName)
                    || pg.ConstraintName.EndsWith("_npi", StringComparison.Ordinal);
            }

            return false;
        }
    }
}
