namespace Scrapers.Utilities
{
    internal static class ConditionDecision
    {
        public static object MatcherUnavailable(string term) =>
            new { term, reason = "mesh_matcher_not_initialized" };

        public static object CuiNotFound(string term, string? meshCui, double similarity) =>
            new { term, reason = "cui_not_found", meshCui, similarity };

        public static object Rejected(string term, string reason, double similarity) =>
            new { term, reason, similarity };
    }
}
