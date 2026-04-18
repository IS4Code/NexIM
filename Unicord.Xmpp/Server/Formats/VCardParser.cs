using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using Unicord.Primitives;
using Unicord.Primitives.Xml.Handlers;
using Unicord.Server.Accounts.VCards;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Server.Handlers;

namespace Unicord.Xmpp.Server.Formats;

internal class VCardParser<TContext>(VCard vcard) : BaseVCardHandler<TContext> where TContext : IPayloadHandlerContext
{
    public VCard VCard => vcard;
    public CapturingHandler<IVCardHandler>? ExtensionsHandler { get; private set; }

    protected async override ValueTask OnVersion(string? version)
    {
        this.SetOnce(ref vcard.Version, version);
    }

    protected async override ValueTask OnFormattedName(string? text)
    {
        this.SetOnce(ref vcard.FormattedName, text);
    }

    protected async override ValueTask<IVCardNameHandler> OnName()
    {
        return new NameParser(vcard) { Context = Context };
    }

    protected async override ValueTask OnNickname(string? nicknames)
    {
        this.AddList(ref vcard.Nicknames, nicknames);
    }

    protected async override ValueTask<IVCardMediaHandler> OnPhoto()
    {
        return new MediaParser(this.AddList(ref vcard.Photos, new())) { Context = Context };
    }

    protected async override ValueTask OnBirthday(DateTimeOffset? dateTime)
    {
        this.SetOnce(ref vcard.Birthday, dateTime);
    }

    protected async override ValueTask<IVCardDeliveryAddressHandler> OnDeliveryAddress()
    {
        var obj = new VCardDeliveryAddress();
        (vcard.DeliveryAddresses ??= new()).Add(obj);
        return new DeliveryAddressParser(obj) { Context = Context };
    }

    protected async override ValueTask<IVCardAddressLabelHandler> OnAddressLabel()
    {
        var obj = new VCardAddressLabel();
        (vcard.AddressLabels ??= new()).Add(obj);
        return new AddressLabelParser(obj) { Context = Context };
    }

    protected async override ValueTask<IVCardTelephoneHandler> OnTelephone()
    {
        var obj = new VCardTelephone();
        (vcard.Telephones??= new()).Add(obj);
        return new TelephoneParser(obj) { Context = Context };
    }

    protected async override ValueTask<IVCardEmailHandler> OnEmail()
    {
        var obj = new VCardEmail();
        (vcard.Emails ??= new()).Add(obj);
        return new EmailParser(obj) { Context = Context };
    }

    protected async override ValueTask OnXmppAddress(XmppAddress? address)
    {
        this.AddList(ref vcard.XmppAddresses, address?.ToString());
    }

    protected async override ValueTask OnMailUserAgent(string? type)
    {
        this.AddList(ref vcard.MailUserAgents, type);
    }

    protected async override ValueTask OnTimeZone(TimeZoneOffset? offset)
    {
        this.AddList(ref vcard.TimeZones, offset);
    }

    protected async override ValueTask<IVCardGeoHandler> OnGeographicalPosition()
    {
        return new GeoParser(vcard) { Context = Context };
    }

    protected async override ValueTask OnTitle(string? text)
    {
        this.AddList(ref vcard.Titles, text);
    }

    protected async override ValueTask OnRole(string? text)
    {
        this.AddList(ref vcard.Roles, text);
    }

    protected async override ValueTask<IVCardMediaHandler> OnLogo()
    {
        return new MediaParser(this.AddList(ref vcard.Logos, new())) { Context = Context };
    }

    protected async override ValueTask<IVCardPersonHandler> OnAdministrativeAgent()
    {
        return new PersonParser(this.AddList(ref vcard.AdministrativeAgents, new())) { Context = Context };
    }

    protected async override ValueTask<IVCardOrganizationHandler> OnOrganization()
    {
        return new OrganizationParser(vcard) { Context = Context };
    }

    protected async override ValueTask<IVCardCategoriesHandler> OnCategories()
    {
        return new CategoriesParser(vcard) { Context = Context };
    }

    protected async override ValueTask OnNote(string? text)
    {
        this.AddList(ref vcard.Notes, text);
    }

    protected async override ValueTask OnVCardProduct(string? text)
    {
        this.SetOnce(ref vcard.VCardProduct, text);
    }

    protected async override ValueTask OnRevised(DateTimeOffset? dateTime)
    {
        this.SetOnce(ref vcard.Revised, dateTime);
    }

    protected async override ValueTask OnSortString(string? value)
    {
        this.AddList(ref vcard.SortStrings, value);
    }

    protected async override ValueTask<IVCardPronunciationHandler> OnPronunciation()
    {
        return new PronunciationParser(this.AddList(ref vcard.Pronunciations, new())) { Context = Context };
    }

