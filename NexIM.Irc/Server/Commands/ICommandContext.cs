using NexIM.Irc.Server.Communication;

namespace NexIM.Irc.Server.Commands;

internal interface ICommandContext
{
    IrcServerReceiver ServerReceiver { get; }
    IIrcSession Session { get; }
}

internal interface ICommandHandler
{
    ICommandContext Context { get; init; }
}
