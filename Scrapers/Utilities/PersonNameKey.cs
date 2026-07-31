namespace Scrapers.Utilities
{
    internal static class PersonNameKey
    {
        public static (string? Prefix, string FullName, string Raw) Parse(string rawName)
        {
            var (prefix, fullName, _) = NameParser.Parse(rawName);
            return (prefix, fullName, rawName.Trim());
        }

        public static string[] LookupOrder(string fullName, string raw) =>
            fullName == raw ? [fullName] : [fullName, raw];

        public static string? MergePrefix(string? current, string? candidate) =>
            current == null && candidate != null ? candidate : current;
    }
}
