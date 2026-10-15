namespace NexIM.ServerTests.Xmpp;

[TestClass]
public sealed class RosterSubscriptionLifecycleTests : TestHelper
{
    [TestMethod]
    public void SubscribeBothWays()
    {
        // Use dedicated accounts not to affect other tests' subscription states
        var primary = CreateClient("PrologueSubscriber1", "PrologueSubscriber1Bind");
        var test2 = CreateClient("PrologueSubscriber2", "PrologueSubscriber2Bind");

        test2.Send("SubscriptionBothTest2Initial");
        test2.Receive("SubscriptionBothTest2Initial");

        primary.Send("SubscriptionBothPrimaryInitial");
        primary.Receive("SubscriptionBothPrimaryInitial");

        test2.Receive("SubscriptionBothTest2Request");

        test2.Send("SubscriptionBothTest2Approve");

        primary.Receive("SubscriptionBothPrimaryApproved");
        primary.FinishReceive();

        test2.FinishReceive();

        test2.Send("SubscriptionBothTest2Reciprocal");

        primary.Receive("SubscriptionBothPrimaryRequest");

        primary.Send("SubscriptionBothPrimaryApprove");

        test2.Receive("SubscriptionBothTest2Approved");
        test2.FinishReceive();

        primary.FinishReceive();

        primary.Send("SubscriptionBothPrimaryRosterCheck");
        primary.Receive("SubscriptionBothPrimaryRosterCheck");
        primary.FinishReceive();

        test2.Send("SubscriptionBothTest2RosterCheck");
        test2.Receive("SubscriptionBothTest2RosterCheck");
        test2.FinishReceive();
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        ServerInitialize(testContext);
        RegisterAccount(receiver.Server, "subscriber1", "subscriber1", "subscriber1@example.org").AsTask().GetAwaiter().GetResult();
        RegisterAccount(receiver.Server, "subscriber2", "subscriber2", "subscriber2@example.org").AsTask().GetAwaiter().GetResult();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        ServerCleanup();
    }
}
