using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Server;
using NexIM.Tools;
using NexIM.Xmpp.Server;
using NexIM.Xmpp.Server.Communication;

[assembly: DoNotParallelize]

namespace NexIM.ServerTests.Xmpp;

public abstract class TestHelper
{
    static readonly XmppServerReceiver receiver = new();

    static readonly XmlReaderSettings xmppReaderSettings = new() {
        Async = true,
        CheckCharacters = false,
        CloseInput = false,
        ConformanceLevel = ConformanceLevel.Fragment,
        DtdProcessing = DtdProcessing.Ignore,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
        NameTable = NexIM.Xmpp.Protocol.Grammar.Vocabulary.Instance.CreateNameTable<XmlWeakNameTable>(),
        ValidationType = ValidationType.None,
        XmlResolver = XmlResolver.ThrowingResolver
    };
    static readonly XmlWriterSettings xmppWriterSettings = new() {
        Async = true,
        CheckCharacters = false,
        CloseOutput = false,
        ConformanceLevel = ConformanceLevel.Fragment,
        Encoding = new UTF8Encoding(false),
        NamespaceHandling = NamespaceHandling.OmitDuplicates,
        NewLineChars = "\n",
        NewLineHandling = NewLineHandling.Entitize,
        OmitXmlDeclaration = true,
        NewLineOnAttributes = false,
        WriteEndDocumentOnClose = false
    };

    static readonly XmlReaderSettings testReaderSettings = new() {
        Async = false,
        CheckCharacters = false,
        CloseInput = false,
        ConformanceLevel = ConformanceLevel.Fragment,
        DtdProcessing = DtdProcessing.Ignore,
        IgnoreComments = true,
        IgnoreWhitespace = true,
        ValidationType = ValidationType.None,
        XmlResolver = XmlResolver.ThrowingResolver
    };

    static string dbPath = null!;

    static int running = 0;

    protected static void ServerInitialize(TestContext testContext)
    {
        if(Interlocked.Increment(ref running) != 1)
        {
            // Already initialized
            return;
        }

        // Prepare the server
        dbPath = Path.GetTempFileName();

        var server = receiver.Server = new NexServer(new NexDatabase.Sqlite {
            ConnectionString = $"Data Source=\"{dbPath}\""
        });
        using var password = new TemporaryString();
        password.Append("test");
        server.Register(new AccountName("test", "localhost"), password, new("test@example.org"), new()).AsTask().GetAwaiter().GetResult();
    }

    protected static void ServerCleanup()
    {
        if(Interlocked.Decrement(ref running) != 0)
        {
            // Still in use
            return;
        }

        receiver.Server.Close();
        try
        {
            File.Delete(dbPath);
        }
        catch
        {

        }
    }

    XmppManualSession session = null!;
    Pipe clientToServerPipe = null!;
    Pipe serverToClientPipe = null!;
    CancellationTokenSource testCts = null!;
    Task sessionTask = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        clientToServerPipe = new();
        serverToClientPipe = new();
        testCts = new();

        // Provide auth message
        Send("Prologue1");

        // Create session over the two channels
        session = new(
            new BidirectionalStream(
                clientToServerPipe.Reader.AsStream(),
                serverToClientPipe.Writer.AsStream()
            ),
            receiver,
            xmppReaderSettings,
            xmppWriterSettings
        );

        // Connect to receiver
        var handler = receiver.Connected(session).AsTask().GetAwaiter().GetResult();

        // Start session
        sessionTask = session.Run(handler, testCts.Token).AsTask();

        // Check auth response
        Receive("Prologue1");

        // Bind
        Send("Prologue2");

        // Check bound
        Receive("Prologue2");

        // Ignore anything else
        FlushReceive();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        try
        {
            testCts.Cancel();
            clientToServerPipe.Writer.Complete();
            serverToClientPipe.Reader.Complete();

            // Stopped
            sessionTask.GetAwaiter().GetResult();
        }
        finally
        {
            // Wait until done
            session.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static Stream LoadResourceStream(string name)
    {
        return
            typeof(TestHelper).Assembly.GetManifestResourceStream($"{typeof(TestHelper).Namespace}.Data.{name}")
            ?? throw new FileNotFoundException();
    }

    public static IEnumerable<string> LoadResource(string name, bool isInput)
    {
        using var stream = LoadResourceStream(name + ".txt");
        using var reader = new StreamReader(stream!);
        while(reader.ReadLine() is { } line)
        {
            if(String.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if(line.StartsWith("##", StringComparison.Ordinal))
            {
                // Swap input/output
                isInput = !isInput;
                continue;
            }

            if(line.StartsWith('#'))
            {
                // Comment
                continue;
            }

            if(isInput)
            {
                yield return line;
            }
        }
    }

    protected void Send(string file)
    {
        using(var writer = new StreamWriter(clientToServerPipe.Writer.AsStream(leaveOpen: true)))
        {
            foreach(var line in LoadResource(file, true))
            {
                // Ignore newlines
                writer.Write(line);
            }
        }
    }

    protected void Receive(string file)
    {
        if(sessionTask.Wait(5))
        {
            throw new InvalidOperationException("The session was closed.");
        }

        var stringReader = new StringReader(String.Concat(LoadResource(file, false)));
        using(var expected = XmlReader.Create(stringReader, testReaderSettings))
        {
            using var reader = XmlReader.Create(serverToClientPipe.Reader.AsStream(leaveOpen: true), testReaderSettings);
            AssertEqualXml(expected, reader, () => stringReader.Peek() == -1);
        }
    }

    protected void FlushReceive()
    {
        var reader = serverToClientPipe.Reader;
        while(reader.TryRead(out _))
        {
            // Move past all remaining data
        }
    }

    protected void FinishReceive()
    {
        var reader = serverToClientPipe.Reader;
        Assert.IsFalse(reader.TryRead(out _), "Unexpected data remaining in the stream.");
    }

    private static void AssertEqualXml(XmlReader expected, XmlReader actual, Func<bool> isFinished)
    {
        while(Read())
        {
            if(expected.NodeType is XmlNodeType.Whitespace)
            {
                continue;
            }

            do
            {
                bool result = false;
#pragma warning disable SYSLIB0046
                ControlledExecution.Run(() => {
                    // Might block when no data was received
                    result = actual.Read();
                }, new CancellationTokenSource(2000).Token);
#pragma warning restore SYSLIB0046
                Assert.IsTrue(result, "XML stream ended prematurely.");
            }
            while(actual.NodeType is XmlNodeType.None or XmlNodeType.Whitespace);

            Assert.AreEqual(expected.NodeType, actual.NodeType);

            switch(expected.NodeType)
            {
                case XmlNodeType.Element:
                    Assert.AreEqual(expected.LocalName, actual.LocalName);
                    Assert.AreEqual(expected.NamespaceURI, actual.NamespaceURI);
                    for(int i = 0; i < expected.AttributeCount; i++)
                    {
                        expected.MoveToAttribute(i);
                        if(expected.Prefix == "xmlns" || expected.LocalName == "xmlns")
                        {
                            // Namespace declarations are not relevant
                            continue;
                        }

                        Assert.AreEqual(expected.Value, actual.GetAttribute(expected.LocalName, expected.NamespaceURI));
                    }
                    expected.MoveToElement();
                    break;
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.SignificantWhitespace:
                    Assert.AreEqual(expected.Value, actual.Value);
                    break;
                // Other nodes are not expected
            }
        }
        
        // Actual might still have more data

        bool Read()
        {
            try
            {
                return expected.Read();
            }
            catch(XmlException) when(isFinished())
            {
                // End of stream
                return false;
            }
        }
    }
}
