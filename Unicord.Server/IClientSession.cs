using System.Collections.Generic;
using System.Threading.Tasks;
using Unicord.Server.Model;

namespace Unicord.Server;

public interface IClientSession
{
    string Identifier { get; }
    sbyte Priority { get; }

    ValueTask Conversation(Sender sender, ConversationType? type, Message? message, ChatState? chatState);
    ValueTask ContactAdded(Contact contact, ICollection<Contact> current);
    ValueTask ContactRemoved(Contact contact, ICollection<Contact> current);
}
