using System;
using System.Text;
using System.Threading.Tasks;

namespace Unicord.Primitives;

/// <summary>
/// Provides a mutable string implementation whose contents can be determinstically
/// cleared from memory.
/// </summary>
public class TemporaryString : TemporaryArray<char>
{
    public TemporaryString(int capacity = 1, ITemporaryArraySource<char>? arraySource = null) : base(capacity, arraySource)
    {

    }

    private protected TemporaryString(TemporaryArray<char> original) : base(original)
    {

    }

    public static new TemporaryString MoveFrom(TemporaryArray<char> original)
    {
        return new(original);
    }
}

/// <summary>
/// Provides a mutable byte-encoded string implementation whose contents
/// can be determinstically cleared from memory.
/// </summary>
public class TemporaryEncodedString : TemporaryArray<char>
{
    readonly Encoding encoding;
    readonly ITemporaryArraySource<byte> byteArraySource;

    public TemporaryEncodedString(Encoding encoding, int capacity = 1, ITemporaryArraySource<char>? arraySource = null, ITemporaryArraySource<byte>? byteArraySource = null) : base(capacity, arraySource)
    {
        this.encoding = encoding;
        this.byteArraySource = byteArraySource ?? TemporaryArraySource<byte>.Shared;
    }

    private protected TemporaryEncodedString(Encoding encoding, ITemporaryArraySource<byte>? byteArraySource, TemporaryArray<char> original) : base(original)
    {
        this.encoding = encoding;
        this.byteArraySource = byteArraySource ?? (original as TemporaryEncodedString)?.byteArraySource ?? TemporaryArraySource<byte>.Shared;
    }

    private protected TemporaryEncodedString(Encoding encoding, TemporaryEncodedString original) : this(encoding, original.byteArraySource, original)
    {

    }

    private TemporaryEncodedString(TemporaryEncodedString original) : this(original.encoding, original.byteArraySource, original)
    {

    }

    // TODO Use direct streaming without a backing byte array

    public void ReadFrom<TArgs>(TemporaryArray<byte>.SynchronousReader<TArgs> reader, TArgs args)
    {
        using var array = new TemporaryArray<byte>(arraySource: byteArraySource);
        array.ReadFrom(reader, args);
        DecodeFrom(array.Value);
    }

    public async ValueTask ReadFromAsync<TArgs>(TemporaryArray<byte>.AsynchronousReader<TArgs> reader, TArgs args)
    {
        using var array = new TemporaryArray<byte>(arraySource: byteArraySource);
        await array.ReadFromAsync(reader, args);
        DecodeFrom(array.Value);
    }

    private void DecodeFrom(ArraySegment<byte> input)
    {
        var array = input.Array;
        var offset = input.Offset;
        var remaining = input.Count;

        var decoder = encoding.GetDecoder();
        ReadFrom((buffer, args) => {
            decoder.Convert(array, offset, remaining, buffer.Array, buffer.Offset, buffer.Count, true, out var bytesUsed, out var charsUsed, out var completed);

            if(remaining == 0 && charsUsed == 0 && !completed)
            {
                throw new InvalidOperationException("Decoding cannot be finished.");
            }

            offset += bytesUsed;
            remaining -= bytesUsed;

            return charsUsed;
        }, default(ValueTuple));
    }

    private void EncodeTo(TemporaryArray<byte> output)
    {
        var input = Value;

        var array = input.Array;
        var offset = input.Offset;
        var remaining = input.Count;

        var encoder = encoding.GetEncoder();
        output.ReadFrom((buffer, args) => {
            encoder.Convert(array, offset, remaining, buffer.Array, buffer.Offset, buffer.Count, true, out var charsUsed, out var bytesUsed, out var completed);

            if(remaining == 0 && bytesUsed == 0 && !completed)
            {
                throw new InvalidOperationException("Encoding cannot be finished.");
            }

            offset += charsUsed;
            remaining -= charsUsed;

            return charsUsed;
        }, default(ValueTuple));
    }

    public void WriteTo<TArgs>(TemporaryArray<byte>.SynchronousWriter<TArgs> writer, TArgs args)
    {
        using var array = new TemporaryArray<byte>(arraySource: byteArraySource);
        EncodeTo(array);
        array.WriteTo(writer, args);
    }

    public async ValueTask WriteToAsync<TArgs>(TemporaryArray<byte>.AsynchronousWriter<TArgs> writer, TArgs args)
    {
        using var array = new TemporaryArray<byte>(arraySource: byteArraySource);
        EncodeTo(array);
        await array.WriteToAsync(writer, args);
    }

    public static TemporaryEncodedString MoveFrom(TemporaryArray<char> original, Encoding encoding, ITemporaryArraySource<byte>? byteArraySource = null)
    {
        return new(encoding, byteArraySource, original);
    }

    public static TemporaryEncodedString MoveFrom(TemporaryEncodedString original, Encoding encoding)
    {
        return new(encoding, original);
    }

    public static TemporaryEncodedString MoveFrom(TemporaryEncodedString original)
    {
        return new(original);
    }
}

/// <summary>
/// Provides a mutable UTF-8-encoded string implementation whose contents
/// can be determinstically cleared from memory.
/// </summary>
public class TemporaryUtf8String : TemporaryEncodedString
{
    static readonly Encoding encoding = new UTF8Encoding(false);

    public TemporaryUtf8String(int capacity = 1, ITemporaryArraySource<char>? arraySource = null) : base(encoding, capacity, arraySource)
    {

    }

    private protected TemporaryUtf8String(ITemporaryArraySource<byte>? byteArraySource, TemporaryArray<char> original) : base(encoding, byteArraySource, original)
    {

    }

    private protected TemporaryUtf8String(TemporaryEncodedString original) : base(encoding, original)
    {

    }

    public static TemporaryUtf8String MoveFrom(TemporaryArray<char> original, ITemporaryArraySource<byte>? byteArraySource = null)
    {
        return new(byteArraySource, original);
    }

    public static new TemporaryUtf8String MoveFrom(TemporaryEncodedString original)
    {
        return new(original);
    }
}
