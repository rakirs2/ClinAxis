using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Persistence.Entities;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class RejectedNameSyncTests
{
    private static RejectedInvestigatorNameEntity Entry(string name, bool? isHumanOverride = null) => new()
    {
        Id = Guid.NewGuid(),
        FullName = name,
        OccurrenceCount = 1,
        StudyCount = 1,
        RejectionReason = "CorporateSuffix:CORP",
        IsHumanOverride = isHumanOverride
    };

    private static RejectedNameStats Stats(string reason = "PharmaBlocklist:PFIZER", int occurrences = 2, int studies = 3)
        => new(reason, occurrences, studies);

    [TestMethod]
    public void Plan_AddsNewlyFailingName()
    {
        var plan = RejectedNameSync.Plan(
            existing: [Entry("Pfizer")],
            currentlyFailing: new Dictionary<string, RejectedNameStats>
            {
                ["Pfizer"] = Stats(),
                ["NewCorp"] = Stats(occurrences: 2, studies: 3)
            },
            overriddenNames: []);

        Assert.AreEqual(1, plan.ToAdd.Count);
        Assert.AreEqual("NewCorp", plan.ToAdd[0].FullName);
        Assert.AreEqual(2, plan.ToAdd[0].OccurrenceCount);
        Assert.AreEqual(3, plan.ToAdd[0].StudyCount);
        Assert.AreEqual(0, plan.ToRemove.Count);
        Assert.AreEqual(1, plan.ToUpdate.Count);
    }

    [TestMethod]
    public void Plan_UpdatesExistingFailingNameInPlace()
    {
        var existing = Entry("Pfizer");
        var plan = RejectedNameSync.Plan(
            existing: [existing],
            currentlyFailing: new Dictionary<string, RejectedNameStats>
            {
                ["Pfizer"] = Stats(reason: "PharmaBlocklist:PFIZER", occurrences: 5, studies: 7)
            },
            overriddenNames: []);

        Assert.AreEqual(1, plan.ToUpdate.Count);
        Assert.AreSame(existing, plan.ToUpdate[0]);
        Assert.AreEqual(5, existing.OccurrenceCount);
        Assert.AreEqual(7, existing.StudyCount);
        Assert.AreEqual("PharmaBlocklist:PFIZER", existing.RejectionReason);
        Assert.AreEqual(0, plan.ToAdd.Count);
        Assert.AreEqual(0, plan.ToRemove.Count);
    }

    [TestMethod]
    public void Plan_MatchesNamesCaseInsensitively()
    {
        var existing = Entry("pfizer");
        var plan = RejectedNameSync.Plan(
            existing: [existing],
            currentlyFailing: new Dictionary<string, RejectedNameStats>
            {
                ["PFIZER"] = Stats()
            },
            overriddenNames: []);

        Assert.AreEqual(1, plan.ToUpdate.Count);
        Assert.AreSame(existing, plan.ToUpdate[0]);
        Assert.AreEqual(0, plan.ToAdd.Count);
    }

    [TestMethod]
    public void Plan_RemovesEntryThatNoLongerFails()
    {
        var stale = Entry("Old Corp");
        var plan = RejectedNameSync.Plan(
            existing: [stale, Entry("Pfizer")],
            currentlyFailing: new Dictionary<string, RejectedNameStats>
            {
                ["Pfizer"] = Stats()
            },
            overriddenNames: []);

        Assert.AreEqual(1, plan.ToRemove.Count);
        Assert.AreSame(stale, plan.ToRemove[0]);
    }

    [TestMethod]
    public void Plan_SkipsOverriddenNameThatFailsTheFilter()
    {
        var plan = RejectedNameSync.Plan(
            existing: [],
            currentlyFailing: new Dictionary<string, RejectedNameStats>
            {
                ["Pfizer"] = Stats()
            },
            overriddenNames: ["Pfizer"]);

        Assert.AreEqual(0, plan.ToAdd.Count);
        Assert.AreEqual(0, plan.ToUpdate.Count);
        Assert.AreEqual(0, plan.ToRemove.Count);
    }

    [TestMethod]
    public void Plan_PreservesOverriddenEntryThatNoLongerFails()
    {
        var overridden = Entry("Pfizer", isHumanOverride: true);
        var stale = Entry("Stale Corp");
        var plan = RejectedNameSync.Plan(
            existing: [overridden, stale],
            currentlyFailing: new Dictionary<string, RejectedNameStats>(),
            overriddenNames: ["Pfizer"]);

        Assert.AreEqual(1, plan.ToRemove.Count);
        Assert.AreSame(stale, plan.ToRemove[0]);
        Assert.IsFalse(plan.ToRemove.Contains(overridden), "Overridden entries must never be purged");
        Assert.AreEqual(0, plan.ToAdd.Count);
    }

    [TestMethod]
    public void Plan_OverriddenNameIsNeverReaddedEvenWhenExistingEntryUnflagged()
    {
        var plan = RejectedNameSync.Plan(
            existing: [Entry("Pfizer")],
            currentlyFailing: new Dictionary<string, RejectedNameStats>
            {
                ["Pfizer"] = Stats()
            },
            overriddenNames: ["pfizer"]);

        Assert.AreEqual(0, plan.ToUpdate.Count, "Overridden name must not be re-scored even if its row predates the override");
        Assert.AreEqual(0, plan.ToAdd.Count);
    }
}
