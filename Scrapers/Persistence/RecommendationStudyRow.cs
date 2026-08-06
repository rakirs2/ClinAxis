namespace Scrapers.Persistence
{
    /// <summary>
    /// Flat (person, study) row returned by
    /// <see cref="StudyRepository.GetRecommendationStudyRowsAsync"/> for the P6 rec-engine
    /// assembler (issue #170): the study's signal inputs plus its condition names and
    /// location countries.
    /// </summary>
    public readonly record struct RecommendationStudyRow(
        Guid PersonId,
        string? OverallStatus,
        int? EnrollmentCount,
        DateOnly? StartDate,
        DateOnly? CompletionDate,
        IReadOnlyList<string> ConditionNames,
        IReadOnlyList<string> Countries);
}
