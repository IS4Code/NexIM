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

    static async Task Main(string[] args)
    {
        var dbPath = Path.GetTempFileName();

        // Prepare the server
        var receiver = new XmppServerReceiver();

        CancellationToken cancellationToken = default;
        TaskCompletionSource programFinished = new();
        try
        {
            cancellationToken = AppCancellation(programFinished.Task);

            var server = receiver.Server = new NexServer(new NexDatabase.Sqlite {
                ConnectionString = $"Data Source=\"{dbPath}\""
            });

            using(var password = new TemporaryString())
            {
                password.Append("test");
                await server.Register(new AccountName("test", "localhost"), password, new("test@example.org"), new());
            }

            var clientToServerPipe = new Pipe();
            var serverToClientPipe = new Pipe();

            cancellationToken.Register(() => {
                clientToServerPipe.Writer.Complete();
                serverToClientPipe.Reader.Complete();
            });

            // Provide auth message
            using(var writer = new StreamWriter(clientToServerPipe.Writer.AsStream(leaveOpen: true)))
            {
                foreach(var line in TestHelper.LoadResource("Prologue1", true))
                {
                    writer.Write(line);
                }
            }

            // Create session over the two channels
            await using var session = new XmppManualSession(
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
            using var readerStream = serverToClientPipe.Reader.AsStream();
            using var outputStream = new ConsoleDebuggingStream(Stream.Null, Console.ForegroundColor) {
                SendIndicator = "",
                ReceiveIndicator = "",
                IndicatorSeparator = ""
            };
            _ = readerStream.CopyToAsync(outputStream, cancellationToken);

            using(var writer = new StreamWriter(clientToServerPipe.Writer.AsStream()))
            {
                // Send bind
                foreach(var line in TestHelper.LoadResource("Prologue2", true))
                {
                    writer.Write(line);
                }
                writer.Flush();

                // Redirect console to input
                while(Console.ReadLine() is { } line)
                {
                    writer.Write(line);
                    writer.Flush();
                }
            }
        }
        finally
        {
            receiver.Server.Close();
            File.Delete(dbPath);

            programFinished.TrySetResult();
            termSignalRegistration?.Dispose();
            sigHupSignalRegistration?.Dispose();
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
