using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Grammar;

using static XmppVocabulary;

internal abstract class XmppEncoder : IPayloadHandler, IMessageHandler, IPresenceHandler, IInfoQueryHandler, IRosterQueryHandler, IAuthQueryHandler, IFeaturesHandler
{
    protected abstract XmlWriter Writer { get; }
    protected abstract CancellationToken CancellationToken { get; }
    protected abstract XmppEncoder ForkInner();

    async ValueTask IPayloadHandler.Other(XElement payload)
    {
        await payload.WriteToAsync(Writer, CancellationToken);
    }

    public abstract ValueTask DisposeAsync();

    async ValueTask IMessageHandler.Body(string? text)
    {
        await WriteElementValueAsync(Writer, null, Body, JabberClientNs, text);
    }

    async ValueTask IMessageHandler.Subject(string? text)
    {
        await WriteElementValueAsync(Writer, null, Subject, JabberClientNs, text);
    }

    async ValueTask IPresenceHandler.Show(string? text)
    {
        await WriteElementValueAsync(Writer, null, Show, JabberClientNs, text);
    }

    async ValueTask IPresenceHandler.Status(string? text)
    {
        await WriteElementValueAsync(Writer, null, Status, JabberClientNs, text);
    }

    async ValueTask<IRosterQueryHandler> IInfoQueryHandler.RosterQuery()
    {
        await Writer.WriteStartElementAsync(null, Query, JabberIqRosterNs);
        return ForkInner();
    }

    async ValueTask<IAuthQueryHandler> IInfoQueryHandler.AuthQuery()
    {
        await Writer.WriteStartElementAsync(null, Query, JabberIqAuthNs);
        return ForkInner();
    }

    async ValueTask IRosterQueryHandler.Item(string? identifier)
    {
        var writer = Writer;
        await writer.WriteStartElementAsync(null, Item, JabberIqRosterNs);
        if(identifier != null)
        {
            await writer.WriteAttributeStringAsync(null, Jid, null, identifier);
        }
        await writer.WriteEndElementAsync();
    }

    async ValueTask IAuthQueryHandler.Username(string? value)
    {
        await WriteElementValueAsync(Writer, null, Username, JabberIqAuthNs, value);
    }

    async ValueTask IAuthQueryHandler.Password(string? value)
    {
        await WriteElementValueAsync(Writer, null, Password, JabberIqAuthNs, value);
    }

    async ValueTask IAuthQueryHandler.Digest(string? value)
    {
        await WriteElementValueAsync(Writer, null, Digest, JabberIqAuthNs, value);
    }

    async ValueTask IAuthQueryHandler.Resource(string? value)
    {
        await WriteElementValueAsync(Writer, null, Resource, JabberIqAuthNs, value);
    }

    async ValueTask IFeaturesHandler.IqAuth()
    {
        var writer = Writer;
        await writer.WriteStartElementAsync(null, Auth, IqAuthNs);
        await writer.WriteEndElementAsync();
    }

    static async Task WriteElementValueAsync(XmlWriter writer, string? prefix, string localName, string? ns, string? value)
    {
        if(value is null)
        {
            await writer.WriteStartElementAsync(prefix, localName, ns);
            await writer.WriteEndElementAsync();
        }
        else
        {
            await writer.WriteElementStringAsync(prefix, localName, ns, value);
        }
    }
}
