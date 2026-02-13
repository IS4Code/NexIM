using System.Threading.Tasks;
using Unicord.Server.Model;

namespace Unicord.Server;

public interface IClientSession
{
    string Identifier { get; }
    sbyte Priority { get; }

    ValueTask Send(Message message);
}