    protected async override ValueTask OnUniqueIdentifier(string? value)
    {
        this.SetOnce(ref vcard.UniqueIdentifier, value);
    }

    protected async override ValueTask OnAssociatedUrl(Uri? value)
    {
        this.AddList(ref vcard.AssociatedUrls, value);
    }

    protected async override ValueTask<IVCardPrivacyClassificationHandler> OnPrivacyClassification()
    {
        return new PrivacyClassificationParser(vcard) { Context = Context };
    }

    protected async override ValueTask<IVCardCredentialHandler> OnCredential()
    {
        return new CredentialParser(vcard) { Context = Context };
    }

    protected async override ValueTask OnDescription(string? text)
    {
        this.AddList(ref vcard.Descriptions, text);
    }

    protected override ValueTask OnOther(XmlReader payloadReader)
    {
        IPayloadHandler handler = ExtensionsHandler ??= new();
        return handler.Other(payloadReader);
    }

    protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
    public override ValueTask DisposeAsync() => default;

    sealed class NameParser(VCard vcard) : BaseVCardNameHandler<TContext>
    {
        protected async override ValueTask OnFamilyName(string? name)
        {
            this.SetOnce(ref vcard.FamilyName, name);
        }

        protected async override ValueTask OnGivenName(string? name)
        {
            this.SetOnce(ref vcard.GivenName, name);
        }

        protected async override ValueTask OnMiddleName(string? name)
        {
            this.SetOnce(ref vcard.MiddleName, name);
        }

        protected async override ValueTask OnPrefix(string? prefix)
        {
            this.SetOnce(ref vcard.Prefix, prefix);
        }

        protected async override ValueTask OnSuffix(string? suffix)
        {
            this.SetOnce(ref vcard.Suffix, suffix);
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
        public override ValueTask DisposeAsync() => default;
    }

    sealed class MediaParser(VCardMedia media) : BaseVCardMediaHandler<TContext>
    {
        protected async override ValueTask OnFormatType(string? type)
        {
            this.SetOnce(ref media.FormatType, type);
        }

        protected async override ValueTask OnExternalValue(Uri? uri)
        {
            this.SetOnce(ref media.ExternalValue, uri);
        }

        protected async override ValueTask OnBinaryValue(TemporaryFile? data)
        {
            this.SetOnce(ref media.BinaryValue, data);
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
        public override ValueTask DisposeAsync() => default;
    }

    sealed class DeliveryAddressParser(VCardDeliveryAddress address) : BaseVCardDeliveryAddressHandler<TContext>
    {
        protected async override ValueTask OnIsHome()
        {
            this.SetOnceFlag(ref address.IdentifierFlags, VCardIdentifierFlags.IsHome);
        }

        protected async override ValueTask OnIsWork()
        {
            this.SetOnceFlag(ref address.IdentifierFlags, VCardIdentifierFlags.IsWork);
        }

        protected async override ValueTask OnIsPreferred()
        {
            this.SetOnceFlag(ref address.IdentifierFlags, VCardIdentifierFlags.IsPreferred);
        }

        protected async override ValueTask OnIsDomestic()
        {
            this.SetOnce(ref address.AddressType, VCardAddressType.Domestic);
        }

        protected async override ValueTask OnIsInternational()
        {
            this.SetOnce(ref address.AddressType, VCardAddressType.International);
        }

        protected async override ValueTask OnIsParcel()
        {
            this.SetOnceFlag(ref address.AddressFlags, VCardAddressFlags.IsParcel);
        }

        protected async override ValueTask OnIsPostal()
        {
            this.SetOnceFlag(ref address.AddressFlags, VCardAddressFlags.IsPostal);
        }

        protected async override ValueTask OnCountry(string? text)
        {
            this.SetOnce(ref address.Country, text);
        }

        protected async override ValueTask OnExtendedAddress(string? text)
        {
            this.SetOnce(ref address.ExtendedAddress, text);
        }

        protected async override ValueTask OnLocality(string? text)
        {
            this.SetOnce(ref address.Locality, text);
        }

        protected async override ValueTask OnPostalCode(string? text)
        {
            this.SetOnce(ref address.PostalCode, text);
        }

        protected async override ValueTask OnPostOfficeBox(string? text)
        {
            this.SetOnce(ref address.PostOfficeBox, text);
        }

        protected async override ValueTask OnRegion(string? text)
        {
            this.SetOnce(ref address.Region, text);
        }

        protected async override ValueTask OnStreetAddress(string? text)
        {
            this.SetOnce(ref address.StreetAddress, text);
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
        public override ValueTask DisposeAsync() => default;
    }

