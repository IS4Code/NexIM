using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace NexIM.Irc.Server.Communication;

/// <summary>
/// Provides an implementation of <see cref="IIrcSession"/> sending
/// IRC commands to a <see cref="Stream"/> instance.
/// </summary>
public abstract class IrcStreamSession : IrcHandlerSession
{
    protected Stream Stream { get; }

    readonly PipeWriter writer;
    protected sealed override PipeWriter Writer => writer;

    readonly PipeReader reader;
    protected sealed override PipeReader Reader => reader;

    public IrcStreamSession(Stream stream)
    {
        Stream = stream;

        writer = PipeWriter.Create(stream);
        reader = PipeReader.Create(stream);
    }

    public async override ValueTask DisposeAsync()
    {
        await Stream.DisposeAsync();
    }
}
