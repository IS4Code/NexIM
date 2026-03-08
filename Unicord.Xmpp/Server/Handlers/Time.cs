using System;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Xmpp.Protocol.Handlers;

namespace Unicord.Xmpp.Server.Handlers;

internal class GetTime : TimeHandler, ICommandHandler
{
    public CommandState State { get; init; }

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
