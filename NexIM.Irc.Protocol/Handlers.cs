using System.Threading.Tasks;

namespace NexIM.Irc.Protocol;

public interface IStreamHandler
{
    ValueTask Command(in CommandMessage message);
    ValueTask Reply(in ReplyMessage message);
}
