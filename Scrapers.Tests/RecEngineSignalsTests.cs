using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class RecEngineSignalsTests
{
    private static readonly DateOnly AsOf = new(2026, 8, 6);

    // ---- Completion signal ----

    [TestMethod]
    public void CompletionSignal_MapsStatusesToExpectedValues()
    {
        Assert.AreEqual(1.0, RecEngineSignals.CompletionSignalFromStatus("COMPLETED"));
        Assert.AreEqual(0.5, RecEngineSignals.CompletionSignalFromStatus("ACTIVE_NOT_RECRUITING"));
        Assert.AreEqual(0.3, RecEngineSignals.CompletionSignalFromStatus("SUSPENDED"));
        Assert.AreEqual(0.2, RecEngineSignals.CompletionSignalFromStatus("ENROLLING_BY_INVITATION"));
        Assert.AreEqual(0.2, RecEngineSignals.CompletionSignalFromStatus("RECRUITING"));
        Assert.AreEqual(0.2, RecEngineSignals.CompletionSignalFromStatus("ACTIVE"));
        Assert.AreEqual(0.0, RecEngineSignals.CompletionSignalFromStatus("NOT_YET_RECRUITING"));
        Assert.AreEqual(0.0, RecEngineSignals.CompletionSignalFromStatus("TERMINATED"));
        Assert.AreEqual(0.0, RecEngineSignals.CompletionSignalFromStatus("WITHDRAWN"));
    }

    [TestMethod]
    public void CompletionSignal_UnknownOrMissingStatus_Abstains()
    {
        Assert.IsNull(RecEngineSignals.CompletionSignalFromStatus("UNKNOWN"));
        Assert.IsNull(RecEngineSignals.CompletionSignalFromStatus(null));
        Assert.IsNull(RecEngineSignals.CompletionSignalFromStatus(""));
        Assert.IsNull(RecEngineSignals.CompletionSignalFromStatus("SOME_NEW_STATUS"));
    }

    // ---- Enrollment velocity ----

    [TestMethod]
    public void Velocity_CompletedStudy_UsesCompletionDate()
    {
        var velocity = RecEngineSignals.EnrollmentVelocity(120, new DateOnly(2020, 1, 1), new DateOnly(2022, 1, 1), AsOf);

        Assert.IsNotNull(velocity);
        Assert.AreEqual(5.0, velocity.Value, 0.01);
    }

    [TestMethod]
    public void Velocity_OngoingStudy_UsesAsOfDate()
    {
        var velocity = RecEngineSignals.EnrollmentVelocity(60, new DateOnly(2026, 1, 1), null, AsOf);

        Assert.IsNotNull(velocity);
        Assert.AreEqual(8.42, velocity.Value, 0.01);
    }

    [TestMethod]
    public void Velocity_MissingEnrollment_Abstains()
    {
        Assert.IsNull(RecEngineSignals.EnrollmentVelocity(null, new DateOnly(2020, 1, 1), null, AsOf));
    }

    [TestMethod]
    public void Velocity_ZeroEnrollment_Abstains()
    {
        Assert.IsNull(RecEngineSignals.EnrollmentVelocity(0, new DateOnly(2020, 1, 1), null, AsOf));
    }

    [TestMethod]
    public void Velocity_MissingStartDate_Abstains()
    {
        Assert.IsNull(RecEngineSignals.EnrollmentVelocity(100, null, null, AsOf));
    }

    [TestMethod]
    public void Velocity_StartAfterEnd_Abstains()
    {
        Assert.IsNull(RecEngineSignals.EnrollmentVelocity(100, new DateOnly(2026, 8, 1), new DateOnly(2026, 1, 1), AsOf));
    }

    // ---- Aggregation ----

    [TestMethod]
    public void Aggregate_EmptyInput_ReturnsNullsAndZeroCount()
    {
        var result = RecEngineSignals.Aggregate([], AsOf);

        Assert.IsNull(result.CompletionRate);
        Assert.IsNull(result.EnrollmentVelocity);
        Assert.AreEqual(0, result.IncludedStudyCount);
    }

    [TestMethod]
    public void Aggregate_AllAbstainingStudies_ReturnsNulls()
    {
        var studies = new[]
        {
            new RecEngineSignals.StudySignalInput("UNKNOWN", null, null, null),
            new RecEngineSignals.StudySignalInput(null, 100, null, null)
        };

        var result = RecEngineSignals.Aggregate(studies, AsOf);

        Assert.IsNull(result.CompletionRate);
        Assert.IsNull(result.EnrollmentVelocity);
        Assert.AreEqual(0, result.IncludedStudyCount);
    }

    [TestMethod]
    public void Aggregate_SingleCompletedStudy_ReflectsItsSignal()
    {
        var studies = new[]
        {
            new RecEngineSignals.StudySignalInput("COMPLETED", 100, new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1))
        };

        var result = RecEngineSignals.Aggregate(studies, AsOf);

        Assert.AreEqual(1.0, result.CompletionRate!.Value, 0.0001);
        Assert.IsNotNull(result.EnrollmentVelocity);
        Assert.AreEqual(1, result.IncludedStudyCount);
    }

    [TestMethod]
    public void Aggregate_MixedStatuses_WeightedMeanBetweenBounds()
    {
        var studies = new[]
        {
            new RecEngineSignals.StudySignalInput("COMPLETED", 100, new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1)),
            new RecEngineSignals.StudySignalInput("RECRUITING", 100, new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1))
        };

        var result = RecEngineSignals.Aggregate(studies, AsOf);

        Assert.IsTrue(result.CompletionRate!.Value > 0.2 && result.CompletionRate.Value < 1.0, "Weighted mean must lie between the two signals");
        Assert.IsTrue(Math.Abs(result.CompletionRate.Value - 0.6) < 0.01, "Equal weights must average the two signals");
    }

    [TestMethod]
    public void Aggregate_AbstainingStudy_DoesNotPullMeanTowardZero()
    {
        var studies = new[]
        {
            new RecEngineSignals.StudySignalInput("COMPLETED", 100, new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1)),
            new RecEngineSignals.StudySignalInput("UNKNOWN", 100, new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1))
        };

        var result = RecEngineSignals.Aggregate(studies, AsOf);

        Assert.AreEqual(1.0, result.CompletionRate!.Value, 0.0001, "Unknown-status study must abstain, not drag the mean down");
    }

    [TestMethod]
    public void Aggregate_BiggerRecentStudy_DominatesWeights()
    {
        var smallOld = new RecEngineSignals.StudySignalInput("RECRUITING", 10, new DateOnly(2010, 1, 1), new DateOnly(2011, 1, 1));
        var bigRecent = new RecEngineSignals.StudySignalInput("COMPLETED", 1000, new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1));

        var result = RecEngineSignals.Aggregate([smallOld, bigRecent], AsOf);

        Assert.IsTrue(result.CompletionRate!.Value > 0.9, "Recent 1000-enrollment COMPLETED study must dominate the old small RECRUITING study");
    }

    [TestMethod]
    public void Aggregate_VelocityIsWeightedMeanOfPerStudyVelocities()
    {
        var studies = new[]
        {
            new RecEngineSignals.StudySignalInput("COMPLETED", 120, new DateOnly(2020, 1, 1), new DateOnly(2022, 1, 1)),
            new RecEngineSignals.StudySignalInput("COMPLETED", 120, new DateOnly(2020, 1, 1), new DateOnly(2022, 1, 1))
        };

        var result = RecEngineSignals.Aggregate(studies, AsOf);

        Assert.IsNotNull(result.EnrollmentVelocity);
        Assert.AreEqual(5.0, result.EnrollmentVelocity!.Value, 0.01, "Both studies have velocity 5/month -> weighted mean is 5");
    }
}
