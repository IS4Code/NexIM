using System.Collections.Generic;
using System.Threading.Tasks;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.App.Configuration;

[ComplexType]
public interface IServerHandler : IPayloadHandler
{
    [Name("Database")]
    ValueTask<IDatabaseHandler> Database();

    [Name("Certificates")]
    ValueTask<ICertificatesHandler> Certificates();

    [Name("XMPP")]
    ValueTask<IXmppHandler> Xmpp();

    [Name("Metadata")]
    ValueTask<IMetadataHandler> Metadata();
}

[ComplexType]
public interface IServiceHandler : IPayloadHandler
{
    [Name("Endpoint")]
    ValueTask Endpoint(IReadOnlyList<string>? endpoints);
}
