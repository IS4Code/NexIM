using System;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Xmpp.Protocol.Handlers;

namespace NexIM.Xmpp.Server.Handlers;

internal class GetTime : TimeHandler<ICommandContext>
{
    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unrecognized(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        await using var iq = await this.CreateResponse();
        await using var time = await iq.Time();

        var dateTime = DateTimeOffset.Now;

        await time.TimeZoneOffset(TimeZoneOffset.FromDateTimeOffset(dateTime));
        await time.UtcTime(dateTime.UtcDateTime);
    }
}
