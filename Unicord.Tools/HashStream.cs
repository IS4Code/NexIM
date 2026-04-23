using System;
using System.Security.Cryptography;

namespace Unicord.Tools;

public class HashStream(HashAlgorithm algorithm) : NonSeekableStream
{
    public override bool CanRead => false;
    public override bool CanWrite => true;

    public int HashSize => (algorithm.HashSize + 7) / 8;

    public override void Write(byte[] buffer, int offset, int count)
    {
        algorithm.TransformBlock(buffer, offset, count, buffer, offset);
    }

    public Span<byte> ComputeHash(Span<byte> output)
    {
        // Still uses the previous data
        if(!algorithm.TryComputeHash(default, output, out var length))
        {
            throw new ArgumentException("The buffer has insufficient size.", nameof(output));
        }
        return output.Slice(0, length);
    }

    public override void Flush()
    {

    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}
