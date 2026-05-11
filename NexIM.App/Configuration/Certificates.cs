using System;
using System.Threading.Tasks;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.App.Configuration;

[ComplexType]
public interface ICertificatesHandler : IPayloadHandler
{
    [Name("SelfSigned")]
    ValueTask<ISelfSignedHandler> SelfSigned();
}

[ComplexType]
public interface ISelfSignedHandler : IServiceHandler
{
    [Name("SubjectName")]
    ValueTask SubjectName(string? value);

    [Name("Expires")]
    ValueTask Expires(TimeSpan? duration);
}
