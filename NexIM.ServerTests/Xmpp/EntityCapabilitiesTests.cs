namespace NexIM.ServerTests.Xmpp;

[TestClass]
public sealed class EntityCapabilitiesTests : TestHelper
{
    [TestMethod]
    [DataRow("EntityCapabilitiesDeclare")]
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
