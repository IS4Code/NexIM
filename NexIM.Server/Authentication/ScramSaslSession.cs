using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Server.Tools;

namespace NexIM.Server.Authentication;

/// <summary>
/// Represents a SASL session using the SCRAM mechanisms.
/// </summary>
sealed class ScramSaslSession(NexServer server, Func<string, AccountName> usernameResolver, HashAlgorithmName algorithm) : SaslSession
{
    static readonly Dictionary<HashAlgorithmName, HashInfo> hashInfos = new() {
        { HashAlgorithmName.SHA1, new(SHA1.HashSizeInBytes, SHA1.HashData, HMACSHA1.HashData) },
        { HashAlgorithmName.SHA256, new(SHA256.HashSizeInBytes, SHA256.HashData, HMACSHA256.HashData) }
    };

    readonly HashInfo hashInfo = hashInfos[algorithm];

    protected async override IAsyncEnumerator<SaslResponse> Run()
    {
        if(CurrentResponse is not { } initialResponse)
        {
            // Ask for response
            yield return SaslResponse.Challenge(null);
            initialResponse = CurrentResponse;
        }
        if(initialResponse == null)
        {
            yield return SaslResponse.Failure(SaslStatus.InvalidRequest);
            yield break;
        }

        // Take ownership
        using var clientFirstPayload = TemporaryUtf8String.MoveFrom(initialResponse);

        if(ParseClientFirstMessage(clientFirstPayload.Value.AsMemory()) is not { } clientFirstMessage)
        {
            yield return SaslResponse.Failure(SaslStatus.InvalidRequest);
            yield break;
        }

        var accountName = usernameResolver(clientFirstMessage.Username);

        if(clientFirstMessage.Authzid is { } authzid && !accountName.Matches(authzid))
        {
            yield return SaslResponse.Failure(SaslStatus.AuthorizationFailed);
            yield break;
        }

        var account = accountName.IsUser ? await server.GetAccount(accountName) : null;

        if(!GetPbkdf2Info(accountName, account?.PasswordHash, out var salt, out var iterations, out var saltedPassword))
        {
            // Not supported for this account
            yield return SaslResponse.Failure(SaslStatus.MechanismTooWeak);
            yield break;
        }

        if(saltedPassword.Length == 0)
        {
            // Fake information
            account = null;
        }

        // Owned until the end since it contains the full nonce
        using var serverPayload = new TemporaryUtf8String(18 + clientFirstMessage.ClientNonce.Length + (serverNonceLength + 2) / 3 * 4 + (salt.Length + 2) / 3 * 4);
        
        serverPayload.Append("r=");
        int nonceStart = serverPayload.Length;
        serverPayload.Append(clientFirstMessage.ClientNonce);
        GenerateNonce(serverPayload);
        int nonceEnd = serverPayload.Length;

        serverPayload.Append(",s=");
        serverPayload.FillFrom(base64Reader, salt.Span, default);
        serverPayload.Append(",i=");
        serverPayload.FillFrom(integerReader, iterations);

        var nonce = serverPayload.Value.AsMemory(nonceStart, nonceEnd - nonceStart);

        yield return SaslResponse.Challenge(serverPayload);

        if(CurrentResponse is not { } finalResponse)
        {
            yield return SaslResponse.Failure(SaslStatus.InvalidRequest);
            yield break;
        }

        // Avoid hashing on original thread
        await Task.Yield();

        // No need to take ownership since there is no yield during verification
        if(VerifyClientFinal(finalResponse.Value.AsMemory(), out var failure) is not { } serverSignature)
        {
            yield return SaslResponse.Failure(failure);
            yield break;
        }

        using(serverSignature)
        {
            // Reuse buffer
            serverPayload.Clear();
            serverPayload.Reserve(2 + (serverSignature.Length + 2) / 3 * 4);
            serverPayload.Append("v=");
            serverPayload.FillFrom(base64Reader, serverSignature.Value.AsSpan(), default);
        }

        yield return SaslResponse.Success(account!, serverPayload);

        TemporaryArray<byte>? VerifyClientFinal(ReadOnlyMemory<char> text, out SaslStatus failure)
        {
            failure = SaslStatus.InvalidRequest;

            if(ParseClientFinalMessage(text) is not { } clientFinalMessage)
            {
                return null;
            }

            if(clientFinalMessage.ProofBase64.Length != (hashInfo.HashSize + 2) / 3 * 4)
            {
                // Client proof length does not match
                return null;
            }

            if(!clientFinalMessage.Nonce.Equals(nonce.Span, StringComparison.Ordinal))
            {
                // Nonce does not match
                return null;
            }

            // Verify GS2 header
            int gs2Utf8Length = Encoding.UTF8.GetByteCount(clientFirstMessage.Gs2Header);
            int gs2Base64Length = clientFinalMessage.ChannelBindingBase64.Length;

            if((gs2Utf8Length + 2) / 3 * 4 != gs2Base64Length)
            {
                // Expected base64 length does not match
                return null;
            }

            // Allocate contiguous buffer to compare both halves
            int allocated = 2 * gs2Utf8Length;

            const int stackLimit = 512;

            var pool = ArrayPool<byte>.Shared;
            byte[]? rented = null;
            Span<byte> buffer = allocated < stackLimit ? stackalloc byte[allocated] : (rented = pool.Rent(allocated)).AsSpan(0, allocated);
            try
            {
                var expected = buffer.Slice(gs2Utf8Length);
                if(!Convert.TryFromBase64Chars(clientFinalMessage.ChannelBindingBase64, expected, out var written) || written != gs2Utf8Length)
                {
                    // Does not fit - different contents
                    return null;
                }

                var value = buffer.Slice(0, gs2Utf8Length);
                if(!Encoding.UTF8.TryGetBytes(clientFirstMessage.Gs2Header, value, out _))
                {
                    // Something wrong with the length
                    return null;
                }

                if(!value.SequenceEqual(expected))
                {
                    // Different contents
                    return null;
                }
            }
            finally
            {
                if(rented != null)
                {
                    pool.Return(rented);
                }
            }

            Span<byte> clientSignatureBuffer = stackalloc byte[hashInfo.HashSize * 2];
            var clientProof = clientSignatureBuffer.Slice(hashInfo.HashSize);
            if(!Convert.TryFromBase64Chars(clientFinalMessage.ProofBase64, clientProof, out var proofLength) || proofLength != hashInfo.HashSize)
            {
                // Invalid proof
                return null;
            }

            // All well-formed - reject if required
            failure = SaslStatus.AuthenticationFailed;
            if(saltedPassword.Length == 0 || account == null)
            {
                return null;
            }

            Span<byte> clientKey = stackalloc byte[hashInfo.HashSize];
            hashInfo.Hmac(saltedPassword.Span, "Client Key"u8, clientKey);

            Span<byte> storedKey = stackalloc byte[hashInfo.HashSize];
            hashInfo.Hash(clientKey, storedKey);

            // Include server key into the allocation to make it contiguous
            // Server message has only ASCII characters
            allocated = hashInfo.HashSize + 2 + Encoding.UTF8.GetByteCount(clientFirstMessage.Bare) + serverPayload.Length + Encoding.UTF8.GetByteCount(clientFinalMessage.WithoutProof);

            rented = null;
            buffer = allocated < stackLimit ? stackalloc byte[allocated] : (rented = pool.Rent(allocated)).AsSpan(0, allocated);
            try
            {
                // Form auth message from individual strings
                var authMessage = buffer.Slice(hashInfo.HashSize);
                if(!Encoding.UTF8.TryGetBytes(clientFirstMessage.Bare, authMessage, out var pos))
                {
                    return null;
                }
                authMessage[pos++] = (byte)',';
                if(!Encoding.UTF8.TryGetBytes(serverPayload.Value.AsSpan(), authMessage.Slice(pos), out var end))
                {
                    return null;
                }
                pos += end;
                authMessage[pos++] = (byte)',';
                if(!Encoding.UTF8.TryGetBytes(clientFinalMessage.WithoutProof, authMessage.Slice(pos), out end))
                {
                    return null;
                }
                pos += end;
                buffer = buffer.Slice(0, hashInfo.HashSize + pos);
                authMessage = authMessage.Slice(0, pos);

                var serverKey = buffer.Slice(0, hashInfo.HashSize);

                var clientSignature = clientSignatureBuffer.Slice(0, hashInfo.HashSize);
                hashInfo.Hmac(storedKey, authMessage, clientSignature);

                XorBuffer(clientProof, clientSignature);

                Span<byte> recoveredStoredKey = stackalloc byte[hashInfo.HashSize];
                hashInfo.Hash(clientProof, recoveredStoredKey);
                if(!CryptographicOperations.FixedTimeEquals(recoveredStoredKey, storedKey))
                {
                    return null;
                }

                hashInfo.Hmac(saltedPassword.Span, "Server Key"u8, serverKey);

                var result = new TemporaryArray<byte>(hashInfo.HashSize);
                try
                {
                    // Buffer is server key + auth message
                    result.FillFrom(hmacReader, buffer, hashInfo);
                    return result;
                }
                catch when(Cleanup())
                {
                    throw;
                }

                bool Cleanup()
                {
                    result.Dispose();
                    return false;
                }
            }
            finally
            {
                if(rented != null)
                {
                    pool.Return(rented);
                }
            }
        }
    }

