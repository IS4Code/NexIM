namespace NexIM.ServerTests.Xmpp;

[TestClass]
public sealed class ResourceBindingTests : TestHelper
{
    [TestMethod]
    public void ConflictingResourceRejected()
    {
        CreateConflictingResourceClient();

        // Primary client is unaffected and still functional
        Send("PresenceStatus");
        Receive("PresenceStatus");
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
