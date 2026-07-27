namespace DataApi.Services;

internal static class InvestigatorScorer
{
    public static double ComputeRelevance(int studyCount)
    {
        return Math.Min(1.0, studyCount / 20.0);
    }

    public static double ComputeExperience(int studyCount, int completedStudies, int? enrollmentTotal)
    {
        double completionRate = studyCount > 0 ? (double)completedStudies / studyCount : 0;
        double enrollmentScore = enrollmentTotal.HasValue ? Math.Min(1.0, enrollmentTotal.Value / 10000.0) : 0;
        double countScore = Math.Min(1.0, studyCount / 50.0);
        return 0.4 * completionRate + 0.3 * countScore + 0.3 * enrollmentScore;
    }

    public static double ComputePublication(int? hIndex, int? paperCount)
    {
        double hScore = hIndex.HasValue ? Math.Min(1.0, hIndex.Value / 100.0) : 0;
        double paperScore = paperCount.HasValue ? Math.Min(1.0, paperCount.Value / 200.0) : 0;
        return 0.6 * hScore + 0.4 * paperScore;
    }

    public static double ComputeNetwork(int coInvestigatorCount)
    {
        return Math.Min(1.0, coInvestigatorCount / 50.0);
    }

    public static double ComputeTotal(double relevance, double experience, double publication, double network)
    {
        return 0.40 * relevance + 0.25 * experience + 0.20 * publication + 0.15 * network;
    }
}
