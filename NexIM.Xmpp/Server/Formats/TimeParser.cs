using System;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;
using NexIM.Xmpp.Server.Handlers;

namespace NexIM.Xmpp.Server.Formats;

internal sealed class TimeParser<TContext> : BaseTimeHandler<TContext> where TContext : IPayloadHandlerContext
{
    public CapturingHandler<ITimeHandler>? ExtensionsHandler { get; private set; }

    DateTime? dateTime;
    TimeZoneOffset? timeZoneOffset;

    public DateTimeOffset DateTime =>
        new DateTimeOffset(dateTime ?? throw XmppStanzaException.BadRequest())
        .ToOffset((timeZoneOffset ?? throw XmppStanzaException.BadRequest()).Value);

    protected async override ValueTask OnUtcTime(DateTime? time)
    {
        this.SetOnce(ref dateTime, time);
    }

    protected async override ValueTask OnTimeZoneOffset(TimeZoneOffset? offset)
    {
        this.SetOnce(ref timeZoneOffset, offset);
    }

    protected override ValueTask OnOther(XmlReader payloadReader)
    {
        IPayloadHandler handler = ExtensionsHandler ??= new();
        return handler.Other(payloadReader);
    }

    protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
    public override ValueTask DisposeAsync() => default;
}
