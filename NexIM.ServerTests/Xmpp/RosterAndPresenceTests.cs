namespace NexIM.ServerTests.Xmpp;

[TestClass]
public sealed class RosterAndPresenceTests : TestHelper
{
    [TestMethod]
    [DataRow("RosterAddGetRemove")]
    [DataRow("RosterSubscribeUnsubscribe")]
    [DataRow("RosterPreApproval")]
    [DataRow("PresenceStatus")]
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
