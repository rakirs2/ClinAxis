using System.Text.Json;
using Scrapers.Services.Enrichment;

namespace Scrapers.Utilities
{
    /// <summary>
    /// Person-side signals used to disambiguate NPI candidates.
    /// </summary>
    internal sealed class PersonSignalProfile
    {
        public string FirstName { get; init; } = string.Empty;
        public string? MiddleName { get; init; }
        public string LastName { get; init; } = string.Empty;
        public string? Suffix { get; init; }
        public string? Orcid { get; init; }
        public string? InstitutionName { get; init; }
        public string? Department { get; init; }
        public string? City { get; init; }
        public string? State { get; init; }
        public HashSet<string> SpecialtyCategories { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Match features computed for one NPPES candidate against a person's signal profile.
    /// Also carries the candidate fields that must be persisted to <c>person_identifier_candidates</c>.
    /// <c>Score</c> is set by <see cref="NpiCandidateScorer"/>.
    /// </summary>
    internal sealed class NpiCandidateFeatures
    {
        public string Number { get; init; } = string.Empty;
        public string? MatchedFullName { get; init; }
        public string? MatchedAffiliation { get; init; }
        public string? MatchedState { get; init; }
        public string? MatchedCity { get; init; }
        public string? MatchedMiddleName { get; init; }
        public string? MatchedCredential { get; init; }
        public string? MatchedNamePrefix { get; init; }
        public string? MatchedGender { get; init; }
        public string? MatchedTaxonomyDesc { get; init; }
        public string? MatchedTaxonomyState { get; init; }
        public string? MatchedTaxonomyLicense { get; init; }
        public string? MatchedOtherNamesJson { get; init; }
        public string? MatchedIdentifiersJson { get; init; }

        public bool HasPersonName { get; init; }
        public bool ExactNameMatch { get; init; }
        public bool? MiddleNameMatch { get; init; }
        public bool? CredentialMatch { get; init; }
        public bool? StateMatch { get; init; }
        public bool? CityMatch { get; init; }
        public bool? OrgMatch { get; init; }
        public bool? OtherNameMatch { get; init; }
        public bool? SpecialtyMatch { get; init; }
        public bool? LicenseStateMatch { get; init; }
        public bool? DepartmentMatch { get; init; }
        public bool OrcidMatch { get; init; }
        public bool IsDeactivated { get; init; }

        public double? Score { get; set; }
    }

    /// <summary>
    /// Builds the person signal profile and per-candidate match features for NPI disambiguation.
    /// Pure — no database access.
    /// </summary>
    internal static class NpiFeatureExtractor
    {
        private const string OrcidIdentifierType = "17";

        /// <summary>
        /// Builds a person profile from parsed name parts, primary affiliation data,
        /// and MeSH descriptor names derived from the person's studies.
        /// </summary>
        public static PersonSignalProfile BuildProfile(
            string fullName,
            string? orcid,
            string? institutionName,
            string? department,
            string? city,
            string? state,
            IEnumerable<string> meshDescriptorNames)
        {
            var (prefix, parsedName, suffix) = NameParser.Parse(fullName);
            var (first, middle, last) = ParseFullName(parsedName);

            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var descriptorName in meshDescriptorNames)
            {
                categories.UnionWith(SpecialtyCategoryMapper.MapToCategories(descriptorName));
            }

            return new PersonSignalProfile
            {
                FirstName = first,
                MiddleName = middle,
                LastName = last,
                Suffix = suffix,
                Orcid = orcid,
                InstitutionName = Normalize(institutionName),
                Department = Normalize(department),
                City = Normalize(city),
                State = Normalize(state),
                SpecialtyCategories = categories
            };
        }

        /// <summary>
        /// Computes the match features for a single NPPES candidate against the person profile.
        /// </summary>
        public static NpiCandidateFeatures Extract(PersonSignalProfile profile, NpiRegistryResult result)
        {
            var basic = result.Basic;
            var hasPersonName = !string.IsNullOrWhiteSpace(basic?.FirstName) || !string.IsNullOrWhiteSpace(basic?.LastName);
            var candidateFirst = Normalize(basic?.FirstName);
            var candidateLast = Normalize(basic?.LastName);

            NpiAddress? address = null;
            if (result.Addresses is { Count: > 0 })
            {
                foreach (var a in result.Addresses)
                {
                    if (string.Equals(a.AddressPurpose, "LOCATION", StringComparison.OrdinalIgnoreCase))
                    {
                        address = a;
                        break;
                    }
                }

                address ??= result.Addresses[0];
            }

            var candidateState = Normalize(address?.State);
            var candidateCity = Normalize(address?.City);

            NpiTaxonomy? taxonomy = null;
            if (result.Taxonomies is { Count: > 0 })
            {
                foreach (var t in result.Taxonomies)
                {
                    if (t.Primary)
                    {
                        taxonomy = t;
                        break;
                    }
                }

                taxonomy ??= result.Taxonomies[0];
            }

            return new NpiCandidateFeatures
            {
                Number = result.Number ?? string.Empty,
                MatchedFullName = $"{basic?.FirstName} {basic?.LastName}".Trim(),
                MatchedAffiliation = basic?.OrganizationName,
                MatchedState = candidateState,
                MatchedCity = candidateCity,
                MatchedMiddleName = basic?.MiddleName,
                MatchedCredential = basic?.Credential,
                MatchedNamePrefix = basic?.NamePrefix,
                MatchedGender = basic?.Gender,
                MatchedTaxonomyDesc = taxonomy?.Desc,
                MatchedTaxonomyState = Normalize(taxonomy?.State),
                MatchedTaxonomyLicense = taxonomy?.License,
                MatchedOtherNamesJson = basic?.OtherNames is { Count: > 0 }
                    ? JsonSerializer.Serialize(basic.OtherNames)
                    : null,
                MatchedIdentifiersJson = result.Identifiers is { Count: > 0 }
                    ? JsonSerializer.Serialize(result.Identifiers)
                    : null,

                HasPersonName = hasPersonName,
                ExactNameMatch = hasPersonName
                    && string.Equals(profile.FirstName, candidateFirst, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(profile.LastName, candidateLast, StringComparison.OrdinalIgnoreCase),
                MiddleNameMatch = MiddleNameMatch(profile.MiddleName, basic?.MiddleName),
                CredentialMatch = CredentialMatch(profile.Suffix, basic?.Credential),
                StateMatch = string.IsNullOrWhiteSpace(profile.State) || string.IsNullOrWhiteSpace(candidateState)
                    ? null
                    : string.Equals(profile.State, candidateState, StringComparison.OrdinalIgnoreCase),
                CityMatch = string.IsNullOrWhiteSpace(profile.City) || string.IsNullOrWhiteSpace(candidateCity)
                    ? null
                    : string.Equals(profile.City, candidateCity, StringComparison.OrdinalIgnoreCase),
                OrgMatch = string.IsNullOrWhiteSpace(profile.InstitutionName) || string.IsNullOrWhiteSpace(basic?.OrganizationName)
                    ? null
                    : basic.OrganizationName.Contains(profile.InstitutionName, StringComparison.OrdinalIgnoreCase),
                OtherNameMatch = OtherNameMatch(profile, basic),
                SpecialtyMatch = profile.SpecialtyCategories.Count == 0 || string.IsNullOrWhiteSpace(taxonomy?.Desc)
                    ? null
                    : SpecialtyCategoryMapper.AnyOverlap(profile.SpecialtyCategories, SpecialtyCategoryMapper.MapToCategories(taxonomy.Desc)),
                LicenseStateMatch = string.IsNullOrWhiteSpace(profile.State) || string.IsNullOrWhiteSpace(Normalize(taxonomy?.State))
                    ? null
                    : string.Equals(profile.State, taxonomy!.State, StringComparison.OrdinalIgnoreCase),
                DepartmentMatch = DepartmentMatch(profile, taxonomy),
                OrcidMatch = !string.IsNullOrWhiteSpace(profile.Orcid)
                    && result.Identifiers?.Any(id =>
                        string.Equals(id.IdentifierType, OrcidIdentifierType, StringComparison.Ordinal)
                        && string.Equals(id.Identifier, profile.Orcid, StringComparison.OrdinalIgnoreCase)) == true,
                IsDeactivated = string.Equals(result.Status, "D", StringComparison.OrdinalIgnoreCase)
            };
        }

        /// <summary>
        /// Splits a parsed full name ("John A Smith") into first, middle, last.
        /// </summary>
        internal static (string First, string? Middle, string Last) ParseFullName(string fullName)
        {
            var tokens = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length == 0)
            {
                return (string.Empty, null, string.Empty);
            }

            if (tokens.Length == 1)
            {
                return (string.Empty, null, tokens[0]);
            }

            return tokens.Length == 2
                ? (tokens[0], null, tokens[^1])
                : (tokens[0], string.Join(' ', tokens[1..^1]), tokens[^1]);
        }

        private static bool? MiddleNameMatch(string? personMiddle, string? candidateMiddle)
        {
            if (string.IsNullOrWhiteSpace(personMiddle) || string.IsNullOrWhiteSpace(candidateMiddle))
            {
                return null;
            }

            var p = personMiddle.Trim();
            var c = candidateMiddle.Trim();
            if (string.Equals(p, c, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (p.Length == 1)
            {
                return c.StartsWith(p, StringComparison.OrdinalIgnoreCase);
            }

            return c.Length == 1 && p.StartsWith(c, StringComparison.OrdinalIgnoreCase);
        }

        private static bool? CredentialMatch(string? personSuffix, string? candidateCredential)
        {
            if (string.IsNullOrWhiteSpace(personSuffix) || string.IsNullOrWhiteSpace(candidateCredential))
            {
                return null;
            }

            var personTokens = personSuffix
                .Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeCredentialToken)
                .ToHashSet(StringComparer.Ordinal);

            return candidateCredential
                .Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeCredentialToken)
                .Any(personTokens.Contains);
        }

        private static string NormalizeCredentialToken(string token) =>
            new string(token.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        private static bool? OtherNameMatch(PersonSignalProfile profile, NpiBasic? basic)
        {
            if (basic?.OtherNames is not { Count: > 0 })
            {
                return null;
            }

            foreach (var otherName in basic.OtherNames)
            {
                if (!string.IsNullOrWhiteSpace(profile.LastName)
                    && string.Equals(Normalize(otherName.LastName), profile.LastName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(profile.InstitutionName)
                    && otherName.OrganizationName?.Contains(profile.InstitutionName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool? DepartmentMatch(PersonSignalProfile profile, NpiTaxonomy? taxonomy)
        {
            if (string.IsNullOrWhiteSpace(profile.Department) || string.IsNullOrWhiteSpace(taxonomy?.Desc))
            {
                return null;
            }

            var departmentCategories = SpecialtyCategoryMapper.MapToCategories(profile.Department);
            if (departmentCategories.Count == 0)
            {
                return null;
            }

            return SpecialtyCategoryMapper.AnyOverlap(departmentCategories, SpecialtyCategoryMapper.MapToCategories(taxonomy.Desc));
        }

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
