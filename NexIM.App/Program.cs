using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NexIM.App.Configuration;
using NexIM.Server;

namespace NexIM.App;

internal class Program
{
    static async Task Main(string[] args)
    {
        CancellationToken cancellationToken = default;
        TaskCompletionSource programFinished = new();
        try
        {
            cancellationToken = AppCancellation(programFinished.Task);

            string configPath = "config.xml";

            var asm = typeof(Program).Assembly;
            var ver = asm.GetName().Version ?? new();
            Console.WriteLine($"NexIM v{ver.Major}.{ver.Minor} starting...");
            Console.WriteLine("Warning: This is a PRERELASE version. Anything is subject to change. Backwards compatibility is not guaranteed.");
            Console.WriteLine("Please report all bugs at https://github.com/IS4Code/NexIM");

            if(args.Length > 0)
            {
                if(args.Length > 1 || args[0].ToLowerInvariant() is "-?" or "/?" or "--help")
                {
                    Console.WriteLine($"Usage: {Path.GetFileName(Environment.ProcessPath)} [config path]");
                    return;
                }
                configPath = args[0];
            }

            ConfigurationHandler config;
            try
            {
                config = await ConfigurationReader.Read(configPath);
            }
            catch(Exception e)
            {
                throw new ApplicationException($"Configuration file '{configPath}' {(File.Exists(configPath) ? $"cannot be read ({e.Message})" : "is missing")}.");
            }

            var connectionString = config.ConnectionString;
            const string memorySqlite = "Data Source=:memory:";
            if(config.DatabaseType is not { } dbType)
            {
                Console.WriteLine("Warning: Database configuration is missing. An in-memory SQLite database will be used.");
                dbType = DatabaseType.Sqlite;
                connectionString = memorySqlite;
            }
            else if(String.IsNullOrEmpty(connectionString))
            {
                if(dbType != DatabaseType.Sqlite)
                {
                    throw new ApplicationException($"{dbType.ToToken().Value} connection string is missing.");
                }
                Console.WriteLine("Warning: An in-memory SQLite database will be used.");
                connectionString = memorySqlite;
            }
            config.XmppReceiver.Server = new NexServer(dbType switch {
                DatabaseType.Sqlite => new NexDatabase.Sqlite {
                    ConnectionString = connectionString
                },
                DatabaseType.MySQL => new NexDatabase.MySQL {
                    ConnectionString = connectionString
                },
                DatabaseType.PostgreSQL => new NexDatabase.PostgreSQL {
                    ConnectionString = connectionString
                },
                _ => throw new ApplicationException($"{dbType.ToToken().Value} database is not supported.")
            });

            var tasks = new List<Task>();

            if(config.XmppTcp is { } tcpListener)
            {
                Console.WriteLine("Starting XMPP TCP listener at: " + String.Join(", ", tcpListener.EndPoints));
                tasks.Add(tcpListener.RunAsync(cancellationToken));
            }
            if(config.XmppWebSocket is { } wsListener)
            {
                Console.WriteLine("Starting XMPP WebSocket listener at: " + String.Join(", ", wsListener.Prefixes));
                tasks.Add(wsListener.RunAsync(cancellationToken));
            }
            if(config.XmppHtml is { } webListener)
            {
                Console.WriteLine("Starting XMPP HTML service at: " + String.Join(", ", webListener.Prefixes));
                tasks.Add(webListener.RunAsync(cancellationToken));
            }
            if(config.Metadata is { } metadataServer)
            {
                Console.WriteLine("Starting well-known services at: " + String.Join(", ", metadataServer.Prefixes));
                tasks.Add(metadataServer.RunAsync(cancellationToken));
            }

            Console.WriteLine("Server is up!");
            await Task.WhenAll(tasks);
        }
        catch(Exception e) when(e is OperationCanceledException or ObjectDisposedException && cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("Server stopped.");
        }
        catch(ApplicationException e) when(!Debugger.IsAttached)
        {
            Console.WriteLine($"Error: {e.Message}");
        }
        catch(Exception e) when(!Debugger.IsAttached)
        {
            Console.WriteLine($"Unhandled error: {e}");
        }
        finally
        {
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
