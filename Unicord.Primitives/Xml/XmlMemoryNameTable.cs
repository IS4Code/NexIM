using System;
using System.Xml;

namespace NexIM.Primitives.Xml;

public abstract class XmlMemoryNameTable : XmlNameTable
{
    public abstract string Add(ReadOnlyMemory<char> memory);
    public abstract string? Get(ReadOnlyMemory<char> memory);

    public sealed override string Add(char[] array, int offset, int length)
    {
        return Add(array.AsMemory(offset, length));
    }

    public sealed override string Add(string str)
    {
        return Add(str.AsMemory());
    }

    public sealed override string? Get(char[] array, int offset, int length)
    {
        return Get(array.AsMemory(offset, length));
    }

    public sealed override string? Get(string str)
    {
        return Get(str.AsMemory());
    }
}