    sealed class AddressLabelParser(VCardAddressLabel address) : BaseVCardAddressLabelHandler<TContext>
    {
        protected async override ValueTask OnIsHome()
        {
            this.SetOnceFlag(ref address.IdentifierFlags, VCardIdentifierFlags.IsHome);
        }

        protected async override ValueTask OnIsWork()
        {
            this.SetOnceFlag(ref address.IdentifierFlags, VCardIdentifierFlags.IsWork);
        }

        protected async override ValueTask OnIsPreferred()
        {
            this.SetOnceFlag(ref address.IdentifierFlags, VCardIdentifierFlags.IsPreferred);
        }

        protected async override ValueTask OnIsDomestic()
        {
            this.SetOnce(ref address.AddressType, VCardAddressType.Domestic);
        }

        protected async override ValueTask OnIsInternational()
        {
            this.SetOnce(ref address.AddressType, VCardAddressType.International);
        }

        protected async override ValueTask OnIsParcel()
        {
            this.SetOnceFlag(ref address.AddressFlags, VCardAddressFlags.IsParcel);
        }

        protected async override ValueTask OnIsPostal()
        {
            this.SetOnceFlag(ref address.AddressFlags, VCardAddressFlags.IsPostal);
        }

        protected async override ValueTask OnLine(string? text)
        {
            if(text != null)
            {
                (address.Lines ??= new()).Add(text);
            }
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
        public override ValueTask DisposeAsync() => default;
    }

    sealed class TelephoneParser(VCardTelephone telephone) : BaseVCardTelephoneHandler<TContext>
    {
        protected async override ValueTask OnIsHome()
        {
            this.SetOnceFlag(ref telephone.IdentifierFlags, VCardIdentifierFlags.IsHome);
        }

        protected async override ValueTask OnIsWork()
        {
            this.SetOnceFlag(ref telephone.IdentifierFlags, VCardIdentifierFlags.IsWork);
        }

        protected async override ValueTask OnIsPreferred()
        {
            this.SetOnceFlag(ref telephone.IdentifierFlags, VCardIdentifierFlags.IsPreferred);
        }

        protected async override ValueTask OnIsBbs()
        {
            this.SetOnceFlag(ref telephone.TelephoneFlags, VCardTelephoneFlags.IsBbs);
        }

        protected async override ValueTask OnIsCell()
        {
            this.SetOnceFlag(ref telephone.TelephoneFlags, VCardTelephoneFlags.IsCell);
        }

        protected async override ValueTask OnIsFax()
        {
            this.SetOnceFlag(ref telephone.TelephoneFlags, VCardTelephoneFlags.IsFax);
        }

        protected async override ValueTask OnIsIsdn()
        {
            this.SetOnceFlag(ref telephone.TelephoneFlags, VCardTelephoneFlags.IsIsdn);
        }

        protected async override ValueTask OnIsModem()
        {
            this.SetOnceFlag(ref telephone.TelephoneFlags, VCardTelephoneFlags.IsModem);
        }

        protected async override ValueTask OnIsPager()
        {
            this.SetOnceFlag(ref telephone.TelephoneFlags, VCardTelephoneFlags.IsPager);
        }

        protected async override ValueTask OnIsPcs()
        {
            this.SetOnceFlag(ref telephone.TelephoneFlags, VCardTelephoneFlags.IsPcs);
        }

        protected async override ValueTask OnIsText()
        {
            this.SetOnceFlag(ref telephone.TelephoneFlags, VCardTelephoneFlags.IsText);
        }

        protected async override ValueTask OnIsVideo()
        {
            this.SetOnceFlag(ref telephone.TelephoneFlags, VCardTelephoneFlags.IsVideo);
        }

        protected async override ValueTask OnIsVoice()
        {
            this.SetOnceFlag(ref telephone.TelephoneFlags, VCardTelephoneFlags.IsVoice);
        }

        protected async override ValueTask OnNumber(string? number)
        {
            this.SetOnce(ref telephone.Number, number);
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
        public override ValueTask DisposeAsync() => default;
    }

    sealed class EmailParser(VCardEmail email) : BaseVCardEmailHandler<TContext>
    {
        protected async override ValueTask OnIsHome()
        {
            this.SetOnceFlag(ref email.IdentifierFlags, VCardIdentifierFlags.IsHome);
        }

        protected async override ValueTask OnIsWork()
        {
            this.SetOnceFlag(ref email.IdentifierFlags, VCardIdentifierFlags.IsWork);
        }

        protected async override ValueTask OnIsPreferred()
        {
            this.SetOnceFlag(ref email.IdentifierFlags, VCardIdentifierFlags.IsPreferred);
        }

