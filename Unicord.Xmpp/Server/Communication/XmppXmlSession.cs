using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Grammar;

namespace Unicord.Xmpp.Server.Communication;

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

    protected abstract ValueTask UpgradeTls();
    protected abstract ValueTask EnableCompression();
    protected abstract ValueTask Authenticated();
    protected abstract ValueTask Close();

    public abstract ValueTask FlushCommand();
    public abstract ValueTask<bool> CheckFinished();

    protected async sealed override ValueTask<bool> OnTlsProceed()
    {
        await base.OnTlsProceed();
        await FlushCommand();

        // Swap to TLS while locked
        await UpgradeTls();
        return true;
    }

    protected async sealed override ValueTask<bool> OnTlsFailure()
    {
        try
        {
            await base.OnTlsFailure();
            await FlushCommand();
            return true;
        }
        finally
        {
            // No more commands will be sent
            await Close();
        }
    }

    protected async sealed override ValueTask<bool> OnCompressed()
    {
        await base.OnCompressed();
        await FlushCommand();

        // Enable compression while locked
        await EnableCompression();
        return true;
    }

    protected async sealed override ValueTask<bool> OnSaslSuccess()
    {
        await base.OnSaslSuccess();
        await FlushCommand();

        // Finish authentication while locked
        await Authenticated();
        return true;
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
