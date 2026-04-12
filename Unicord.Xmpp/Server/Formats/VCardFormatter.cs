using System.Threading.Tasks;
using Unicord.Server.Accounts.VCards;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server.Formats;

internal static class VCardFormatter
{
    public static async ValueTask WriteTo(this VCard vcard, IVCardHandler handler)
    {
        await handler.Version(vcard.Version);
        await handler.FormattedName(vcard.FormattedName);
        await using(var n = await handler.Name())
        {
            await n.FamilyName(vcard.FamilyName);
            await n.GivenName(vcard.GivenName);
            await n.MiddleName(vcard.MiddleName);
            await n.Prefix(vcard.Prefix);
            await n.Suffix(vcard.Suffix);
        }
        await handler.Nicknames(vcard.Nicknames);
        if(vcard.Photo is { } photo)
        {
            await using var media = await handler.Photo();
            await WriteTo(photo, media);
        }
        await handler.Birthday(vcard.Birthday);
        if(vcard.DeliveryAddresses is { } addresses)
        {
            foreach(var address in addresses)
            {
                await using var adr = await handler.DeliveryAddress();
                await WriteTo(address, adr);
                await adr.PostOfficeBox(address.PostOfficeBox);
                await adr.ExtendedAddress(address.ExtendedAddress);
                await adr.StreetAddress(address.StreetAddress);
                await adr.Locality(address.Locality);
                await adr.Region(address.Region);
                await adr.PostalCode(address.PostalCode);
                await adr.Country(address.Country);
            }
        }
        if(vcard.AddressLabels is { } labels)
        {
            foreach(var address in labels)
            {
                await using var adr = await handler.AddressLabel();
                await WriteTo(address, adr);
                if(address.Lines is { } lines)
                {
                    foreach(var line in lines)
                    {
                        await adr.Line(line);
                    }
                }
            }
        }
        if(vcard.Telephones is { } telephones)
        {
            foreach(var telephone in telephones)
            {
                await using var tel = await handler.Telephone();
                await WriteTo(telephone, tel);
            }
        }
        if(vcard.Emails is { } emails)
        {
            foreach(var email in emails)
            {
                await using var mail = await handler.Email();
                await WriteTo(email, mail);
            }
        }
        if(vcard.XmppAddress is { } xmpp)
        {
            await handler.XmppAddress(XmppAddress.Parse(xmpp));
        }
        await handler.MailUserAgent(vcard.MailUserAgent);
        await handler.TimeZone(vcard.TimeZone);
        if(vcard.Latitude != null || vcard.Longitude != null)
        {
            await using var geo = await handler.GeographicalPosition();
            await geo.Latitude(vcard.Latitude);
            await geo.Longitude(vcard.Longitude);
        }
        await handler.Title(vcard.Title);
        await handler.Role(vcard.Role);
        if(vcard.Logo is { } logo)
        {
            await using var media = await handler.Logo();
            await WriteTo(logo, media);
        }
        if(vcard.AdministrativeAgent is { } agent)
        {
            await using var agentHandler = await handler.AdministrativeAgent();
            if(agent.VCard is { } agentVCard)
            {
                await using var nestedHandler = await agentHandler.VCard();
                await WriteTo(agentVCard, nestedHandler);
            }
            await agentHandler.ExternalValue(agent.ExternalValue);
        }
        if(vcard.OrganizationName is { } orgName)
        {
            await using var org = await handler.Organization();
            await org.Name(orgName);
            if(vcard.OrganizationUnits is { } units)
            {
                foreach(var unit in units)
                {
                    await org.Unit(unit);
                }
            }
        }
        if(vcard.CategoriesKeywords is { } keywords)
        {
            await using var categories = await handler.Categories();
            foreach(var keyword in keywords)
            {
                await categories.Keyword(keyword);
            }
        }
        await handler.Note(vcard.Note);
        await handler.VCardProduct(vcard.VCardProduct);
        await handler.Revised(vcard.Revised);
        await handler.SortString(vcard.SortString);
        if(vcard.Pronunciation is { } pronunciation)
        {
            await using var soundHandler = await handler.Pronunciation();
            await soundHandler.PhoneticTranscription(pronunciation.PhoneticTranscription);
            await soundHandler.BinaryValue(pronunciation.BinaryValue);
            await soundHandler.ExternalValue(pronunciation.ExternalValue);
        }
        await handler.UniqueIdentifier(vcard.UniqueIdentifier);
        await handler.AssociatedUrl(vcard.AssociatedUrl);
        if(vcard.PrivacyClassification is { } privacy)
        {
            await using var classification = await handler.PrivacyClassification();
            switch(privacy)
            {
                case VCardPrivacyClassification.Public:
                    await classification.IsPublic();
                    break;
                case VCardPrivacyClassification.Private:
                    await classification.IsPublic();
                    break;
                case VCardPrivacyClassification.Confidential:
                    await classification.IsConfidential();
                    break;
            }
        }
        if(vcard.CredentialValue is { } keyValue)
        {
            await using var key = await handler.Credential();
            await key.Type(vcard.CredentialType);
            await key.Value(keyValue);
        }
        await handler.Description(vcard.Description);
    }

