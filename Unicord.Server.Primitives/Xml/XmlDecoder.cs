using System;
using System.Threading.Tasks;
using System.Xml;

namespace Unicord.Server.Primitives.Xml;

/// <summary>
/// Provides support for decoding from XML.
/// </summary>
public abstract class XmlDecoder
{
    protected readonly TypedEncoder TypedEncoder = TypedEncoder.Default;

    protected abstract void ThrowElementNotEmpty();
    protected abstract void ThrowElementNotSimple();

    protected async ValueTask EmptyElement(XmlReader reader)
    {
        if(reader.IsEmptyElement)
        {
            return;
        }

        await reader.ReadAsync();
        if(reader.NodeType != XmlNodeType.EndElement)
        {
            ThrowElementNotEmpty();
        }
    }

    protected async ValueTask<bool> OpenElement(XmlReader reader)
    {
        if(reader.IsEmptyElement)
        {
            // Known to be empty
            return false;
        }

        await reader.ReadAsync();
        switch(reader.NodeType)
        {
            case XmlNodeType.EndElement:
                return false;
            case XmlNodeType.Element:
                ThrowElementNotSimple();
                return false;
        }

        return true;
    }

    protected T CloseElement<T>(XmlReader reader, T result)
    {
        try
        {
            if(reader.NodeType != XmlNodeType.EndElement)
            {
                ThrowElementNotSimple();
            }
            return result;
        }
        catch when(Dispose())
        {
            throw;
        }

        bool Dispose()
        {
            (result as IDisposable)?.Dispose();
            return false;
        }
    }
}
