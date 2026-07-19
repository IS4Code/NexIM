namespace NexIM.ServerTests.Xmpp;

[TestClass]
public sealed class XHtmlTests : TestHelper
{
    [TestMethod]
    [DataRow("XHtmlBasicFormatting")]
    [DataRow("XHtmlWhitespaceBetweenElements")]
    [DataRow("XHtmlStyles")]
    [DataRow("XHtmlLink")]
    [DataRow("XHtmlImage")]
    [DataRow("XHtmlLineBreak")]
    [DataRow("XHtmlHeadings")]
    [DataRow("XHtmlLists")]
    [DataRow("XHtmlInlineSemantics")]
    [DataRow("XHtmlQuotesAndCode")]
    [DataRow("XHtmlDivSpan")]
    [DataRow("XHtmlNonDefaultLanguage")]
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
