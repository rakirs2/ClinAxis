using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Scrapers.Utilities;

namespace Scrapers.Tests;

[TestClass]
public sealed class NpiCollisionDetectorTests
{
    [TestMethod]
    public void IsNpiUniqueViolation_NpiIndex23505_ReturnsTrue()
    {
        var ex = UniqueViolation(constraintName: "IX_investigator_persons_npi");

        Assert.IsTrue(NpiCollisionDetector.IsNpiUniqueViolation(ex));
    }

    [TestMethod]
    public void IsNpiUniqueViolation_NullConstraintName_ReturnsTrue()
    {
        var ex = UniqueViolation(constraintName: null);

        Assert.IsTrue(NpiCollisionDetector.IsNpiUniqueViolation(ex));
    }

    [TestMethod]
    public void IsNpiUniqueViolation_NestedPostgresInner_ReturnsTrue()
    {
        var pg = new PostgresException(
            "duplicate key value violates unique constraint", "ERROR", "ERROR", "23505",
            detail: "Key (npi)=(1234567890) already exists.",
            constraintName: "IX_investigator_persons_npi");
        var ex = new DbUpdateException("save failed", new InvalidOperationException("wrap", pg));

        Assert.IsTrue(NpiCollisionDetector.IsNpiUniqueViolation(ex));
    }

    [TestMethod]
    public void IsNpiUniqueViolation_OtherConstraint23505_ReturnsFalse()
    {
        var ex = UniqueViolation(constraintName: "IX_person_identifier_candidates_person_id");

        Assert.IsFalse(NpiCollisionDetector.IsNpiUniqueViolation(ex));
    }

    [TestMethod]
    public void IsNpiUniqueViolation_OtherSqlState_ReturnsFalse()
    {
        var ex = new DbUpdateException(
            "save failed",
            new PostgresException(
                "violates foreign key constraint", "ERROR", "ERROR", "23503",
                detail: "detail",
                constraintName: "fk_studies_conditions"));

        Assert.IsFalse(NpiCollisionDetector.IsNpiUniqueViolation(ex));
    }

    [TestMethod]
    public void IsNpiUniqueViolation_NonPostgresInner_ReturnsFalse()
    {
        var ex = new DbUpdateException("save failed", new InvalidOperationException("boom"));

        Assert.IsFalse(NpiCollisionDetector.IsNpiUniqueViolation(ex));
    }

    [TestMethod]
    public void IsNpiUniqueViolation_NoInner_ReturnsFalse()
    {
        Assert.IsFalse(NpiCollisionDetector.IsNpiUniqueViolation(new DbUpdateException("save failed")));
    }

    private static DbUpdateException UniqueViolation(string? constraintName) =>
        new(
            "save failed",
            new PostgresException(
                "duplicate key value violates unique constraint", "ERROR", "ERROR", "23505",
                detail: "Key (npi)=(1234567890) already exists.",
                constraintName: constraintName));
}
