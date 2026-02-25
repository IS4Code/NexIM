using System.Collections.Generic;
using System.Threading.Tasks;
using Unicord.Server.Model;

namespace Unicord.Server;

public interface IClientSession
{
    string Identifier { get; }
    sbyte Priority { get; }

    SenderPresentation Presentation { get; }
    Status Status { get; }

    ValueTask Conversation(Sender sender, ConversationType? type, Message? message, ChatState? chatState);

    ValueTask StatusUpdate(Sender sender, Status status);
    ValueTask SubscribeRequest(Sender sender);
    ValueTask SubscribeResponse(Sender sender);
    ValueTask UnsubscribeRequest(Sender sender);
    ValueTask UnsubscribeResponse(Sender sender);

    ValueTask ContactUpdated(Contact contact, ICollection<Contact> current);
    ValueTask ContactRemoved(Contact contact, ICollection<Contact> current);
}
