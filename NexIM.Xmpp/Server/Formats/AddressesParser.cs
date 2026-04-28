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
    NonEmptySet<DeliveryAddress>.Builder addressBuilder;

    public NonEmptySet<Identifier>? Recipients => recipientBuilder.TryToSet();
    public NonEmptySet<DeliveryAddress>? Addresses => addressBuilder.TryToSet();

    protected async override ValueTask OnAddress(Token<AddressType>? type, XmppResource? address, Token<DiscoNode>? node, ValueUri? uri, LanguageTaggedString? description, True? delivered)
    {
        if(uri is not null || node is not null)
        {
            throw XmppStanzaException.FeatureNotImplemented(ErrorType.Modify, "Only simple XMPP addresses are supported.");
        }

        // No need to pass the XMPP session because the server does not receive messages or presence
        var recipient = address?.ToIdentifier();

        var delivery = new DeliveryAddress(
            type?.ToEnum()?.ToDeliveryAddressType() ?? throw XmppStanzaException.BadRequest(),
            recipient,
            description
        );

        addressBuilder.Add(delivery);
        if(delivered is null && recipient is { } identifier && delivery.Type is DeliveryAddressType.Primary or DeliveryAddressType.Secondary or DeliveryAddressType.Hidden)
        {
            // Should also be delivered to this address
            recipientBuilder.Add(identifier);
        }
    }

    public void Add(DeliveryAddress address)
    {
        addressBuilder.Add(address);
    }

    protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
    public override ValueTask DisposeAsync() => default;
}
