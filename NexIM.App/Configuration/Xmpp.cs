using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.App.Configuration;

[ComplexType]
public interface IXmppHandler : IPayloadHandler
{
    [Name("TCP")]
    ValueTask<IXmppTcpHandler> Tcp();

    [Name("WebSocket")]
    ValueTask<IXmppWebSocketHandler> WebSocket();

    [Name("HTML")]
    ValueTask<IXmppHtmlHandler> Html();
}

[ComplexType]
public interface IXmppTcpHandler : IServiceHandler
{

}

[ComplexType]
public interface IXmppWebSocketHandler : IServiceHandler
{

}

[ComplexType]
public interface IXmppHtmlHandler : IServiceHandler
{
    [Name("Title")]
    ValueTask Title(string? title);

    [Name("Converse")]
    ValueTask ConverseDistribution(string? url);

    [Name("Language")]
    ValueTask Language(LanguageCode? language);
}
