using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class RecCandidateAssemblerTests
{
    private static readonly DateOnly AsOf = new(2026, 8, 6);
    private static readonly Guid Person1 = Guid.NewGuid();
    private static readonly Guid Person2 = Guid.NewGuid();

    private static RecCandidateAssembler.CandidateScalars Scalars(
        Guid uuid, string? name = null, int studyCount = 0, int? hIndex = null, int paperCount = 0)
    {
        return new RecCandidateAssembler.CandidateScalars(uuid, name, "Mayo Clinic", studyCount, hIndex, paperCount);
    }

    private static Scrapers.Persistence.RecommendationStudyRow Row(
        Guid personId,
        string? status = null,
        int? enrollment = null,
        DateOnly? start = null,
        DateOnly? completion = null,
        string[]? conditions = null,
        string[]? countries = null)
    {
        return new Scrapers.Persistence.RecommendationStudyRow(personId, status, enrollment, start, completion,
            conditions ?? [], countries ?? []);
    }

    [TestMethod]
    public void Assemble_AggregatesCompletionAndVelocityPerPerson()
    {
        var candidates = new[] { Scalars(Person1, "A", studyCount: 2) };
        var rows = new[]
        {
            Row(Person1, "COMPLETED", 120, new DateOnly(2020, 1, 1), new DateOnly(2022, 1, 1)),
            Row(Person1, "RECRUITING", 60, new DateOnly(2020, 1, 1), new DateOnly(2022, 1, 1))
        };

        var result = RecCandidateAssembler.Assemble(candidates, rows, AsOf);

        var person = result.Single();
        Assert.AreEqual(Person1, person.Uuid);
        Assert.AreEqual(2, person.StudyCount);
        Assert.IsNotNull(person.CompletionRate);
        Assert.IsTrue(person.CompletionRate.Value > 0.2 && person.CompletionRate.Value < 1.0);
        Assert.IsNotNull(person.EnrollmentVelocity);
    }

    [TestMethod]
    public void Assemble_NoStudyRows_LeavesSignalsNull()
    {
        var candidates = new[] { Scalars(Person1, "A", studyCount: 1, hIndex: 30, paperCount: 40) };

        var result = RecCandidateAssembler.Assemble(candidates, [], AsOf);

        var person = result.Single();
        Assert.IsNull(person.CompletionRate);
        Assert.IsNull(person.EnrollmentVelocity);
        Assert.AreEqual(30, person.HIndex);
        Assert.AreEqual(40, person.PaperCount);
    }

    [TestMethod]
    public void Assemble_MapsConditionNamesToCategories()
    {
        var candidates = new[] { Scalars(Person1, "A") };
        var rows = new[]
        {
            Row(Person1, "COMPLETED", conditions: ["Lung Neoplasms"]),
            Row(Person1, "COMPLETED", conditions: ["Heart Failure"])
        };

        var result = RecCandidateAssembler.Assemble(candidates, rows, AsOf);

        var categories = result.Single().ConditionCategories;
        CollectionAssert.Contains(categories.ToList(), "oncology");
        CollectionAssert.Contains(categories.ToList(), "cardiology");
    }

    [TestMethod]
    public void Assemble_RegionsAreDistinctAcrossStudies()
    {
        var candidates = new[] { Scalars(Person1, "A") };
        var rows = new[]
        {
            Row(Person1, "COMPLETED", countries: ["United States", "Canada"]),
            Row(Person1, "COMPLETED", countries: ["United States"])
        };

        var result = RecCandidateAssembler.Assemble(candidates, rows, AsOf);

        CollectionAssert.AreEqual(new[] { "Canada", "United States" }, result.Single().Regions.OrderBy(r => r).ToList());
    }

    [TestMethod]
    public void Assemble_UnknownPersonRowsAreIgnored()
    {
        var candidates = new[] { Scalars(Person1, "A") };
        var rows = new[] { Row(Person2, "COMPLETED", enrollment: 50) };

        var result = RecCandidateAssembler.Assemble(candidates, rows, AsOf);

        var person = result.Single();
        Assert.AreEqual(Person1, person.Uuid);
        Assert.IsNull(person.CompletionRate);
    }

    [TestMethod]
    public void Assemble_EmptyInput_ReturnsEmpty()
    {
        var result = RecCandidateAssembler.Assemble([], [], AsOf);

        Assert.AreEqual(0, result.Count);
    }
}
