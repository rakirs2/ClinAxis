using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;

namespace Scrapers.Services
{
    public sealed class LocationMeshMatcher
    {
        private readonly Dictionary<string, int> _exactNameLookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _validCountries = new(StringComparer.OrdinalIgnoreCase);

        public LocationMeshMatcher(string connectionString)
        {
            using var ctx = CreateContext(connectionString);
            var geoDescriptors = ctx.MeshDescriptors
                .Where(m => m.TreeNumbers.Any(tn => tn.StartsWith('Z')))
                .AsNoTracking()
                .ToList();

            foreach (var d in geoDescriptors)
            {
                _exactNameLookup[d.Name] = d.Id;
                _validCountries.Add(d.Name);
            }
        }

        public int? Match(string? country, string? state)
        {
            if (string.IsNullOrWhiteSpace(country))
                return null;

            var countryKey = country.Trim();
            if (_exactNameLookup.TryGetValue(countryKey, out var id))
                return id;

            if (!string.IsNullOrWhiteSpace(state))
            {
                var stateKey = state.Trim();
                if (_exactNameLookup.TryGetValue(stateKey, out var sid))
                    return sid;
            }

            foreach (var kvp in _exactNameLookup)
            {
                if (kvp.Key.Contains(countryKey, StringComparison.OrdinalIgnoreCase) ||
                    countryKey.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            return null;
        }

        private static ClinicalTrialsContext CreateContext(string connectionString)
        {
            var builder = new DbContextOptionsBuilder<ClinicalTrialsContext>();
            builder.ConfigureNpgsql(connectionString);
            return new ClinicalTrialsContext(builder.Options);
        }
    }
}