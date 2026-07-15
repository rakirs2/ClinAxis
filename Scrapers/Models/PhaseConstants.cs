namespace Scrapers.Models;

public static class PhaseConstants
{
    public const string EarlyPhase1 = "EARLY_PHASE1";
    public const string Phase1 = "PHASE1";
    public const string Phase2 = "PHASE2";
    public const string Phase3 = "PHASE3";
    public const string Phase4 = "PHASE4";
    public const string NotApplicable = "NA";

    public static readonly IReadOnlyList<string> All = [EarlyPhase1, Phase1, Phase2, Phase3, Phase4, NotApplicable];
}