        protected async override ValueTask OnIsInternet()
        {
            this.SetOnceFlag(ref email.EmailFlags, VCardEmailFlags.IsInternet);
        }

        protected async override ValueTask OnIsX400()
        {
            this.SetOnceFlag(ref email.EmailFlags, VCardEmailFlags.IsX400);
        }

        protected async override ValueTask OnIdentifier(string? value)
        {
            this.SetOnce(ref email.Identifier, value);
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
        public override ValueTask DisposeAsync() => default;
    }

    sealed class GeoParser(VCard vcard) : BaseVCardGeoHandler<TContext>
    {
        decimal? latitude, longitude;

        protected async override ValueTask OnLatitude(decimal? value)
        {
            this.SetOnce(ref latitude, value);
        }

        protected async override ValueTask OnLongitude(decimal? value)
        {
            this.SetOnce(ref longitude, value);
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
        
        public async override ValueTask DisposeAsync()
        {
            this.AddList(ref vcard.GeographicalPositions, new() {
                Latitude = latitude ?? throw XmppStanzaException.BadRequest("Latitude is missing."),
                Longitude = longitude ?? throw XmppStanzaException.BadRequest("Longitude is missing."),
            });
        }
    }

    sealed class PersonParser(VCardPerson person) : BaseVCardPersonHandler<TContext>
    {
        protected async override ValueTask OnExternalValue(Uri? uri)
        {
            this.SetOnce(ref person.ExternalValue, uri);
        }

        protected async override ValueTask<IVCardHandler> OnVCard()
        {
            var vcard = this.SetOnce(ref person.VCard, new());
            return new VCardParser<TContext>(vcard) { Context = Context };
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
        public override ValueTask DisposeAsync() => default;
    }

    sealed class OrganizationParser(VCard vcard) : BaseVCardOrganizationHandler<TContext>
    {
        string? name;
        List<string>? units;

        protected async override ValueTask OnName(string? name)
        {
            this.SetOnce(ref this.name, name);
        }

        protected async override ValueTask OnUnit(string? text)
        {
            this.AddList(ref units, text);
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
        
        public async override ValueTask DisposeAsync()
        {
            this.AddList(ref vcard.Organizations, new() {
                Name = name ?? throw XmppStanzaException.BadRequest("Organization name is missing."),
                Units = units
            });
        }
    }

    sealed class CategoriesParser(VCard vcard) : BaseVCardCategoriesHandler<TContext>
    {
        List<string>? keywords;

        protected async override ValueTask OnKeyword(string? keyword)
        {
            this.AddList(ref keywords, keyword);
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
        
        public async override ValueTask DisposeAsync()
        {
            this.AddList(ref vcard.CategoriesKeywords, keywords ?? throw XmppStanzaException.BadRequest("Category keywords are missing."));
        }
    }

    sealed class PronunciationParser(VCardPronunciation pronunciation) : BaseVCardPronunciationHandler<TContext>
    {
        protected async override ValueTask OnExternalValue(Uri? uri)
        {
            this.SetOnce(ref pronunciation.ExternalValue, uri);
        }

        protected async override ValueTask OnBinaryValue(TemporaryFile? data)
        {
            this.SetOnce(ref pronunciation.BinaryValue, data);
        }

        protected async override ValueTask OnPhoneticTranscription(string? text)
        {
            this.SetOnce(ref pronunciation.PhoneticTranscription, text);
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
        public override ValueTask DisposeAsync() => default;
    }

    sealed class PrivacyClassificationParser(VCard vcard) : BaseVCardPrivacyClassificationHandler<TContext>
    {
        protected async override ValueTask OnIsPublic()
        {
            this.SetOnce(ref vcard.PrivacyClassification, VCardPrivacyClassification.Public);
        }

        protected async override ValueTask OnIsPrivate()
        {
            this.SetOnce(ref vcard.PrivacyClassification, VCardPrivacyClassification.Private);
        }

        protected async override ValueTask OnIsConfidential()
        {
            this.SetOnce(ref vcard.PrivacyClassification, VCardPrivacyClassification.Confidential);
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
        public override ValueTask DisposeAsync() => default;
    }

    sealed class CredentialParser(VCard vcard) : BaseVCardCredentialHandler<TContext>
    {
        string? type, value;

        protected async override ValueTask OnType(string? type)
        {
            this.SetOnce(ref this.type, type);
        }

        protected async override ValueTask OnValue(string? value)
        {
            this.SetOnce(ref this.value, value);
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
        
        public async override ValueTask DisposeAsync()
        {
            this.AddList(ref vcard.Credentials, new() {
                Type = type,
                Value = value ?? throw XmppStanzaException.BadRequest("Key value is missing.")
            });
        }
    }
}
