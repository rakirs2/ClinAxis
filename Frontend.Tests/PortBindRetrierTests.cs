using System.Net.Sockets;
using Frontend;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frontend.Tests;

[TestClass]
public sealed class PortBindRetrierTests
{
    [TestMethod]
    public void RetriesOnAddressAlreadyInUseUntilMaxRetriesExceeded()
    {
        var callCount = 0;
        Assert.ThrowsException<SocketException>(() =>
            PortBindRetrier.Run(() =>
            {
                callCount++;
                throw new SocketException((int)SocketError.AddressAlreadyInUse);
            }, maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(1)));

        Assert.AreEqual(3, callCount);
    }

    [TestMethod]
    public void DoesNotRetryOnNonAddressInUseException()
    {
        var callCount = 0;
        Assert.ThrowsException<InvalidOperationException>(() =>
            PortBindRetrier.Run(() =>
            {
                callCount++;
                throw new InvalidOperationException("unexpected error");
            }, maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(1)));

        Assert.AreEqual(1, callCount);
    }
}
