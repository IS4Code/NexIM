namespace NexIM.ServerTests.Xmpp;

[TestClass]
public sealed class ServiceDiscoveryTests : TestHelper
{
    [TestMethod]
    [DataRow("DiscoServer")]
    [DataRow("DiscoAccount")]
    [DataRow("DiscoFullJidUnsupported")]
    public void RoundTrip(string file)
    {
        Send(file);
        Receive(file);
        FinishReceive();
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
