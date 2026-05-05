using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Server.Accounts.VCards;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Handlers;

namespace NexIM.Xmpp.Server.Formats;

internal static class VCardFormatter
{
    public static async ValueTask WriteTo(this VCard vcard, IVCardHandler handler)
    {
        await handler.Version(vcard.Version);
        await handler.FormattedName(vcard.FormattedName);
        await using(var n = await handler.Name())
        {
            await n.FamilyNameNotNull(vcard.FamilyName);
            await n.GivenNameNotNull(vcard.GivenName);
            await n.MiddleNameNotNull(vcard.MiddleName);
            await n.PrefixNotNull(vcard.Prefix);
            await n.SuffixNotNull(vcard.Suffix);
        }
        await handler.NicknameRange(vcard.Nicknames);
        if(vcard.Photos is { } photos)
        {
            foreach(var photo in photos)
            {
                await using var media = await handler.Photo();
                await WriteTo(photo, media);
            }
        }
        await handler.BirthdayNotNull(vcard.Birthday);
        if(vcard.DeliveryAddresses is { } addresses)
        {
            foreach(var address in addresses)
            {
                await using var adr = await handler.DeliveryAddress();
                await WriteTo(address, adr);
                await adr.PostOfficeBoxNotNull(address.PostOfficeBox);
                await adr.ExtendedAddressNotNull(address.ExtendedAddress);
                await adr.StreetAddressNotNull(address.StreetAddress);
                await adr.LocalityNotNull(address.Locality);
                await adr.RegionNotNull(address.Region);
                await adr.PostalCodeNotNull(address.PostalCode);
                await adr.CountryNotNull(address.Country);
            }
        }
        if(vcard.AddressLabels is { } labels)
        {
            foreach(var address in labels)
            {
                await using var adr = await handler.AddressLabel();
                await WriteTo(address, adr);
                await adr.LineRange(address.Lines);
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
        if(vcard.XmppAddresses is { } xmppAddresses)
        {
            foreach(var xmpp in xmppAddresses)
            {
                await handler.XmppAddress(XmppAddress.Parse(xmpp));
            }
        }
        await handler.MailUserAgentRange(vcard.MailUserAgents);
        await handler.TimeZoneRange(vcard.TimeZones);
        if(vcard.GeographicalPositions is { } positions)
        {
            foreach(var position in positions)
            {
                await using var geo = await handler.GeographicalPosition();
                await geo.Latitude(position.Latitude);
                await geo.Longitude(position.Longitude);
            }
        }
        await handler.TitleRange(vcard.Titles);
        await handler.RoleRange(vcard.Roles);
        if(vcard.Logos is { } logos)
        {
            foreach(var logo in logos)
            {
                await using var media = await handler.Logo();
                await WriteTo(logo, media);
            }
        }
        if(vcard.AdministrativeAgents is { } agents)
        {
            foreach(var agent in agents)
            {
                await using var agentHandler = await handler.AdministrativeAgent();
                if(agent.VCard is { } agentVCard)
                {
                    await using(var nestedHandler = await agentHandler.VCard())
                    {
                        await WriteTo(agentVCard, nestedHandler);
                    }
                }
                await agentHandler.ExternalValueNotNull(agent.ExternalValue);
            }
        }
        if(vcard.Organizations is { } organizations)
        {
            foreach(var organization in organizations)
            {
                await using var org = await handler.Organization();
                await org.Name(organization.Name);
                await org.UnitRange(organization.Units);
            }
        }
        if(vcard.CategoriesKeywords is { } categoriesKeywords)
        {
            foreach(var keywords in categoriesKeywords)
            {
                await using var categories = await handler.Categories();
                await categories.KeywordRange(keywords);
            }
        }
        await handler.NoteRange(vcard.Notes);
        await handler.VCardProductNotNull(vcard.VCardProduct);
        await handler.RevisedNotNull(vcard.Revised);
        await handler.SortStringRange(vcard.SortStrings);
        if(vcard.Pronunciations is { } pronunciations)
        {
            foreach(var pronunciation in pronunciations)
            {
                await using var soundHandler = await handler.Pronunciation();
                await soundHandler.PhoneticTranscriptionNotNull(pronunciation.PhoneticTranscription);
                if(pronunciation.BinaryValue is { } binary)
                {
                    await soundHandler.BinaryValueNotNull((await binary.Get(static x => x))?.ToBase64());
                }
                await soundHandler.ExternalValueNotNull(pronunciation.ExternalValue);
            }
        }
        await handler.UniqueIdentifierNotNull(vcard.UniqueIdentifier);
        await handler.AssociatedUrlRange(vcard.AssociatedUrls);
        if(vcard.PrivacyClassification is { } privacy)
        {
            await using var classification = await handler.PrivacyClassification();
            switch(privacy)
            {
                case VCardPrivacyClassification.Public:
                    await classification.IsPublic();
                    break;
                case VCardPrivacyClassification.Private:
                    await classification.IsPrivate();
                    break;
                case VCardPrivacyClassification.Confidential:
                    await classification.IsConfidential();
                    break;
            }
        }
        if(vcard.Credentials is { } credentials)
        {
            foreach(var credential in credentials)
            {
                await using var key = await handler.Credential();
                await key.TypeNotNull(credential.Type);
                await key.Value(credential.Value);
            }
        }
        await handler.DescriptionRange(vcard.Descriptions);
    }

    static async ValueTask WriteTo(VCardMedia media, IVCardMediaHandler handler)
    {
        await handler.FormatType(media.FormatType);
        if(media.BinaryValue is { } binary)
        {
            await handler.BinaryValueNotNull((await binary.Get(static x => x))?.ToBase64());
        }
        await handler.ExternalValueNotNull(media.ExternalValue);
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
