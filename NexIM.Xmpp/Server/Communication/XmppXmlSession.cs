using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Primitives.Xml;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Grammar;

namespace NexIM.Xmpp.Server.Communication;

/// <summary>
/// Provides an implementation of <see cref="IXmppSession"/> capable of
/// sending synchronized XMPP commands as XML data.
/// </summary>
public abstract class XmppXmlSession : XmppSession
{
    readonly SemaphoreSlim semaphore = new(1, 1);

    protected sealed override IStreamHandler InnerHandler { get; }

    public abstract XmlReader Reader { get; }
    public abstract XmlWriter Writer { get; }

    // Cannot access XmlReader.NameTable when asynchronous reading is in progress
    protected virtual XmlNameTable NameTable => Reader.Settings?.NameTable ?? Reader.NameTable;

    public XmppXmlSession()
    {
        InnerHandler = new CommandHandler(this);
    }

    protected async sealed override ValueTask<bool> OnEnter()
    {
        await semaphore.WaitAsync(CancellationToken);
        return true;
    }

    protected async override ValueTask OnExit()
    {
        await FlushCommand();
        semaphore.Release();
    }

    public override Token<T> GetToken<T>(ReadOnlyMemory<char> value)
    {
        return Token<T>.FromAtomized(NameTable.Add(value));
    }

    public override Token<T> GetToken<T>(ReadOnlySpan<char> value)
    {
        return Token<T>.FromAtomized(NameTable.Add(value));
    }

    protected abstract ValueTask UpgradeTls();
    protected abstract ValueTask EnableCompression();
    protected abstract ValueTask Authenticated();
    protected abstract ValueTask Close();

    public abstract ValueTask FlushCommand();
    public abstract ValueTask<bool> CheckFinished();

    protected async sealed override ValueTask OnTlsProceed()
    {
        await base.OnTlsProceed();
        await FlushCommand();

        // Swap to TLS while locked
        await UpgradeTls();
    }

    protected async sealed override ValueTask OnTlsFailure()
    {
        try
        {
            await base.OnTlsFailure();
            await FlushCommand();
        }
        finally
        {
            // No more commands will be sent
            await Close();
        }
    }

    protected async sealed override ValueTask OnCompressed()
    {
        await base.OnCompressed();
        await FlushCommand();

        // Enable compression while locked
        await EnableCompression();
    }

    protected async sealed override ValueTask OnSaslSuccess()
    {
        await base.OnSaslSuccess();
        await FlushCommand();

        // Finish authentication while locked
        await Authenticated();
    }

    class CommandHandler(XmppXmlSession session) : Encoder
    {
        protected override XmlWriter Writer => session.Writer;
        protected override CancellationToken CancellationToken => session.CancellationToken;
        public override string DefaultNamespace => session.DefaultNamespace;

        protected override ValueTask<Encoder> ForkInner()
        {
            return new(new ElementHandler(session));
        }

        sealed class ElementHandler(XmppXmlSession session) : CommandHandler(session)
        {
            int level = 1;

            protected override ValueTask<Encoder> ForkInner()
            {
                // Reuse the current instance to encode nested elements
                if(Interlocked.Increment(ref level) <= 1)
                {
                    throw new ObjectDisposedException(ToString());
                }
                return new(this);
            }

            public override ValueTask DisposeAsync()
            {
                if(Interlocked.Decrement(ref level) < 0)
                {
                    return default;
                }
                return base.DisposeAsync();
            }
        }
    }
}
