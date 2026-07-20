namespace NexIM.ServerTests.Xmpp;

[TestClass]
public sealed class MultiResourceTests : TestHelper
{
    [TestMethod]
    public void PresenceDeliveredToOwnResources()
    {
        var res2 = CreateSecondResourceClient();

        res2.Send("MultiResourcePresenceRes2Initial");
        res2.Receive("MultiResourcePresenceRes2Initial");

        Send("MultiResourcePresencePrimaryInitial");
        Receive("MultiResourcePresencePrimaryInitial");
        FinishReceive();

        res2.Receive("MultiResourcePresenceRes2Broadcast");
        res2.FinishReceive();
    }

    [TestMethod]
    public void MessageRoutingByPriority()
    {
        var res2 = CreateSecondResourceClient();
        var test2 = CreateSecondClient();

        res2.Send("MultiResourcePresenceRes2Priority");
        res2.Receive("MultiResourcePresenceRes2Priority");

        test2.Send("MultiResourcePriorityMessageSend");
        test2.Receive("MultiResourcePriorityMessageSend");
        test2.FinishReceive();

        res2.Receive("MultiResourcePriorityMessageReceive");
        res2.FinishReceive();

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
