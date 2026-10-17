using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NexIM.Irc.Protocol;
using NexIM.Irc.Server.Commands;
using NexIM.Irc.Tools;

namespace NexIM.Irc.Server.Communication;

/// <summary>
/// Provides an implementation of <see cref="IIrcSession"/> 
/// that sends IRC messages from input to
/// an <see cref="IIrcReceivingHandler"/> instance.
/// </summary>
public abstract class IrcHandlerSession : IrcTextSession, ICommandContext
{
    public abstract IrcServerReceiver ServerReceiver { get; }
    IIrcSession ICommandContext.Session => this;

    public override Encoding Encoding => mainHandler?.Encoding ?? Encoding.UTF8;

    IIrcReceivingHandler? mainHandler;

    protected override ValueTask OnCommand(in CommandMessage message)
    {
        if(mainHandler is not { } handler)
        {
            return default;
        }
        var task = handler.Command(message);
        if(task.IsCompletedSuccessfully)
        {
            return task;
        }
        return HandleExceptions(task);
    }

    protected override ValueTask OnReply(in ReplyMessage message)
    {
        if(mainHandler is not { } handler)
        {
            return default;
        }
        var task = handler.Reply(message);
        if(task.IsCompletedSuccessfully)
        {
            return task;
        }
        return HandleExceptions(task);
    }

    private async ValueTask HandleExceptions(ValueTask task)
    {
        try
        {
            await task;
        }
        catch(Exception e) when(GetIrcException(e, out var ircException))
        {
            var param = parameterPool.Rent(1);
            try
            {
                param[0] = ByteStringCache.Global.Get(Encoding, ircException.Message ?? "");
                await SendReply(new(ircException.Reply, Parameters: new(param, 1)));
            }
            finally
            {
                parameterPool.Return(param);
            }
        }
    }

    public async Task Run(IIrcReceivingHandler mainHandler, CancellationToken cancellationToken)
    {
        this.mainHandler = mainHandler;
        try
        {
            await mainHandler.StreamStarted();
            await ReadMessages(cancellationToken);
        }
        finally
        {
            await mainHandler.StreamStopped();
        }
    }

    bool GetIrcException(Exception e, [MaybeNullWhen(false)] out IrcException ircException)
    {
        switch(e)
        {
            case IrcException ie:
                ircException = ie;
                return true;
            case { InnerException: { } inner } when GetIrcException(inner, out ircException):
                return true;
            default:
                ircException = null;
                return false;
        }
    }
}
