using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using NexIM.Tools;

namespace NexIM.Irc.Server.Communication;

/// <summary>
/// Provides an implementation of <see cref="IIrcSession"/> 
/// sending IRC commands via a <see cref="NetworkStream"/> instance.
/// </summary>
public abstract class IrcNetworkSession(NetworkStream networkStream) : IrcStreamSession(GetStream(networkStream))
{
    SslStream? sslStream;

    public sealed override bool Connected => networkStream.Socket.Connected;

    public sealed override bool IsSecure =>
        sslStream != null ||
        // Local connection is considered secure
        LocalEndPoint.SameAddressAs(RemoteEndPoint);

    const string socketException = "The socket is not bound";

    public sealed override EndPoint LocalEndPoint => networkStream.Socket.LocalEndPoint ?? throw new InvalidOperationException(socketException);
    public sealed override EndPoint RemoteEndPoint => networkStream.Socket.RemoteEndPoint ?? throw new InvalidOperationException(socketException);
    public sealed override X509Certificate? RemoteCertificate => sslStream?.RemoteCertificate;

    private static Stream GetStream(NetworkStream stream)
    {
#if DEBUG
        return new ConsoleDebuggingStream(stream, stream.Socket.RemoteEndPoint ?? (object)stream) {
            LineTerminator = null
        };
#else
        return stream;
#endif
    }
}
