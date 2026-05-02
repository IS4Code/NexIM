using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Server.Accounts;
using NexIM.Server.Events;
using NexIM.Tools;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;
using NexIM.Xmpp.Server.Handlers;

namespace NexIM.Xmpp.Server.Formats;

internal sealed class AddressesParser<TContext> : BaseAddressesHandler<TContext> where TContext : IPayloadHandlerContext
{
    NonEmptySet<Identifier>.Builder recipientBuilder;
    NonEmptyDictionary<AddressRelation, LocalizedString?>.Builder addressBuilder;

    public NonEmptySet<Identifier>? Recipients => recipientBuilder.TryToSet();
    public NonEmptyDictionary<AddressRelation, LocalizedString?>? Addresses => addressBuilder.TryToDictionary();

    protected async override ValueTask OnAddress(Token<AddressType>? type, XmppResource? address, Token<DiscoNode>? node, ValueUri? uri, LanguageTaggedString? description, True? delivered)
    {
        if(uri is not null || node is not null)
        {
            throw XmppStanzaException.FeatureNotImplemented(ErrorType.Modify, "Only simple XMPP addresses are supported.");
        }

        // No need to pass the XMPP session because the server does not receive messages or presence
        var recipient = address?.ToIdentifier();

        var delivery = new AddressRelation(
            type?.ToEnum()?.ToDeliveryAddressType() ?? throw XmppStanzaException.BadRequest(),
            recipient
        );

        addressBuilder.Add(delivery, description);
        if(delivered is null && recipient is { } identifier && delivery.Type is DeliveryRelationType.Primary or DeliveryRelationType.Secondary or DeliveryRelationType.Hidden)
        {
            // Should also be delivered to this address
            recipientBuilder.Add(identifier);
        }
    }

    public void Add(AddressRelation address, LanguageTaggedString? description = null)
    {
        addressBuilder.Add(address, description);
    }

    protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
    public override ValueTask DisposeAsync() => default;
}