    static readonly int randomSeed = new Random().Next();
    bool GetPbkdf2Info(AccountName accountName, byte[]? passwordHash, out ReadOnlyMemory<byte> salt, out int iterations, out ReadOnlyMemory<byte> saltedPassword)
    {
        if(passwordHash != null)
        {
            // Existing information
            return PasswordHasher.ExtractPbkdf2Info(passwordHash, algorithm, out salt, out iterations, out saltedPassword);
        }

        // Provide fake information to prevent account detection
        saltedPassword = default;

        if(!PasswordHasher.GetDefaultPbkdf2Info(algorithm, out var saltLength, out iterations))
        {
            // Not the default algorithm
            salt = default;
            return false;
        }

        // Fill with deterministic data
        var data = new byte[saltLength];
        new Random(accountName.GetHashCode() ^ randomSeed).NextBytes(data);
        salt = data;
        return true;
    }

    static void XorBuffer(Span<byte> target, ReadOnlySpan<byte> other)
    {
        var intTarget = MemoryMarshal.Cast<byte, int>(target);
        var intOther = MemoryMarshal.Cast<byte, int>(other);
        for(int i = 0; i < intTarget.Length; i++)
        {
            intTarget[i] ^= intOther[i];
        }

        for(int i = intTarget.Length * sizeof(int); i < target.Length; i++)
        {
            target[i] ^= other[i];
        }
    }

