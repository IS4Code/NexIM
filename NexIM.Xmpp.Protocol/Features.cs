using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Protocol;

[ComplexType]
public interface IFeaturesHandler : ICapabilitiesHandler
{
    [Name("auth", "http://jabber.org/features/iq-auth")]
    ValueTask IqAuth();

    [Name("register", "http://jabber.org/features/iq-register")]
    ValueTask IqRegister();

    [Name("starttls", XmppTls)]
    ValueTask<ITlsFeaturesHandler> StartTls();

    [Name("compression", FeaturesCompress)]
    ValueTask<ICompressionFeaturesHandler> Compression();

    [Name("ver", "urn:xmpp:features:rosterver")]
    ValueTask RosterVersion();

    [Name("sub", "urn:xmpp:features:pre-approval")]
    ValueTask PreApproval();

    [Name("bind", XmppBind)]
    ValueTask Bind();

    [Name("session", XmppSession)]
    ValueTask<ISessionFeaturesHandler> Session();

    [Name("mechanisms", XmppSasl)]
    ValueTask<ISaslMechanismsHandler> SaslMechanisms();
}

[ComplexType, Namespace(XmppTls)]
public interface ITlsFeaturesHandler : IPayloadHandler
{
    [Name("required")]
    ValueTask Required();
}

[ComplexType, Namespace(FeaturesCompress)]
public interface ICompressionFeaturesHandler : IPayloadHandler
{
    [Name("method")]
    ValueTask Method(Token<CompressionMethod>? name);
}

[ComplexType, Namespace(XmppSession)]
public interface ISessionFeaturesHandler : IPayloadHandler
{
    [Name("optional")]
    ValueTask Optional();
}
