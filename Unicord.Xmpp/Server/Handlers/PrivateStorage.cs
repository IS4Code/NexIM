using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Primitives.Xml.Handlers;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal sealed class GetPrivateStorage : BasePrivateStorageHandler<ICommandContext>
{
    XName? key;

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
        this.SetOnce(ref key, XName.Get(payloadReader.LocalName, payloadReader.NamespaceURI));
    }

    private PrivateData GetData()
    {
        if(key == null)
        {
            throw XmppStanzaException.BadRequest();
        }
        return new PrivateData {
            Key = key
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

    XName? key;

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
        this.SetOnce(ref key, XName.Get(payloadReader.LocalName, payloadReader.NamespaceURI));

        using var nested = payloadReader.ReadSubtree();
        await base.OnOther(nested);
    }

    protected PrivateData GetData()
    {
        if(key == null)
        {
            throw XmppStanzaException.BadRequest();
        }
        return new PrivateData {
            Key = key,
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
