using System.Collections.Generic;
using System.Threading.Tasks;
using Unicord.Server.Events;
using Unicord.Server.Accounts;

namespace Unicord.Server;

public interface IClientSession
{
    string Identifier { get; }
    sbyte Priority { get; }

    SenderPresentation Presentation { get; }
    Status Status { get; }

    ValueTask<ErrorCode> Receive(Event evnt);

    ValueTask StatusUpdate(Sender sender, Status status);
    ValueTask SubscribeRequest(Sender sender);
    ValueTask SubscribeResponse(Sender sender);
    ValueTask UnsubscribeRequest(Sender sender);
    ValueTask UnsubscribeResponse(Sender sender);

    ValueTask ContactUpdated(Contact contact, ICollection<Contact> current);
    ValueTask ContactRemoved(Contact contact, ICollection<Contact> current);
}
