using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Server;
using NexIM.ServerTests.Xmpp;
using NexIM.Tools;
using NexIM.Xmpp.Server;
using NexIM.Xmpp.Server.Communication;

namespace NexIM.ServerTests.Terminal;

class Program
{
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

    static async Task Main(string[] args)
    {
        var dbPath = Path.GetTempFileName();

        // Prepare the server
        var receiver = new XmppServerReceiver();

        TaskCompletionSource programFinished = new();
        var clients = Array.Empty<Client>();
        try
        {
            var cancellationToken = AppCancellation(programFinished.Task);

            var server = receiver.Server = new NexServer(new NexDatabase.Sqlite {
                ConnectionString = $"Data Source=\"{dbPath}\""
            });

            await TestHelper.RegisterAccounts(server);

            clients = new[] {
                new Client(1, "Prologue1", "Prologue2", ConsoleColor.Cyan),
                new Client(2, "Prologue1Second", "Prologue2Second", ConsoleColor.Green)
            };

            var startTasks = new Task[clients.Length];
            for(int i = 0; i < clients.Length; i++)
            {
                startTasks[i] = clients[i].Start(receiver, cancellationToken);
            }
            await Task.WhenAll(startTasks);

            // Route to client based on prefix
            while(Console.ReadLine() is { } line)
            {
                var target = clients[0];
                foreach(var client in clients)
                {
                    var prefix = client.Prefix;
                    if(line.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        target = client;
                        line = line[prefix.Length..].TrimStart(' ');
                        break;
                    }
                }
                target.WriteLine(line);
            }

            foreach(var client in clients)
            {
                client.CompleteInput();
            }
        }
        finally
        {
            foreach(var client in clients)
            {
                await client.DisposeAsync();
            }

            receiver.Server.Close();
            File.Delete(dbPath);

            programFinished.TrySetResult();
            termSignalRegistration?.Dispose();
            sigHupSignalRegistration?.Dispose();
        }
    }

    sealed class Client(int index, string prologue1, string prologue2, ConsoleColor color) : IAsyncDisposable
    {
        public string Prefix { get; } = $"{index}>";

        readonly Pipe clientToServerPipe = new();
        readonly Pipe serverToClientPipe = new();

        XmppManualSession? session;
        Stream? readerStream, outputStream;
        StreamWriter inputWriter = null!;

        public async Task Start(XmppServerReceiver receiver, CancellationToken cancellationToken)
        {
            inputWriter = new StreamWriter(clientToServerPipe.Writer.AsStream(leaveOpen: true));

            // Provide auth message
            Send(prologue1);

            // Create session over the two channels
            session = new XmppManualSession(
                new BidirectionalStream(
                    clientToServerPipe.Reader.AsStream(),
                    serverToClientPipe.Writer.AsStream()
                ),
                receiver,
                xmppReaderSettings,
                xmppWriterSettings
            );

            // Connect to receiver
            var handler = await receiver.Connected(session);

            // Start session
            var sessionTask = session.Run(handler, cancellationToken).AsTask();

            try
            {
                // Wait a bit for initialization
                await sessionTask.WaitAsync(TimeSpan.FromMilliseconds(1000));
            }
            catch(TimeoutException)
            {
                // Should always happen
            }

            var reader = serverToClientPipe.Reader;
            while(reader.TryRead(out var result))
            {
                // Skip all initial messages
                reader.AdvanceTo(result.Buffer.End);
            }

            // Redirect output to console now
            readerStream = serverToClientPipe.Reader.AsStream();
            outputStream = new ConsoleDebuggingStream(Stream.Null, color) {
                SendIndicator = Prefix + " ",
                ReceiveIndicator = "",
                IndicatorSeparator = ""
            };
            _ = readerStream.CopyToAsync(outputStream, cancellationToken);

            // Send bind
            Send(prologue2);
        }

        public async ValueTask DisposeAsync()
        {
            outputStream?.Dispose();
            readerStream?.Dispose();
            if(session != null)
            {
                await session.DisposeAsync();
            }
        }

        void Send(string file)
        {
            foreach(var line in TestHelper.LoadResource(file, true))
            {
                inputWriter.Write(line);
            }
            inputWriter.Flush();
        }

        public void WriteLine(string line)
        {
            inputWriter.Write(line);
            inputWriter.Flush();
        }

        public void CompleteInput()
        {
            clientToServerPipe.Writer.Complete();
        }
    }

    static PosixSignalRegistration? termSignalRegistration, sigHupSignalRegistration;

    static CancellationToken AppCancellation(Task completed)
    {
        var cts = new CancellationTokenSource();

        AppDomain.CurrentDomain.ProcessExit += delegate { Cancel(); };
        Console.CancelKeyPress += delegate { Cancel(); };
        termSignalRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, delegate { Cancel(); });
        sigHupSignalRegistration = PosixSignalRegistration.Create(PosixSignal.SIGHUP, delegate { Cancel(); });

        void Cancel()
        {
            cts.Cancel();

            // Block until main task is finished
            if(!completed.Wait(30000))
            {
                Console.WriteLine("The application did not finish within the timeout.");
            }
        }

        return cts.Token;
    }
}
