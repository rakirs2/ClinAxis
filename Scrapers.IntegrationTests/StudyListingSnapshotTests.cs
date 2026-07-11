using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence;
using Scrapers.Persistence.Entities;
using Scrapers.Testing;

namespace Scrapers.IntegrationTests;

[TestClass]
public sealed class StudyListingSnapshotTests
{
    [TestMethod]
    public async Task SnapshotData_HasFiveStudies()
    {
        await using SnapshotDb snapshot = new();
        var repo = new StudyRepository(snapshot.ConnectionString);
        Assert.AreEqual(5, await repo.CountStudiesAsync());
    }

    [TestMethod]
    public async Task SearchByTitle_FindsMatchingStudy()
    {
        await using SnapshotDb snapshot = new();
        var repo = new StudyRepository(snapshot.ConnectionString);

        IReadOnlyList<StudyEntity> results = await repo.GetStudiesPagedAsync(1, 10, search: "Pregabalin");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("NCT00000002", results[0].NctId);
    }

    [TestMethod]
    public async Task FilterByStatus_ReturnsCorrectCount()
    {
        await using SnapshotDb snapshot = new();
        var repo = new StudyRepository(snapshot.ConnectionString);

        IReadOnlyList<StudyEntity> recruiting = await repo.GetStudiesPagedAsync(1, 10, status: "RECRUITING");

        Assert.AreEqual(2, recruiting.Count);
    }

    [TestMethod]
    public async Task Pagination_ReturnsCorrectPage()
    {
        await using SnapshotDb snapshot = new();
        var repo = new StudyRepository(snapshot.ConnectionString);

        IReadOnlyList<StudyEntity> page1 = await repo.GetStudiesPagedAsync(1, 2);

        Assert.AreEqual(2, page1.Count);
    }

    [TestMethod]
    public async Task SearchWithNoMatches_ReturnsEmpty()
    {
        await using SnapshotDb snapshot = new();
        var repo = new StudyRepository(snapshot.ConnectionString);

        IReadOnlyList<StudyEntity> results = await repo.GetStudiesPagedAsync(1, 10, search: "zzzznotfound");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task Investigators_ListAll()
    {
        await using SnapshotDb snapshot = new();
        var repo = new StudyRepository(snapshot.ConnectionString);

        IReadOnlyList<InvestigatorSummary> results = await repo.GetInvestigatorsPagedAsync(1, 10);

        Assert.AreEqual(4, results.Count);
    }

    [TestMethod]
    public async Task Investigators_SearchByName()
    {
        await using SnapshotDb snapshot = new();
        var repo = new StudyRepository(snapshot.ConnectionString);

        IReadOnlyList<InvestigatorSummary> results = await repo.GetInvestigatorsPagedAsync(1, 10, search: "Bob");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Dr. Bob Williams", results[0].Name);
    }

    [TestMethod]
    public async Task Investigators_SearchWithNoMatch_ReturnsEmpty()
    {
        await using SnapshotDb snapshot = new();
        var repo = new StudyRepository(snapshot.ConnectionString);

        IReadOnlyList<InvestigatorSummary> results = await repo.GetInvestigatorsPagedAsync(1, 10, search: "zzzzz");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task Investigators_CountFiltered()
    {
        await using SnapshotDb snapshot = new();
        var repo = new StudyRepository(snapshot.ConnectionString);

        Assert.AreEqual(4, await repo.CountInvestigatorsFilteredAsync());

        Assert.AreEqual(1, await repo.CountInvestigatorsFilteredAsync(search: "Carol"));
        Assert.AreEqual(2, await repo.CountInvestigatorsFilteredAsync(search: "Alice"));
    }
}