    static async ValueTask WriteTo(VCardMedia media, IVCardMediaHandler handler)
    {
        await handler.FormatType(media.FormatType);
        await handler.BinaryValue(media.BinaryValue);
        await handler.ExternalValue(media.ExternalValue);
    }

    static async ValueTask WriteTo(VCardAddress address, IVCardAddressHandler handler)
    {
        if((address.IdentifierFlags & VCardIdentifierFlags.IsHome) != 0)
        {
            await handler.IsHome();
        }
        if((address.IdentifierFlags & VCardIdentifierFlags.IsWork) != 0)
        {
            await handler.IsWork();
        }
        if((address.AddressFlags & VCardAddressFlags.IsPostal) != 0)
        {
            await handler.IsPostal();
        }
        if((address.AddressFlags & VCardAddressFlags.IsParcel) != 0)
        {
            await handler.IsParcel();
        }
        switch(address.AddressType)
        {
            case VCardAddressType.Domestic:
                await handler.IsDomestic();
                break;
            case VCardAddressType.International:
                await handler.IsInternational();
                break;
        }
        if((address.IdentifierFlags & VCardIdentifierFlags.IsPreferred) != 0)
        {
            await handler.IsPreferred();
        }
    }

    static async ValueTask WriteTo(VCardTelephone telephone, IVCardTelephoneHandler handler)
    {
        if((telephone.IdentifierFlags & VCardIdentifierFlags.IsHome) != 0)
        {
            await handler.IsHome();
        }
        if((telephone.IdentifierFlags & VCardIdentifierFlags.IsWork) != 0)
        {
            await handler.IsWork();
        }
        if((telephone.TelephoneFlags & VCardTelephoneFlags.IsVoice) != 0)
        {
            await handler.IsVoice();
        }
        if((telephone.TelephoneFlags & VCardTelephoneFlags.IsFax) != 0)
        {
            await handler.IsFax();
        }
        if((telephone.TelephoneFlags & VCardTelephoneFlags.IsPager) != 0)
        {
            await handler.IsPager();
        }
        if((telephone.TelephoneFlags & VCardTelephoneFlags.IsText) != 0)
        {
            await handler.IsText();
        }
        if((telephone.TelephoneFlags & VCardTelephoneFlags.IsCell) != 0)
        {
            await handler.IsCell();
        }
        if((telephone.TelephoneFlags & VCardTelephoneFlags.IsVideo) != 0)
        {
            await handler.IsVideo();
        }
        if((telephone.TelephoneFlags & VCardTelephoneFlags.IsBbs) != 0)
        {
            await handler.IsBbs();
        }
        if((telephone.TelephoneFlags & VCardTelephoneFlags.IsModem) != 0)
        {
            await handler.IsModem();
        }
        if((telephone.TelephoneFlags & VCardTelephoneFlags.IsIsdn) != 0)
        {
            await handler.IsIsdn();
        }
        if((telephone.TelephoneFlags & VCardTelephoneFlags.IsPcs) != 0)
        {
            await handler.IsPcs();
        }
        if((telephone.IdentifierFlags & VCardIdentifierFlags.IsPreferred) != 0)
        {
            await handler.IsPreferred();
        }
        await handler.Number(telephone.Number);
    }

    static async ValueTask WriteTo(VCardEmail email, IVCardEmailHandler handler)
    {
        if((email.IdentifierFlags & VCardIdentifierFlags.IsHome) != 0)
        {
            await handler.IsHome();
        }
        if((email.IdentifierFlags & VCardIdentifierFlags.IsWork) != 0)
        {
            await handler.IsWork();
        }
        if((email.EmailFlags & VCardEmailFlags.IsInternet) != 0)
        {
            await handler.IsInternet();
        }
        if((email.IdentifierFlags & VCardIdentifierFlags.IsPreferred) != 0)
        {
            await handler.IsPreferred();
        }
        if((email.EmailFlags & VCardEmailFlags.IsX400) != 0)
        {
            await handler.IsX400();
        }
        await handler.Identifier(email.Identifier);
    }
}
