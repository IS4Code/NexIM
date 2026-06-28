using System;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.App.Configuration;

[ComplexType]
public interface ITlsHandler : IPayloadHandler
{
    [Name("Certificate")]
    ValueTask<ICertificateHandler> Certificate([Name("Type")] Token<CertificateType>? type);
}

[ComplexType]
public interface ICertificateHandler : IServiceHandler
{
    [Name("RefreshAfter")]
    ValueTask RefreshAfter(TimeSpan? duration);

    [Name("CertificatePath")]
    ValueTask CertificatePath(string? path);

    [Name("KeyPath")]
    ValueTask KeyPath(string? path);

    [Name("Password")]
    ValueTask Password(TemporaryString? password);

    [Name("SubjectName")]
    ValueTask SubjectName(string? value);

    [Name("Issued")]
    ValueTask Issued(TimeSpan? duration);

    [Name("Expires")]
    ValueTask Expires(TimeSpan? duration);

    [Name("StoreName")]
    ValueTask StoreName(string? value);

    [Name("StoreLocation")]
    ValueTask StoreLocation(string? value);
}

[SimpleType]
public enum CertificateType
{
    [Name("SelfSigned")]
    SelfSigned,

    [Name("File")]
    File,

    [Name("Store")]
    Store,

    [Name("PEM")]
    Pem,

    [Name("PFX")]
    Pkcs12
}
