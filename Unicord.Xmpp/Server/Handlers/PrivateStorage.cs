using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives.Xml.Handlers;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal sealed class GetPrivateStorage : BasePrivateStorageHandler<ICommandContext>
{
    (string localName, string ns)? element;

    protected async override ValueTask OnOther(XmlReader payloadReader)
    {
        if(payloadReader.NodeType == XmlNodeType.None)
        {
            await payloadReader.ReadAsync();
        }
        if(payloadReader.NodeType != XmlNodeType.Element)
        {
            throw XmppStanzaException.BadRequest();
        }
        this.SetOnce(ref element, (payloadReader.LocalName, payloadReader.NamespaceURI));
    }

    private PrivateStorageData GetData()
    {
        if(element is not (var localName, var ns))
        {
            throw XmppStanzaException.BadRequest();
        }
        return new PrivateStorageData {
            KeyName = localName,
            KeyNamespace = ns
        };
    }

    private RetrieveEvent GetEvent()
    {
        return new RetrieveEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = GetData()
        };
    }

    protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);

    public async sealed override ValueTask DisposeAsync()
    {
        this.Post(GetEvent());
    }
}

internal abstract class DataPrivateStorage : BaseDelegatingPrivateStorageHandler<CapturingHandler<IPrivateStorageHandler>, EmptyDisposable, ICommandContext>
{
    protected sealed override CapturingHandler<IPrivateStorageHandler> InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    (string localName, string ns)? element;

    protected async override ValueTask OnOther(XmlReader payloadReader)
    {
        if(payloadReader.NodeType == XmlNodeType.None)
        {
            await payloadReader.ReadAsync();
        }
        if(payloadReader.NodeType != XmlNodeType.Element)
        {
            throw XmppStanzaException.BadRequest();
        }
        this.SetOnce(ref element, (payloadReader.LocalName, payloadReader.NamespaceURI));
        
        await base.OnOther(payloadReader);
    }

    protected PrivateStorageData GetData()
    {
        if(element is not (var localName, var ns))
        {
            throw XmppStanzaException.BadRequest();
        }
        return new PrivateStorageData {
            KeyName = localName,
            KeyNamespace = ns,
            Extensions = InnerHandler.ToExtensions()
        };
    }

    protected abstract QueryEvent GetEvent();

    public async sealed override ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            this.Post(GetEvent());
        }
    }
}

internal sealed class SetPrivateStorage : DataPrivateStorage
{
    protected override QueryEvent GetEvent()
    {
        return new UpdateEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = GetData()
        };
    }
}

internal sealed class ResultPrivateStorage : DataPrivateStorage
{
    protected override QueryEvent GetEvent()
    {
        return new ResponseEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = GetData()
        };
    }
}
