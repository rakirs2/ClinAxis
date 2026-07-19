using Microsoft.VisualStudio.TestTools.UnitTesting;
using Scrapers;

namespace Scrapers.Tests;

[TestClass]
public class ConnectionStringProviderTests
{
    [TestMethod]
    public void WithPoolLimits_SetsMaxPoolSize()
    {
        var result = ConnectionStringProvider.WithPoolLimits("Host=localhost;Database=test;Username=test", 15);
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(result);
        Assert.AreEqual(15, builder.MaxPoolSize);
    }

    [TestMethod]
    public void WithPoolLimits_SetsDefaultMaxPoolSize()
    {
        var result = ConnectionStringProvider.WithPoolLimits("Host=localhost;Database=test;Username=test");
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(result);
        Assert.AreEqual(25, builder.MaxPoolSize);
    }

    [TestMethod]
    public void WithPoolLimits_SetsConnectionIdleLifetime()
    {
        var result = ConnectionStringProvider.WithPoolLimits("Host=localhost;Database=test;Username=test");
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(result);
        Assert.AreEqual(300, builder.ConnectionIdleLifetime);
    }

    [TestMethod]
    public void WithPoolLimits_SetsConnectionPruningInterval()
    {
        var result = ConnectionStringProvider.WithPoolLimits("Host=localhost;Database=test;Username=test");
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(result);
        Assert.AreEqual(60, builder.ConnectionPruningInterval);
    }

    [TestMethod]
    public void WithPoolLimits_OverridesExistingMaxPoolSize()
    {
        var result = ConnectionStringProvider.WithPoolLimits(
            "Host=localhost;Database=test;Username=test;Maximum Pool Size=100", 30);
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(result);
        Assert.AreEqual(30, builder.MaxPoolSize);
    }

    [TestMethod]
    public void WithPoolLimits_PreservesAllOtherParameters()
    {
        const string input = "Host=db.example.com;Port=5433;Database=mydb;Username=admin;Password=secret;Search Path=public";
        var result = ConnectionStringProvider.WithPoolLimits(input);
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(result);
        Assert.AreEqual("db.example.com", builder.Host);
        Assert.AreEqual(5433, builder.Port);
        Assert.AreEqual("mydb", builder.Database);
        Assert.AreEqual("admin", builder.Username);
        Assert.AreEqual("secret", builder.Password);
        Assert.AreEqual("public", builder.SearchPath);
    }

    [TestMethod]
    public void WithPoolLimits_WithExplicitPoolingFalse_DisablesPooling()
    {
        var result = ConnectionStringProvider.WithPoolLimits(
            "Host=localhost;Database=test;Username=test;Pooling=false");
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(result);
        Assert.IsFalse(builder.Pooling);
    }
}
