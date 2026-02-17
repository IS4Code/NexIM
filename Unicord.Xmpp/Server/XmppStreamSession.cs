using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace Unicord.Xmpp.Server;

/// <summary>
/// Provides an implementation of <see cref="IXmppSession"/> sending
/// XMPP commands to a <see cref="Stream"/> instance.
/// </summary>
public abstract class XmppStreamSession : XmppXmlSession
{
    protected Stream Stream { get; private set; }

    XmlWriter writer;
    public sealed override XmlWriter Writer => writer;

    XmlReader reader;
    public XmlReader Reader => reader;

    public XmppStreamSession(Stream stream)
    {
        Initialize(stream);
    }

    protected abstract void OpenXmlStream(Stream stream, out XmlReader reader, out XmlWriter writer);

    [MemberNotNull(nameof(Stream), nameof(writer), nameof(reader))]
    protected void Initialize(Stream stream)
    {
        writer?.Dispose();
        Reader?.Dispose();

        Stream = stream;

        OpenXmlStream(stream, out reader, out writer);
    }

    public ValueTask Flush()
    {
        return new(writer.FlushAsync());
    }

    protected async override ValueTask Close()
    {
        writer.Close();
        Reader.Close();
        await Stream.DisposeAsync();
    }

    public sealed override ValueTask DisposeAsync()
    {
        return Close();
    }
}
