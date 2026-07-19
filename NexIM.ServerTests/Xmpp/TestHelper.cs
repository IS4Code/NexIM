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
        IgnoreWhitespace = false,
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

    static readonly XmlReaderSettings testPipeReaderSettings = new() {
        Async = true,
        CheckCharacters = false,
        CloseInput = false,
        ConformanceLevel = ConformanceLevel.Fragment,
        DtdProcessing = DtdProcessing.Ignore,
        IgnoreComments = true,
        IgnoreWhitespace = false,
        ValidationType = ValidationType.None,
        XmlResolver = XmlResolver.ThrowingResolver
    };
    static readonly XmlReaderSettings testStringReaderSettings = new() {
        Async = false,
        CheckCharacters = false,
        CloseInput = false,
        ConformanceLevel = ConformanceLevel.Fragment,
        DtdProcessing = DtdProcessing.Ignore,
        IgnoreComments = true,
        IgnoreWhitespace = false,
        ValidationType = ValidationType.None,
        XmlResolver = XmlResolver.ThrowingResolver
    };

    static string dbPath = null!;

    static int running = 0;

    public static async ValueTask RegisterAccounts(NexServer server)
    {
        await RegisterAccount(server, "test", "test", "test@example.org");
        await RegisterAccount(server, "test2", "test2", "test2@example.org");
    }

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
        RegisterAccounts(server).AsTask().GetAwaiter().GetResult();
    }

    private static async ValueTask RegisterAccount(NexServer server, string user, string passwordString, string email)
    {
        using var password = new TemporaryString();
        password.Append(passwordString);
        await server.Register(new AccountName(user, "localhost"), password, new(email), new());
    }

    protected static void ServerCleanup()
    {
        if(Interlocked.Decrement(ref running) != 0)
        {
            // Still in use
            return;
        }

        receiver.Server.Close();
        File.Delete(dbPath);
    }

    XmppTestClient primaryClient = null!;
    readonly List<XmppTestClient> secondaryClients = new();
    CancellationTokenSource testCts = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        testCts = new();
        primaryClient = new("Prologue1", "Prologue2", testCts.Token);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        testCts.Cancel();
        try
        {
            primaryClient.Shutdown();
        }
        finally
        {
            foreach(var client in secondaryClients)
            {
                client.Shutdown();
            }
            secondaryClients.Clear();
        }
    }

    protected sealed class XmppTestClient
    {
        readonly Pipe clientToServerPipe = new();
        readonly Pipe serverToClientPipe = new();
        readonly XmppManualSession session;
        readonly Task sessionTask;

        internal XmppTestClient(string prologue1, string prologue2, CancellationToken cancellationToken)
        {
            // Provide auth message
            Send(prologue1);

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
            sessionTask = session.Run(handler, cancellationToken).AsTask();

            // Check auth response
            Receive(prologue1);

            // Bind
            Send(prologue2);

            // Check bound
            Receive(prologue2);

            // Ignore anything else
            FlushReceive();
        }

        internal void Send(string file)
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

        internal void Receive(string file)
        {
            if(sessionTask.Wait(5))
            {
                throw new InvalidOperationException("The session was closed.");
            }

            var stringReader = new StringReader(String.Concat(LoadResource(file, false)));
            using(var expected = XmlReader.Create(stringReader, testStringReaderSettings))
            {
                var stream = new TimeoutableStream(serverToClientPipe.Reader.AsStream(leaveOpen: true)) {
                    ReadTimeout = 2000
                };
                using var reader = XmlReader.Create(stream, testPipeReaderSettings);
                AssertEqualXml(expected, reader, () => stringReader.Peek() == -1);
            }
        }

        internal void FlushReceive()
        {
            var reader = serverToClientPipe.Reader;
            while(reader.TryRead(out var result))
            {
                // Move past all remaining data
                reader.AdvanceTo(result.Buffer.End);
            }
        }

        internal void FinishReceive()
        {
            var reader = serverToClientPipe.Reader;
            Assert.IsFalse(reader.TryRead(out _), "Unexpected data remaining in the stream.");
        }

        internal void Shutdown()
        {
            try
            {
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
    }

    protected XmppTestClient CreateSecondClient()
    {
        var client = new XmppTestClient("Prologue1Second", "Prologue2Second", testCts.Token);
        secondaryClients.Add(client);
        return client;
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

    protected void Send(string file) => primaryClient.Send(file);

    protected void Receive(string file) => primaryClient.Receive(file);

    protected void FlushReceive() => primaryClient.FlushReceive();

    protected void FinishReceive() => primaryClient.FinishReceive();

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
                // Go through async read to trigger timeout
                Assert.IsTrue(actual.ReadAsync().GetAwaiter().GetResult(), "XML stream ended prematurely.");
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
