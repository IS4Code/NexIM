using System.Collections.Generic;
using System.Threading.Tasks;
using Unicord.Server.Accounts;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Formats;

internal static class RosterFormatter
{
    public static async ValueTask WriteTo(this ICollection<Contact> contacts, IInfoQueryHandler handler, string version)
    {
        await using var roster = await handler.RosterQuery(version: version);

        foreach(var contact in contacts)
        {
            if(!contact.SubscriptionState.ApprovedTo)
            {
                // Invisible
                continue;
            }

            await WriteTo(contact, roster, false);
        }
    }

    public static async ValueTask WriteTo(this Contact contact, IRosterQueryHandler handler, bool removed)
    {
        await using var item = await handler.Item(contact.Account.ToResource(null), contact.Name, (removed ? RosterSubscriptionDirection.Remove : contact.SubscriptionState.Direction switch {
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
