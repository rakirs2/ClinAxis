using System.Text.Json;
using Scrapers.Persistence.Entities;
using Scrapers.Services.Enrichment;

namespace Scrapers.Utilities
{
    /// <summary>
    /// Rebuilds an <see cref="NpiRegistryResult"/> from a persisted
    /// <see cref="PersonIdentifierCandidateEntity"/> row. The enrichment service
    /// persists every NPPES field (PR #337), so the NPI match features can be
    /// recomputed at export time with exact parity to the original run — same
    /// selection rules for address (LOCATION first) and taxonomy (primary first).
    /// </summary>
    internal static class NpiCandidateReconstructor
    {
        /// <summary>
        /// Reconstructs the NPPES response for one stored candidate. Returns null
        /// when the row does not look like an NPI candidate.
        /// </summary>
        public static NpiRegistryResult Reconstruct(PersonIdentifierCandidateEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            var basic = new NpiBasic
            {
                MiddleName = entity.MatchedMiddleName,
                Credential = entity.MatchedCredential,
                NamePrefix = entity.MatchedNamePrefix,
                Gender = entity.MatchedGender,
                OrganizationName = entity.MatchedAffiliation,
                OtherNames = Deserialize<NpiOtherName>(entity.MatchedOtherNamesJson)
            };

            // MatchedFullName was persisted as "{FirstName} {LastName}".Trim(); a
            // single-token value is treated as a last name (NPPES person records
            // carry both; single-token records are malformed and rare).
            if (!string.IsNullOrWhiteSpace(entity.MatchedFullName))
            {
                var tokens = entity.MatchedFullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                basic.FirstName = tokens.Length > 1 ? tokens[0] : null;
                basic.LastName = tokens[^1];
            }

            IReadOnlyList<NpiAddress>? addresses = null;
            if (!string.IsNullOrWhiteSpace(entity.MatchedCity) || !string.IsNullOrWhiteSpace(entity.MatchedState))
            {
                addresses =
                [
                    new NpiAddress
                    {
                        AddressPurpose = "LOCATION",
                        City = entity.MatchedCity,
                        State = entity.MatchedState
                    }
                ];
            }

            IReadOnlyList<NpiTaxonomy>? taxonomies = null;
            if (!string.IsNullOrWhiteSpace(entity.MatchedTaxonomyDesc)
                || !string.IsNullOrWhiteSpace(entity.MatchedTaxonomyLicense)
                || !string.IsNullOrWhiteSpace(entity.MatchedTaxonomyState))
            {
                taxonomies =
                [
                    new NpiTaxonomy
                    {
                        Primary = true,
                        Desc = entity.MatchedTaxonomyDesc,
                        License = entity.MatchedTaxonomyLicense,
                        State = entity.MatchedTaxonomyState
                    }
                ];
            }

            return new NpiRegistryResult
            {
                Number = entity.IdentifierValue,
                Basic = basic,
                Addresses = addresses,
                Taxonomies = taxonomies,
                Identifiers = Deserialize<NpiIdentifier>(entity.MatchedIdentifiersJson),
                Status = entity.SourceStatus,
                DeactivationDate = entity.SourceDeactivatedAt
            };
        }

        private static List<T>? Deserialize<T>(string? json) =>
            string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<List<T>>(json);
    }
}
