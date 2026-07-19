namespace NexIM.ServerTests.Xmpp;

[TestClass]
public sealed class MessagingExtensionsTests : TestHelper
{
    [TestMethod]
    [DataRow("ChatStates")]
    [DataRow("MessageReceipts")]
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
