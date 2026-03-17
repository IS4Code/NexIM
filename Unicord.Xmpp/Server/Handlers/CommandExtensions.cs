using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Primitives.Xml;
using Unicord.Server;
using Unicord.Server.Model;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Handlers;

internal static class CommandExtensions
{
    public static XmppResource GetLocalResource(this ICommandHandler handler) => handler.Context.Session.LocalResource ?? throw XmppStanzaException.InternalServerError("The remote server is not properly identified.");
    public static XmppResource GetRemoteResource(this ICommandHandler handler) => handler.Context.Session.RemoteResource ?? throw XmppStanzaException.NotAuthorized();
    public static AccountName GetAccountName(this ICommandHandler handler) => ClientSession.GetAccount(GetRemoteResource(handler), out _);
    public static Account GetAccount(this ICommandHandler handler) => handler.Context.Server.Accounts.GetAccount(GetAccountName(handler)) ?? throw XmppStanzaException.NotAuthorized();

    public static THandler GetHandler<THandler>(this ICommandHandler handler) where THandler : ICommandHandler, new()
    {
        return new THandler()
        {
            Context = handler.Context
        };
    }

    public static Stanza NewResponse(this ICommandHandler handler, StanzaType? type = StanzaType.Result, XmppResource? from = null)
    {
        return new Stanza(Type: type?.ToToken(), Identifier: handler.Context.Identifier, From: from ?? handler.Context.Session.LocalResource, To: handler.Context.Session.RemoteResource);
    }

    public static ValueTask<IInfoQueryHandler> CreateResponse(this ICommandHandler handler, StanzaType? type = StanzaType.Result, XmppResource? from = null)
    {
        return handler.Context.Session.InfoQuery(NewResponse(handler, type: type, from: from));
    }

    public static async ValueTask SendResponse(this ICommandHandler handler, StanzaType? type = StanzaType.Result, XmppResource? from = null, Func<IInfoQueryHandler, ValueTask>? contentProvider = null)
    {
        await using var iq = await CreateResponse(handler, type: type, from: from);
        if(contentProvider != null)
        {
            await contentProvider(iq);
        }
    }

    public static Stanza NewRequest(this ICommandHandler handler, out Token<StanzaIdentifier> identifier, StanzaType? type = StanzaType.Get, XmppResource? from = null)
    {
        identifier = handler.Context.Session.NewStanzaIdentifier();
        return new Stanza(Type: type?.ToToken(), Identifier: identifier, From: from ?? handler.Context.Session.LocalResource, To: handler.Context.Session.RemoteResource);
    }

    public static ValueTask<IInfoQueryHandler> CreateRequest(this ICommandHandler handler, Func<ValueTask<IInfoQueryHandler>> callback, StanzaType? type = StanzaType.Get, XmppResource? from = null)
    {
        var request = NewRequest(handler, out var identifier, type: type, from: from);
        handler.Context.Session.RegisterCallback(identifier, callback);
        return handler.Context.Session.InfoQuery(request);
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
        if(stanza.From is { } from && !from.IsNarrowerThan(handler.Context.Session.RemoteResource))
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
