using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Server.Accounts;
using NexIM.Server.Events;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Server.Communication;

namespace NexIM.Xmpp.Server.Handlers;

[SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Used for extension methods")]
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

    public static XmppServerReceiver GetServerReceiver(this ICommandHandler handler)
    {
        return GetContext(handler).ServerReceiver;
    }

    public static NexIM.Server.Server GetServer(this ICommandHandler handler)
    {
        return GetContext(handler).ServerReceiver.Server;
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
        return new THandler() {
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

    const string propertySetError = "Property set multiple times.";

    public static T SetOnce<T, TContext>(this IPayloadHandler<TContext> handler, ref T? storage, T value) where T : struct where TContext : IPayloadHandlerContext
    {
        if(storage != null)
        {
            throw XmppStanzaException.BadRequest(propertySetError);
        }
        storage = value;
        return value;
    }

    public static T? SetOnce<T, TContext>(this IPayloadHandler<TContext> handler, ref T? storage, T? value) where T : struct where TContext : IPayloadHandlerContext
    {
        if(storage != null)
        {
            throw XmppStanzaException.BadRequest(propertySetError);
        }
        storage = value;
        return value;
    }

    [return: NotNullIfNotNull(nameof(value))]
    public static T? SetOnce<T, TContext>(this IPayloadHandler<TContext> handler, ref T? storage, T? value) where T : class where TContext : IPayloadHandlerContext
    {
        if(storage != null)
        {
            throw XmppStanzaException.BadRequest(propertySetError);
        }
        storage = value;
        return value;
    }

    public static T SetOnceFlag<T, TContext>(this IPayloadHandler<TContext> handler, ref T storage, T value) where T : unmanaged, Enum where TContext : IPayloadHandlerContext
    {
        if(Unsafe.SizeOf<T>() != sizeof(int))
        {
            throw new ArgumentException("Argument must be 32-bit.", nameof(storage));
        }
        ref int rawStorage = ref Unsafe.As<T, int>(ref storage);
        int rawValue = Unsafe.BitCast<T, int>(value);
        int newValue = rawStorage | rawValue;
        if(newValue == rawStorage)
        {
            throw XmppStanzaException.BadRequest(propertySetError);
        }
        rawStorage = newValue;
        return Unsafe.BitCast<int, T>(newValue);
    }

    public static bool SetOnce<TContext>(this IPayloadHandler<TContext> handler, ref bool storage, bool value) where TContext : IPayloadHandlerContext
    {
        if(storage == value)
        {
            throw XmppStanzaException.BadRequest(propertySetError);
        }
        return storage = value;
    }

    public static T? AddList<T, TContext>(this IPayloadHandler<TContext> handler, ref List<T>? list, T? value) where T : struct where TContext : IPayloadHandlerContext
    {
        if(value is { } val)
        {
            (list ??= new()).Add(val);
        }
        return value;
    }

    [return: NotNullIfNotNull(nameof(value))]
    public static T? AddList<T, TContext>(this IPayloadHandler<TContext> handler, ref List<T>? list, T? value) where TContext : IPayloadHandlerContext
    {
        if(value is { } val)
        {
            (list ??= new()).Add(val);
        }
        return value;
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

    public static async ValueTask Unrecognized<TContext>(this IPayloadHandler<TContext> handler, XmlReader payloadReader) where TContext : IPayloadHandlerContext
    {
        var element = await XElement.LoadAsync(payloadReader, elementLoadOptions, CancellationToken.None);
        lock(typeof(Console))
        {
            Console.WriteLine("Unknown payload: " + element);
        }
    }
}
