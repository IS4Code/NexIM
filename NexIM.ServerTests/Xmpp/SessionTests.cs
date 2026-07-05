namespace NexIM.ServerTests.Xmpp;

[TestClass]
public sealed class SessionTests : TestHelper
{
    [TestMethod]
    [DataRow("SessionStartNormal")]
    public void SessionStart(string file)
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
