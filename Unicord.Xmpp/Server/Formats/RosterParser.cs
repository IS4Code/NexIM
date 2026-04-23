using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Primitives.Xml.Handlers;
using Unicord.Server.Accounts;
using Unicord.Server.Tools;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Server.Handlers;

namespace Unicord.Xmpp.Server.Formats;

internal sealed class RosterParser<TContext> : BaseRosterQueryHandler<TContext> where TContext : IPayloadHandlerContext
{
    NonEmptySet<Contact>.Builder addedContacts = NonEmptySet<Contact>.Builder.Empty;
    NonEmptySet<Contact>.Builder removedContacts = NonEmptySet<Contact>.Builder.Empty;

    public NonEmptySet<Contact>? AddedContacts => addedContacts.TryToSet();
    public NonEmptySet<Contact>? RemovedContacts => removedContacts.TryToSet();
    public CapturingHandler<IRosterQueryHandler>? ExtensionsHandler { get; private set; }

    protected async override ValueTask<IRosterItemHandler> OnItem(XmppResource? identifier, string? name, Token<RosterSubscriptionDirection>? subscription, Token<RosterPendingAction>? pending, bool? subscriptionApproved)
    {
        if(identifier is not { } id)
        {
            throw XmppStanzaException.BadRequest("JID is missing.");
        }

        return new ItemParser(this, id, name, subscription?.ToEnum(), pending?.ToEnum(), subscriptionApproved) { Context = Context };
    }

    protected override ValueTask OnOther(XmlReader payloadReader)
    {
        IPayloadHandler handler = ExtensionsHandler ??= new();
        return handler.Other(payloadReader);
    }

    protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
    public override ValueTask DisposeAsync() => default;

    class ItemParser(RosterParser<TContext> parent, XmppResource identifier, string? name, RosterSubscriptionDirection? subscription, RosterPendingAction? pending, bool? subscriptionApproved) : BaseRosterItemHandler<TContext>
    {
        string? group;

        protected async override ValueTask OnGroup(string? name)
        {
            this.SetOnce(ref group, name);
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);

        public override ValueTask DisposeAsync()
        {
            ref var contacts = ref (subscription == RosterSubscriptionDirection.Remove ? ref parent.removedContacts : ref parent.addedContacts);

            contacts.Add(new Contact {
                Account = identifier.ToAccountName(out _),
                Nickname = name,
                Group = group,
                SubscriptionState = new() {
                    Direction = subscription switch {
                        RosterSubscriptionDirection.From => SubscriptionDirection.From,
                        RosterSubscriptionDirection.To => SubscriptionDirection.To,
                        RosterSubscriptionDirection.Both => SubscriptionDirection.Both,
                        _ => SubscriptionDirection.None
                    },
                    PendingTo = pending == RosterPendingAction.Subscription,
                    ApprovedTo = true,
                    ApprovedFrom = subscriptionApproved ?? false
                }
            });

            return default;
        }
    }
}
