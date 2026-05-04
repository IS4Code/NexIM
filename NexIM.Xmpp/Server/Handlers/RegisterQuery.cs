using System;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Server.Accounts.VCards;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;

namespace NexIM.Xmpp.Server.Handlers;

internal sealed class GetRegisterQuery : RegisterQueryHandler<ICommandContext>
{
    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        await this.Unexpected(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        await using var iq = await this.CreateResponse();

        await using var query = await iq.RegisterQuery();

        if(this.TryGetClientSession() is { } session)
        {
            // Already registered
            var account = session.Account;
            await query.Registered();
            await query.Username(account.Name.ToAddress().User);
            await query.Password(null);
            await query.EmailAddress(account.Email);
            return;
        }

        // Never request plaintext password over insecure connection
        if(!this.IsSecureSession())
        {
            await query.Instructions(new("The connection must be secure to proceed with registration."));
            return;
        }

        await query.Username(null);
        await query.Password(null);
        await query.EmailAddress(null);
    }
}

internal class SetRegisterQuery : RegisterQueryHandler<ICommandContext>, IDisposable
{
    string? username;
    MailAddress? email;
    TemporaryString? password;
    VCardDeliveryAddress? _address;
    VCardDeliveryAddress address => _address ??= this.AddList(ref vcard.DeliveryAddresses, new());

    VCard vcard = new() {
        // Populated initially
        PrivacyClassification = VCardPrivacyClassification.Private
    };

    protected async override ValueTask OnUsername(string? value)
    {
        this.SetOnce(ref username, value);
    }

    protected async override ValueTask OnEmailAddress(MailAddress? value)
    {
        if(value == null)
        {
            return;
        }
        this.SetOnce(ref email, value);
        this.AddList(ref vcard.Emails, new VCardEmail {
            EmailFlags = VCardEmailFlags.IsInternet,
            Identifier = value.Address
        });
    }

    protected async override ValueTask OnPassword(TemporaryString? value)
    {
        if(!this.IsSecureSession())
        {
            // Password was not requested
            throw XmppStanzaException.BadRequest();
        }

        if(value == null)
        {
            return;
        }

        var copy = TemporaryString.MoveFrom(value);

        try
        {
            this.SetOnce(ref password, copy);
        }
        catch when(Dispose())
        {
            // Dispose the string when not set
            throw;
        }

        bool Dispose()
        {
            copy.Dispose();
            return false;
        }
    }

    protected async override ValueTask OnName(string? value)
    {
        this.SetOnce(ref vcard.FormattedName, value);
    }

    protected async override ValueTask OnFirstName(string? value)
    {
        this.SetOnce(ref vcard.GivenName, value);
    }

    protected async override ValueTask OnLastName(string? value)
    {
        this.SetOnce(ref vcard.FamilyName, value);
    }

    protected async override ValueTask OnNickname(string? value)
    {
        this.AddList(ref vcard.Nicknames, value);
    }

    protected async override ValueTask OnDate(DateComponents? value)
    {
        this.SetOnce(ref vcard.Birthday, value);
    }

    protected async override ValueTask OnUrl(ValueUri? value)
    {
        this.AddList(ref vcard.AssociatedUrls, value);
    }

    protected async override ValueTask OnAddress(string? value)
    {
        this.SetOnce(ref address.StreetAddress, value);
    }

    protected async override ValueTask OnState(string? value)
    {
        this.SetOnce(ref address.Country, value);
    }

    protected async override ValueTask OnCity(string? value)
    {
        this.SetOnce(ref address.Locality, value);
    }

    protected async override ValueTask OnZipCode(string? value)
    {
        this.SetOnce(ref address.PostalCode, value);
    }

    protected async override ValueTask OnPhoneNumber(string? value)
    {
        this.AddList(ref vcard.Telephones, new() {
            Number = value
        });
    }

    protected async override ValueTask OnText(string? value)
    {
        this.AddList(ref vcard.Notes, value);
    }

    protected async override ValueTask OnMiscellaneous(string? value)
    {
        this.AddList(ref vcard.Descriptions, value);
    }

    protected async override ValueTask OnUnrecognized(XmlReader payloadReader)
    {
        // All elements must be recognized
        await this.Unexpected(payloadReader);
    }

    public async override ValueTask DisposeAsync()
    {
        try
        {
            if(username == null || email == null || password == null)
            {
                throw XmppStanzaException.NotAcceptable();
            }

            var identifier = new XmppAddress(username, this.GetLocalResource().Address.Host);

            var accountName = identifier.ToAccountName();

            if(await this.GetServer().Register(accountName, password, email, vcard) is not { } account)
            {
                throw XmppStanzaException.Conflict();
            }
        }
        finally
        {
            Dispose();
        }

        // No errors - successful
        await this.SendResponse();
    }

    public void Dispose()
    {
        password?.Dispose();
    }
}
