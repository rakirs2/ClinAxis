using System;
using System.Collections.Generic;

namespace Scrapers.Utilities
{
    /// <summary>
    /// Location free-text normalization (issue #380). Applied at ingest time in
    /// StudyRepository so study_locations stores canonical values: countries via
    /// alias map, US states as 2-letter codes, city/facility trimmed. The raw
    /// API free-text is intentionally replaced by the canonical form — this is
    /// the normalization the issue mandates, not data loss (see
    /// docs/scraper_architecture.md §7.8).
    /// </summary>
    internal static class LocationNormalizer
    {
        private static readonly Dictionary<string, string> CountryAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["USA"] = "United States",
            ["US"] = "United States",
            ["U.S."] = "United States",
            ["U.S.A"] = "United States",
            ["U.S.A."] = "United States",
            ["United States of America"] = "United States",
            ["America"] = "United States",
            ["UK"] = "United Kingdom",
            ["U.K."] = "United Kingdom",
            ["GB"] = "United Kingdom",
            ["Great Britain"] = "United Kingdom",
            ["Britain"] = "United Kingdom",
            ["The Netherlands"] = "Netherlands",
            ["Republic of Korea"] = "South Korea",
            ["Korea, Republic of"] = "South Korea",
        };

        private static readonly Dictionary<string, string> UsStateCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Alabama"] = "AL", ["Alaska"] = "AK", ["Arizona"] = "AZ", ["Arkansas"] = "AR",
            ["California"] = "CA", ["Colorado"] = "CO", ["Connecticut"] = "CT", ["Delaware"] = "DE",
            ["District of Columbia"] = "DC", ["Florida"] = "FL", ["Georgia"] = "GA", ["Hawaii"] = "HI",
            ["Idaho"] = "ID", ["Illinois"] = "IL", ["Indiana"] = "IN", ["Iowa"] = "IA",
            ["Kansas"] = "KS", ["Kentucky"] = "KY", ["Louisiana"] = "LA", ["Maine"] = "ME",
            ["Maryland"] = "MD", ["Massachusetts"] = "MA", ["Michigan"] = "MI", ["Minnesota"] = "MN",
            ["Mississippi"] = "MS", ["Missouri"] = "MO", ["Montana"] = "MT", ["Nebraska"] = "NE",
            ["Nevada"] = "NV", ["New Hampshire"] = "NH", ["New Jersey"] = "NJ", ["New Mexico"] = "NM",
            ["New York"] = "NY", ["North Carolina"] = "NC", ["North Dakota"] = "ND", ["Ohio"] = "OH",
            ["Oklahoma"] = "OK", ["Oregon"] = "OR", ["Pennsylvania"] = "PA", ["Rhode Island"] = "RI",
            ["South Carolina"] = "SC", ["South Dakota"] = "SD", ["Tennessee"] = "TN", ["Texas"] = "TX",
            ["Utah"] = "UT", ["Vermont"] = "VT", ["Virginia"] = "VA", ["Washington"] = "WA",
            ["West Virginia"] = "WV", ["Wisconsin"] = "WI", ["Wyoming"] = "WY",
        };

        public static string? NormalizeCountry(string? country)
        {
            if (string.IsNullOrWhiteSpace(country))
                return null;

            var collapsed = CollapseWhitespace(country.Trim());
            return CountryAliases.TryGetValue(collapsed, out var canonical) ? canonical : collapsed;
        }

        public static string? NormalizeState(string? state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return null;

            var collapsed = CollapseWhitespace(state.Trim());
            if (UsStateCodes.TryGetValue(collapsed, out var code))
                return code;

            // Already a 2-letter code (or anything 2-4 letters) — uppercase it.
            return collapsed.Length <= 4 ? collapsed.ToUpperInvariant() : collapsed;
        }

        public static string? NormalizeCity(string? city)
        {
            if (string.IsNullOrWhiteSpace(city))
                return null;

            return CollapseWhitespace(city.Trim());
        }

        public static string? NormalizeFacility(string? facility)
        {
            if (string.IsNullOrWhiteSpace(facility))
                return null;

            return CollapseWhitespace(facility.Trim());
        }

        private static string CollapseWhitespace(string value)
        {
            var chars = new char[value.Length];
            var write = 0;
            var lastWasSpace = false;
            foreach (var c in value)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace)
                        chars[write++] = ' ';
                    lastWasSpace = true;
                }
                else
                {
                    chars[write++] = c;
                    lastWasSpace = false;
                }
            }

            if (write > 0 && chars[write - 1] == ' ')
                write--;

            return new string(chars, 0, write);
        }
    }
}
