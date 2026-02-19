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

    static readonly byte[] buffer = new byte[1];

    public async ValueTask<bool> CheckMoreData()
    {
        return await Stream.ReadAsync(buffer, 0, 1, CancellationToken) > 0;
    }

    public ValueTask Flush()
    {
        return new(writer.FlushAsync());
    }

    protected async override ValueTask Close()
    {
        // Stream will be disposed
        await using var stream = Stream;

        // Close for reading and writing
        var task = CloseStream();
        try
        {
            Reader.Dispose();
        }
        finally
        {
            await task;
        }

        async Task CloseStream()
        {
            await writer.WriteEndDocumentAsync();
            await writer.FlushAsync();
            await writer.DisposeAsync();
        }
    }

    public sealed override ValueTask DisposeAsync()
    {
        return Close();
    }
}
