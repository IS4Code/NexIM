namespace NexIM.ServerTests.Xmpp;

[TestClass]
public sealed class VCardTests : TestHelper
{
    [TestMethod]
    [DataRow("VCardSetGet")]
    [DataRow("VCardOutOfOrderAccepted")]
    [DataRow("VCardBirthdayTruncated")]
    [DataRow("VCardMultiplePhoto")]
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
