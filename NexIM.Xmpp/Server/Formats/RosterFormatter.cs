using System.Threading.Tasks;
using NexIM.Server.Accounts;
using NexIM.Xmpp.Protocol;

namespace NexIM.Xmpp.Server.Formats;

internal static class RosterFormatter
{
    public static async ValueTask WriteTo(this Contact contact, IRosterQueryHandler handler, bool removed)
    {
        if(!contact.SubscriptionState.ApprovedTo)
        {
            // Invisible
            return;
        }

        await using var item = await handler.Item(contact.Account.ToResource(null), contact.Nickname, (removed ? RosterSubscriptionDirection.Remove : contact.SubscriptionState.Direction switch {
            SubscriptionDirection.To => RosterSubscriptionDirection.To,
            SubscriptionDirection.From => RosterSubscriptionDirection.From,
            SubscriptionDirection.Both => RosterSubscriptionDirection.Both,
            _ => RosterSubscriptionDirection.None
        }).ToToken(), contact.SubscriptionState.PendingTo ? RosterPendingAction.Subscription.ToToken() : null, contact.SubscriptionState.ApprovedFrom);

        if(contact.Group is { } group)
        {
            await item.Group(group);
        }
    }
}
