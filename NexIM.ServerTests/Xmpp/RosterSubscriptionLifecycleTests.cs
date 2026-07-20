namespace NexIM.ServerTests.Xmpp;

[TestClass]
public sealed class RosterSubscriptionLifecycleTests : TestHelper
{
    [TestMethod]
    public void SubscribeBothWays()
    {
        var test2 = CreateSecondClient();

        test2.Send("SubscriptionBothTest2Initial");
        test2.Receive("SubscriptionBothTest2Initial");

        Send("SubscriptionBothPrimaryInitial");
        Receive("SubscriptionBothPrimaryInitial");

        test2.Receive("SubscriptionBothTest2Request");

        test2.Send("SubscriptionBothTest2Approve");

        Receive("SubscriptionBothPrimaryApproved");
        FinishReceive();

        test2.FinishReceive();

        test2.Send("SubscriptionBothTest2Reciprocal");

        Receive("SubscriptionBothPrimaryRequest");

        Send("SubscriptionBothPrimaryApprove");

        test2.Receive("SubscriptionBothTest2Approved");
        test2.FinishReceive();

        FinishReceive();

        Send("SubscriptionBothPrimaryRosterCheck");
        Receive("SubscriptionBothPrimaryRosterCheck");
        FinishReceive();

        test2.Send("SubscriptionBothTest2RosterCheck");
        test2.Receive("SubscriptionBothTest2RosterCheck");
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
