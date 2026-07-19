namespace NexIM.ServerTests.Xmpp;

[TestClass]
public sealed class PrivateStorageTests : TestHelper
{
    [TestMethod]
    [DataRow("PrivateStorageNotFound")]
    [DataRow("PrivateStorageSetGetOverwrite")]
    [DataRow("PrivateStorageArbitraryKeys")]
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