    const int serverNonceLength = 18;
    static void GenerateNonce(TemporaryArray<char> output)
    {
        Span<byte> bytes = stackalloc byte[serverNonceLength];
        try
        {
            RandomNumberGenerator.Fill(bytes);
            output.FillFrom(base64Reader, bytes, default);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    static readonly TemporaryArray<char>.SynchronousReadOnlySpanReader<byte, ValueTuple> base64Reader = (buffer, span, _) => {
        if(!Convert.TryToBase64Chars(span, buffer.AsSpan(), out var written))
        {
            throw new ArgumentException("The output buffer has wrong capacity.", nameof(buffer));
        }
        return written;
    };

    static readonly TemporaryArray<char>.SynchronousReader<int> integerReader = (buffer, value) => {
        if(!value.TryFormat(buffer.AsSpan(), out var written))
        {
            throw new ArgumentException("The output buffer has wrong capacity.", nameof(buffer));
        }
        return written;
    };

    static readonly TemporaryArray<byte>.SynchronousReadOnlySpanReader<byte, HashInfo> hmacReader = (buffer, span, hashInfo) => {
        int hashSize = hashInfo.HashSize;
        var key = span.Slice(0, hashSize);
        var source = span.Slice(hashSize);
        hashInfo.Hmac(key, source, buffer.AsSpan(0, hashSize));
        return hashSize;
    };

    [StructLayout(LayoutKind.Auto)]
    readonly struct ClientFirstMessageData(
        ReadOnlyMemory<char> text,
        Range gs2Header,
        Range bare,
        string? authzid,
        string username,
        Range clientNonce
    )
    {
        ReadOnlySpan<char> span => text.Span;

        public ReadOnlySpan<char> Gs2Header => span[gs2Header];
        public ReadOnlySpan<char> Bare => span[bare];

        public string? Authzid => authzid;
        public string Username => username;
        public ReadOnlySpan<char> ClientNonce => span[clientNonce];
    }

    static ClientFirstMessageData? ParseClientFirstMessage(ReadOnlyMemory<char> text)
    {
        var span = text.Span;
        if(span.Length < 3 || span[0] == 'p')
        {
            // Channel binding not supported
            return null;
        }
        if((span[0] != 'n' && span[0] != 'y') || span[1] != ',')
        {
            // Invalid message
            return null;
        }

        const int fieldsStart = 3;
        var parser = new FieldParser(span, fieldsStart);

        if(!parser.ReadField(out var type, out var range))
        {
            return null;
        }

        Range gs2Header, clientFirstMessageBare;
        string? authzid;
        if(type == 'a')
        {
            // Authzid present
            if(range.Start.Equals(range.End))
            {
                // Empty is invalid
                return null;
            }

            authzid = Unescape(span[range]);
            if(authzid == null)
            {
                // Bad escaping
                return null;
            }
            gs2Header = 0..parser.Position;
            clientFirstMessageBare = parser.Position..;

            // Must be followed by another field
            if(!parser.ReadField(out type, out range))
            {
                return null;
            }
        }
        else
        {
            // GS2 header is just <flag>,,
            gs2Header = 0..fieldsStart;
            clientFirstMessageBare = fieldsStart..;
            authzid = null;
        }

        if(type == 'm')
        {
            // Mandatory extensions not supported
            return null;
        }

        if(type != 'n' || range.Start.Equals(range.End))
        {
            // Invalid username
            return null;
        }

        if(Unescape(span[range]) is not { } username)
        {
            // Bad escaping
            return null;
        }

        if(!parser.ReadNonEmptyField('r', out range))
        {
            // Invalid nonce
            return null;
        }

        var nonce = range;

        // Ignore optional extensions

        return new ClientFirstMessageData(text, gs2Header, clientFirstMessageBare, authzid, username, nonce);
    }

    [StructLayout(LayoutKind.Auto)]
    readonly struct ClientFinalMessageData(
        ReadOnlyMemory<char> text,
        Range withoutProof,
        Range channelBindingBase64,
        Range nonce,
        Range proofBase64
    )
    {
        ReadOnlySpan<char> span => text.Span;

        public ReadOnlySpan<char> WithoutProof => span[withoutProof];

        public ReadOnlySpan<char> ChannelBindingBase64 => span[channelBindingBase64];
        public ReadOnlySpan<char> Nonce => span[nonce];
        public ReadOnlySpan<char> ProofBase64 => span[proofBase64];
    }

    static ClientFinalMessageData? ParseClientFinalMessage(ReadOnlyMemory<char> text)
    {
        var parser = new FieldParser(text.Span, 0);

        if(!parser.ReadNonEmptyField('c', out var channelBindingBase64))
        {
            // Invalid channel binding
            return null;
        }

        if(!parser.ReadNonEmptyField('r', out var nonce))
        {
            // Invalid nonce
            return null;
        }

        var proofPos = parser.Position;
        Range proof = default;
        while(parser.ReadField(out var type, out var range))
        {
            // Skip optional extensions

            if(type == 'p')
            {
                // Proof must come last
                proof = range;
            }
            else
            {
                // Capture before reading next field
                proofPos = parser.Position;
            }
        }

        if(!proof.End.Equals(new Index(text.Length)))
        {
            // Proof not present or followed by other fields
            return null;
        }

        var withoutProof = 0..(proofPos - 1);
        return new ClientFinalMessageData(text, withoutProof, channelBindingBase64, nonce, proof);
    }

    static string? Unescape(ReadOnlySpan<char> escaped)
    {
        int pos = escaped.IndexOf('=');
        if(pos == -1)
        {
            return escaped.ToString();
        }

        // Bounded by original length
        var builder = new StringBuilder(escaped.Length);

        int start = 0;
        while(true)
        {
            if(pos + 2 >= escaped.Length)
            {
                // Invalid escape
                return null;
            }

            builder.Append(escaped[start..pos]);
            switch((escaped[pos + 1], escaped[pos + 2]))
            {
                case ('2', 'C'):
                    builder.Append(',');
                    break;
                case ('3', 'D'):
                    builder.Append('=');
                    break;
                default:
                    // Invalid escape
                    return null;
            }

            // Find next after escape
            start = pos + 3;
            pos = escaped.Slice(start).IndexOf('=');
            if(pos == -1)
            {
                break;
            }
            pos += start;
        }
        builder.Append(escaped.Slice(start));

        return builder.ToString();
    }

    [StructLayout(LayoutKind.Auto)]
    ref struct FieldParser
    {
        readonly ReadOnlySpan<char> span;
        public int Position { get; private set; }
        
        public FieldParser(ReadOnlySpan<char> span, int position)
        {
            this.span = span;
            Position = position;
        }

        public bool ReadField(out char type, out Range range)
        {
            if(Position + 1 >= span.Length || span[Position + 1] != '=')
            {
                // No field
                type = default;
                range = default;
                return false;
            }

            type = span[Position];

            Position += 2;
            int end = span.Slice(Position).IndexOf(',');
            if(end == -1)
            {
                end = span.Length;
            }
            else
            {
                end += Position;
            }

            range = Position..end;
            Position = end + 1;
            return true;
        }

        public bool ReadNonEmptyField(out char type, out Range range)
        {
            return ReadField(out type, out range) && !range.Start.Equals(range.End);
        }

        public bool ReadField(char type, out Range range)
        {
            return ReadField(out var readType, out range) && readType == type;
        }

        public bool ReadNonEmptyField(char type, out Range range)
        {
            return ReadField(type, out range) && !range.Start.Equals(range.End);
        }
    }

    delegate int HashFunc(ReadOnlySpan<byte> source, Span<byte> destination);
    delegate int HmacFunc(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination);

    sealed record HashInfo(
        int HashSize,
        HashFunc HashFunc,
        HmacFunc HmacFunc
    )
    {
        public void Hmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if(HmacFunc(key, source, destination) != HashSize)
            {
                throw new ArgumentException("Destination is not properly sized.", nameof(destination));
            }
        }

        public void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if(HashFunc(source, destination) != HashSize)
            {
                throw new ArgumentException("Destination is not properly sized.", nameof(destination));
            }
        }
    }
}
