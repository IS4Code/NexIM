using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Unicord.Primitives;
using Unicord.Server.Accounts;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Server.Communication;

namespace Unicord.Xmpp.Server.Handlers;

internal static class CommandExtensions
{
    public static ICommandContext GetContext(this ICommandHandler handler)
    {
        return handler.Context ?? throw new InvalidOperationException("The command context is not properly initialized.");
    }

    public static XmppResource? TryGetLocalResource(this ICommandHandler handler)
    {
        return GetContext(handler).Session.LocalResource;
    }

    public static XmppResource? TryGetRemoteResource(this ICommandHandler handler)
    {
        return GetContext(handler).Session.RemoteResource;
    }

    public static XmppResource GetLocalResource(this ICommandHandler handler)
    {
        return TryGetLocalResource(handler) ?? throw XmppStanzaException.InternalServerError("The remote server is not properly identified.");
    }

    public static XmppResource GetRemoteResource(this ICommandHandler handler)
    {
        return TryGetRemoteResource(handler) ?? throw XmppStanzaException.NotAuthorized();
    }

    public static Account GetAccount(this ICommandHandler handler)
    {
        return GetClientSession(handler).Account;
    }

    public static XmppClientSession? TryGetClientSession(this ICommandHandler handler)
    {
        return GetContext(handler).Session.ClientSession;
    }

    public static XmppClientSession GetClientSession(this ICommandHandler handler)
    {
        return TryGetClientSession(handler) ?? throw XmppStanzaException.NotAuthorized();
    }

    public static IXmppSession GetSession(this ICommandHandler handler)
    {
        return GetContext(handler).Session;
    }

    public static XmppServer GetServer(this ICommandHandler handler)
    {
        return GetContext(handler).Server;
    }

    public static ref readonly Stanza GetStanza(this ICommandHandler handler)
    {
        return ref GetContext(handler).LastStanza;
    }

    public static bool IsSecureSession(this ICommandHandler handler)
    {
        return GetSession(handler).IsSecure;
    }

    public static void Post(this ICommandHandler handler, Event evnt)
    {
        GetContext(handler).EventsToSend.Add(evnt);
    }

    public static THandler GetHandler<THandler>(this ICommandHandler handler) where THandler : ICommandHandler, new()
    {
        return new THandler()
        {
            Context = handler.Context
        };
    }

    public static Stanza NewResponse(this ICommandHandler handler, StanzaType? type = StanzaType.Result, XmppResource? from = null)
    {
        return new Stanza(Type: type?.ToToken(), Identifier: GetStanza(handler).Identifier, From: from ?? TryGetLocalResource(handler), To: TryGetRemoteResource(handler));
    }

    public static ValueTask<IInfoQueryHandler> CreateResponse(this ICommandHandler handler, StanzaType? type = StanzaType.Result, XmppResource? from = null)
    {
        return GetSession(handler).InfoQuery(NewResponse(handler, type: type, from: from));
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
        identifier = GetSession(handler).NewStanzaIdentifier();
        return new Stanza(Type: type?.ToToken(), Identifier: identifier, From: from ?? TryGetLocalResource(handler), To: TryGetRemoteResource(handler));
    }

    public static ValueTask<IInfoQueryHandler> CreateRequest(this ICommandHandler handler, Func<ValueTask<IInfoQueryHandler>> callback, StanzaType? type = StanzaType.Get, XmppResource? from = null)
    {
        var request = NewRequest(handler, out var identifier, type: type, from: from);
        GetSession(handler).RegisterCallback(identifier, callback);
        return GetSession(handler).InfoQuery(request);
    }

    public static T SetOnce<T>(this ICommandHandler handler, ref T? storage, T value)
    {
        if(storage != null)
        {
            throw XmppStanzaException.BadRequest("Property set multiple times.");
        }
        return storage = value;
    }

    public static void ValidateSender(this ICommandHandler handler, in Stanza stanza)
    {
        if(stanza.From is { } from && !from.IsNarrowerThan(GetSession(handler).RemoteResource))
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
