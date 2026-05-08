using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NexIM.Primitives;

namespace NexIM.Server.Tools;

internal static class PasswordHasher
{
    const int headerSize = 2;
    static readonly HashInfo defaultHashInfo = new(HashAlgorithmName.SHA256, 600000, 32, 32);

    public static async ValueTask<byte[]> HashPassword(ReadOnlyMemory<char> password)
    {
        await Task.Yield();
        return HashPasswordCore(password.Span);
    }

    private static byte[] HashPasswordCore(ReadOnlySpan<char> password)
    {
        var hashInfo = defaultHashInfo;
        var result = new byte[headerSize + hashInfo.SaltAndKeyLength];

        // Write header
        result[0] = hashInfo.HashAlgorithmIndexAndSaltRatio;
        result[1] = hashInfo.IterationsPacked;

        // Prepare salt and key
        var salt = result.AsSpan(headerSize, hashInfo.SaltLength);
        var key = result.AsSpan(headerSize + salt.Length);
        RandomNumberGenerator.Fill(salt);

        // Hash
        Pbkdf2(password, salt, key, hashInfo);
        return result;
    }

    public static async ValueTask<VerificationResult> VerifyPassword(ReadOnlyMemory<char> password, ReadOnlyMemory<byte> hash)
    {
        await Task.Yield();
        return VerifyPasswordCore(password.Span, hash.Span);
    }

    private static VerificationResult VerifyPasswordCore(ReadOnlySpan<char> password, ReadOnlySpan<byte> hash)
    {
        if(hash.Length < headerSize)
        {
            // Invalid data
            return VerificationResult.NotVerified;
        }

        // Read header
        var hashInfo = new HashInfo(hash[0], hash[1], hash.Length - headerSize);

        // Get salt and key
        var salt = hash.Slice(headerSize, hashInfo.SaltLength);
        var expectedKey = hash.Slice(headerSize + salt.Length);

        // Hash and compare
        Span<byte> key = stackalloc byte[expectedKey.Length];
        Pbkdf2(password, salt, key, hashInfo);

        if(!CryptographicOperations.FixedTimeEquals(expectedKey, key))
        {
            return VerificationResult.NotVerified;
        }
        return IsSufficientlyStrong(hashInfo, defaultHashInfo) ? VerificationResult.Verified : VerificationResult.VerifiedWeak;
    }

    private static bool IsSufficientlyStrong(HashInfo info, HashInfo defaultInfo)
    {
        var hashIndex = info.HashAlgorithmIndex;
        var defaultHashIndex = defaultInfo.HashAlgorithmIndex;
        if(hashIndex < defaultHashIndex)
        {
            // Must upgrade algorithm
            return false;
        }
        if(info.SaltAndKeyLength < defaultInfo.SaltAndKeyLength || info.SaltLength < defaultInfo.SaltLength)
        {
            // Key or salt is too short
            return false;
        }
        int minimumIterationsPacked = defaultInfo.IterationsPacked;
        if(hashIndex > defaultHashIndex)
        {
            // Better hash, estimate how many iterations are sufficient
            const int iterationLevelsPerHash = 12;

            int allowedDifference = (hashIndex - defaultHashIndex) * iterationLevelsPerHash;
            minimumIterationsPacked -= allowedDifference;
        }
        if(info.IterationsPacked < minimumIterationsPacked)
        {
            // Insufficient iterations
            return false;
        }
        return true;
    }

    static readonly Encoding encoding = new UTF8Encoding(false);
    private static void Pbkdf2(ReadOnlySpan<char> password, ReadOnlySpan<byte> salt, Span<byte> key, HashInfo info)
    {
        // Convert to UTF-8
        int byteCount = encoding.GetByteCount(password);
        if(byteCount < 256)
        {
            // Okay to put on the stack
            Span<byte> bytes = stackalloc byte[byteCount];
            try
            {
                bytes = bytes.Slice(0, encoding.GetBytes(password, bytes));

                Rfc2898DeriveBytes.Pbkdf2(bytes, salt, key, info.Iterations, info.HashAlgorithm);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
        else
        {
            using var str = new TemporaryUtf8String(password.Length);
            str.Append(password);
            using var bytes = new TemporaryArray<byte>(password.Length);
            str.WriteTo(static (segment, array) => array.Append(segment.AsSpan()), bytes);
            str.Clear();

            Rfc2898DeriveBytes.Pbkdf2(bytes.Value.AsSpan(), salt, key, info.Iterations, info.HashAlgorithm);
        }
    }

    public enum VerificationResult
    {
        NotVerified,
        Verified,
        VerifiedWeak
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly record struct HashInfo(byte HashAlgorithmIndexAndSaltRatio, byte IterationsPacked, int SaltAndKeyLength)
    {
        static readonly HashAlgorithmName[] hashAlgorithms = {
            default,
            HashAlgorithmName.SHA1,
            HashAlgorithmName.SHA256,
            HashAlgorithmName.SHA384,
            HashAlgorithmName.SHA512
        };

        public int Iterations => UnpackByte(IterationsPacked);

        public byte HashAlgorithmIndex => (byte)(HashAlgorithmIndexAndSaltRatio & 0xF);
        public HashAlgorithmName HashAlgorithm => hashAlgorithms[HashAlgorithmIndex];

        public byte SaltRatio => (byte)(HashAlgorithmIndexAndSaltRatio >> 4);
        public int SaltLength => SaltAndKeyLength * SaltRatio / 16;
        public int KeyLength => SaltAndKeyLength - SaltLength;

        public HashInfo(HashAlgorithmName hashAlgorithm, int iterations, int saltLength, int derivedLength) : this(
            (byte)(GetHashAlgorithmIndex(hashAlgorithm) | (GetSaltLengthRatio(saltLength, derivedLength) << 4)),
            PackByte(iterations),
            saltLength + derivedLength
        )
        {
            if(SaltLength != saltLength)
            {
                throw new ArgumentException($"The salt length is not directly representable in the structure. The closest length is {SaltLength}.", nameof(saltLength));
            }
        }

        static int GetHashAlgorithmIndex(HashAlgorithmName hashAlgorithm)
        {
            int index = Array.IndexOf(hashAlgorithms, hashAlgorithm);
            if(index <= 0)
            {
                throw new ArgumentException($"The hash algorithm is not supported. Supported algorithms are {String.Join(", ", hashAlgorithms.Where(alg => alg != default).Select(alg => alg.Name))}.", nameof(hashAlgorithm));
            }
            return index;
        }

        static int GetSaltLengthRatio(int saltLength, int derivedLength)
        {
            var ratio = (double)saltLength / (saltLength + derivedLength);
            return (int)Math.Round(ratio * 16);
        }

        /// <summary>
        /// The solution to <c>b^255 / ln b = e * 2147483647</c>.
        /// </summary>
        const double b = 1.081330319590427076540517477379461892785593229;

        /// <summary>
        /// The solution to <c>255 + a = log_b 2147483647</c>
        /// </summary>
        const double a = 19.80491566773791143619042416251321645127131876;

        /// <summary>
        /// Approximation of <c>1 / ln b</c>.
        /// </summary>
        const int c = 15;

        static byte PackByte(int x)
        {
            if(x <= c)
            {
                return checked((byte)x);
            }
            return checked((byte)Math.Round(Math.Log(x, b) - a));
        }

        static int UnpackByte(byte x)
        {
            if(x <= c)
            {
                return x;
            }
            return (int)Math.Round(Math.Pow(b, x + a));
        }
    }
}
