using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NexIM.Irc.Protocol;
using NexIM.Primitives;
using NexIM.Server.Authentication;

namespace NexIM.Irc.Server.Commands;

partial class Stream
{
    const int authChunkSize = 400;
    TemporaryUtf8String? authBuffer;

    static readonly bstring saslPlus = "+"u8.ToArray();
    private ValueTask AuthenticateCommand(in CommandMessage message)
    {
        var command = message.Command;
        if(message.Parameters.Count < 1)
        {
            return SendReply(MessageReply.NeedMoreParams, command, Texts.NotEnoughParameters);
        }
        var session = this.GetSession();

        if(session.IsAuthenticated)
        {
            return SendReply(MessageReply.SaslAlready, Texts.SaslAlready);
        }

        var token = message.Parameters[0];

        if(session.SaslSession is not { } saslSession)
        {
            var mechanismName = Encoding.GetString(token);

            if(this.GetServer().CreateSaslSession(mechanismName, FormAccountName, session) is not { } newSession)
            {
                return SendReply(MessageReply.SaslFail, Texts.SaslFail);
            }

            // SASL session created
            session.SaslSession = newSession;
            authBuffer = new();

            // Prompt for initial response
            return SendCommand(command, saslPlus);
        }

        var encoded = token.AsSpan();
        
        if(encoded.Length > authChunkSize)
        {
            return SendReply(MessageReply.SaslTooLong, Texts.SaslTooLong);
        }

        if(encoded.Length == 1)
        {
            switch(encoded[0])
            {
                case (byte)'*':
                    // Aborted
                    return SaslAbort();

                case (byte)'+':
                    // Empty or finish
                    encoded = ReadOnlySpan<byte>.Empty;
                    break;
            }
        }

        // Limited to 300
        Span<byte> decoded = stackalloc byte[Base64.GetMaxDecodedFromUtf8Length(encoded.Length)];
        try
        {
            if(Base64.DecodeFromUtf8(encoded, decoded, out _, out var decodedLength) != System.Buffers.OperationStatus.Done)
            {
                return SendReply(MessageReply.SaslFail, Texts.SaslFail);
            }
            decoded = decoded.Slice(0, decodedLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
        }

        (authBuffer ??= new()).Append(decoded);

        if(encoded.Length == authChunkSize)
        {
            // A full chunk is followed by more
            return default;
        }

        return Response();
        async ValueTask Response()
        {
            SaslResponse response;
            try
            {
                response = await saslSession.Response(authBuffer);
            }
            finally
            {
                // Cleanup
                authBuffer.Dispose();
            }
            
            try
            {
                switch(response.Status)
                {
                    case SaslStatus.Challenge:
                        await SendChallenge(response.ChallengeData!);
                        return;

                    case SaslStatus.Success:
                        var account = response.Account!;
                        // No way of sending success data
                        response.ChallengeData?.Dispose();
                        if(!await Login(account))
                        {
                            await SendReply(MessageReply.SaslFail, Texts.SaslFail);
                            return;
                        }
                        // Client session is assigned but remote address not changed until registration finishes
                        await SendReply(MessageReply.LoggedIn, BuildRemoteAddress().Value, encoder.Get(account.Name.ToString()), $"You are now logged in as {account.Name.User ?? ""}");
                        await SendReply(MessageReply.SaslSuccess, Texts.SaslSuccess);
                        return;

                    default:
                        await SendReply(MessageReply.SaslFail, Texts.SaslFail);
                        return;
                }
            }
            finally
            {
                if(response.Status != SaslStatus.Challenge)
                {
                    // Final status
                    await SaslStop();
                }
            }
        }

        async ValueTask SaslAbort()
        {
            await SaslStop();
            await SendReply(MessageReply.SaslAborted, Texts.SaslAborted);
        }

        async ValueTask SaslStop()
        {
            session.SaslSession = null;
            authBuffer?.Dispose();
            authBuffer = null;
            await saslSession.DisposeAsync();
        }

        async ValueTask SendChallenge(TemporaryUtf8String data)
        {
            using var buffer = new TemporaryArray<byte>(capacity: Base64.GetMaxEncodedToUtf8Length(Encoding.UTF8.GetMaxByteCount(data.Length)));
            buffer.FillFrom(utf8ToBase64, data);
            bstring base64 = buffer.Value;

            while(base64.Length is > 0 and var length)
            {
                if(length >= authChunkSize)
                {
                    // Split into chunk
                    await SendCommand(command, base64.Slice(0, authChunkSize));
                    base64 = base64.Slice(authChunkSize);
                }
                else
                {
                    // Final chunk
                    await SendCommand(command, base64);
                    return;
                }
            }
            // Empty or multiple of chunk size
            await SendCommand(command, saslPlus);
        }
    }

    static readonly TemporaryArray<byte>.SynchronousReader<TemporaryUtf8String> utf8ToBase64 = (buffer, source) => {
        var span = source.Value.AsSpan();
        var output = buffer.AsSpan();
        if(!Encoding.UTF8.TryGetBytes(span, output, out var written))
        {
            throw new NotSupportedException();
        }
        if(Base64.EncodeToUtf8InPlace(output, written, out written) != System.Buffers.OperationStatus.Done)
        {
            throw new NotSupportedException();
        }
        return written;
    };
}
