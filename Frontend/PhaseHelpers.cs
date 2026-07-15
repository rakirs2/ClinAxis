namespace Frontend;

internal static class PhaseHelpers
{
    private static readonly Dictionary<string, string> DisplayNames = new()
    {
        ["EARLY_PHASE1"] = "Early Phase 1",
        ["PHASE1"] = "Phase 1",
        ["PHASE2"] = "Phase 2",
        ["PHASE3"] = "Phase 3",
        ["PHASE4"] = "Phase 4",
        ["NA"] = "Not Applicable"
    };

    public static string DisplayName(string phase) =>
        DisplayNames.TryGetValue(phase, out var name) ? name : phase;

    public static string DisplayPhases(IEnumerable<string>? phases) =>
        phases is not null ? string.Join(", ", phases.Select(DisplayName)) : "None";
}
