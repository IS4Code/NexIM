using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Server;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Communication;

internal static class CommandExtensions
{
    public static XmppResource GetLocalResource(this ICommandHandler handler) => handler.State.Session.LocalResource ?? throw XmppStanzaException.InternalServerError("The remote server is not properly identified.");
    public static XmppResource GetRemoteResource(this ICommandHandler handler) => handler.State.Session.RemoteResource ?? throw XmppStanzaException.NotAuthorized();
    public static AccountName GetAccountName(this ICommandHandler handler) => ClientSession.GetAccount(GetRemoteResource(handler), out _);
    public static Account GetAccount(this ICommandHandler handler) => handler.State.Server.Accounts.GetAccount(GetAccountName(handler)) ?? throw XmppStanzaException.NotAuthorized();

    public static Stanza NewResponse(this ICommandHandler handler, StanzaType? type = StanzaType.Result, XmppResource? from = null)
    {
        return new Stanza(Type: type?.ToToken(), Identifier: handler.State.Identifier, From: from ?? handler.State.Session.LocalResource, To: handler.State.Session.RemoteResource);
    }

    public static ValueTask<IInfoQueryHandler> CreateResponse(this ICommandHandler handler, StanzaType? type = StanzaType.Result, XmppResource? from = null)
    {
        return handler.State.Session.InfoQuery(NewResponse(handler, type: type, from: from));
    }

    public static void SetOnce<T>(this ICommandHandler handler, ref T storage, T value)
    {
        if(storage != null)
        {
            throw XmppStanzaException.BadRequest("Property set multiple times.");
        }
        storage = value;
    }

    public static void ValidateSender(this ICommandHandler handler, in Stanza stanza)
    {
        if(stanza.From is { } from && !from.IsNarrowerThan(handler.State.Session.RemoteResource))
        {
            throw XmppStreamException.InvalidFrom();
        }
    }

    public static async ValueTask Unexpected(this ICommandHandler handler, XmlReader payloadReader)
    {
        throw XmppStanzaException.BadRequest("Element was not expected.");
    }

    const LoadOptions elementLoadOptions = LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo;

    public static async ValueTask Unrecognized(this ICommandHandler handler, XmlReader payloadReader)
    {
        var element = await XElement.LoadAsync(payloadReader, elementLoadOptions, CancellationToken.None);
        lock(typeof(Console))
        {
            Console.WriteLine("Unknown payload: " + element);
        }
    }
}
