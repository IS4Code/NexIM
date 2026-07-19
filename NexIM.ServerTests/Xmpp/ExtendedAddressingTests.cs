namespace NexIM.ServerTests.Xmpp;

[TestClass]
public sealed class ExtendedAddressingTests : TestHelper
{
    [TestMethod]
    [DataRow("AddressesMulticastSingleRecipient")]
    [DataRow("AddressesMulticastWithDescriptions")]
    [DataRow("AddressesUnsupportedUri")]
    public void RoundTrip(string file)
    {
        Send(file);
        Receive(file);
        FinishReceive();
    }

    [TestMethod]
    public void MulticastToMultipleRecipients()
    {
        var test2 = CreateSecondClient();

        Send("AddressesMulticastPrimary");
        Receive("AddressesMulticastPrimary");
        FinishReceive();

        test2.Receive("AddressesMulticastSecondary");
        test2.FinishReceive();
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        ServerInitialize(testContext);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        ServerCleanup();
    }
}
