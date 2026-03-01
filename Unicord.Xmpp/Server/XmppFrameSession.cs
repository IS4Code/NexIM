using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Xmpp.Grammar;

namespace Unicord.Xmpp.Server;

public abstract class XmppFrameSession : XmppXmlSession
{
    protected Stream Stream { get; private set; }

    XmlWriter writer;
    public sealed override XmlWriter Writer => writer;

    XmlReader reader;
    public XmlReader Reader => reader;

    public XmppFrameSession(Stream stream)
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

    public override ValueTask Flush()
    {
        return new(writer.FlushAsync());
    }

    protected async override ValueTask Close()
    {
        try
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
                if(writer.WriteState is not (WriteState.Start or WriteState.Closed))
                {
                    if(writer.WriteState != WriteState.Prolog)
                    {
                        await writer.WriteEndDocumentAsync();
                        await writer.FlushAsync();
                    }
                    await writer.WriteStartElementAsync(null, XmppVocabulary.Standard.Close.Value, XmppVocabulary.Standard.FramingNs.Value);
                    await writer.WriteEndElementAsync();
                    await writer.FlushAsync();
                }
                await writer.DisposeAsync();
            }
        }
        catch(Exception e) when(IsAllowedClosingException(e))
        {
            // Accessing closed stream
        }
    }

    public sealed override ValueTask DisposeAsync()
    {
        return Close();
    }
}
