using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Protocol;

[ComplexType, Namespace(VCardTemp)]
public interface IVCardHandler : IPayloadHandler
{
    [Name("VERSION")]
    ValueTask Version(string? version);

    [Name("FN")]
    ValueTask FormattedName(string? text);

    [Name("N")]
    ValueTask<IVCardNameHandler> Name();

    [Name("NICKNAME")]
    ValueTask Nickname(string? nickname);

    [Name("PHOTO")]
    ValueTask<IVCardMediaHandler> Photo();

    [Name("BDAY")]
    ValueTask Birthday(DateComponents? date);

    [Name("ADR")]
    ValueTask<IVCardDeliveryAddressHandler> DeliveryAddress();

    [Name("LABEL")]
    ValueTask<IVCardAddressLabelHandler> AddressLabel();

    [Name("TEL")]
    ValueTask<IVCardTelephoneHandler> Telephone();

    [Name("EMAIL")]
    ValueTask<IVCardEmailHandler> Email();

    [Name("JABBERID")]
    ValueTask XmppAddress(XmppAddress? address);

    [Name("MAILER")]
    ValueTask MailUserAgent(string? type);

    [Name("TZ")]
    ValueTask TimeZone(TimeZoneOffset? offset);

    [Name("GEO")]
    ValueTask<IVCardGeoHandler> GeographicalPosition();

    [Name("TITLE")]
    ValueTask Title(string? text);

    [Name("ROLE")]
    ValueTask Role(string? text);

    [Name("LOGO")]
    ValueTask<IVCardMediaHandler> Logo();

    [Name("AGENT")]
    ValueTask<IVCardPersonHandler> AdministrativeAgent();

    [Name("ORG")]
    ValueTask<IVCardOrganizationHandler> Organization();

    [Name("CATEGORIES")]
    ValueTask<IVCardCategoriesHandler> Categories();

    [Name("NOTE")]
    ValueTask Note(string? text);

    [Name("PRODID")]
    ValueTask VCardProduct(string? text);

    [Name("REV")]
    ValueTask Revised(DateComponents? date);

    [Name("SORT-STRING")]
    ValueTask SortString(string? value);

    [Name("SOUND")]
    ValueTask<IVCardPronunciationHandler> Pronunciation();

    [Name("UID")]
    ValueTask UniqueIdentifier(string? value);

    [Name("URL")]
    ValueTask AssociatedUrl(ValueUri? value);

    [Name("CLASS")]
    ValueTask<IVCardPrivacyClassificationHandler> PrivacyClassification();

    [Name("KEY")]
    ValueTask<IVCardCredentialHandler> Credential();

    [Name("DESC")]
    ValueTask Description(string? text);
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardNameHandler : IPayloadHandler
{
    [Name("FAMILY")]
    ValueTask FamilyName(string? name);

    [Name("GIVEN")]
    ValueTask GivenName(string? name);

    [Name("MIDDLE")]
    ValueTask MiddleName(string? name);

    [Name("PREFIX")]
    ValueTask Prefix(string? prefix);

    [Name("SUFFIX")]
    ValueTask Suffix(string? suffix);
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardResourceHandler : IPayloadHandler
{
    [Name("EXTVAL")]
    ValueTask ExternalValue(ValueUri? uri);
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardDataHandler : IVCardResourceHandler
{
    [Name("BINVAL")]
    ValueTask BinaryValue(Base64<TemporaryFile>? data);
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardMediaHandler : IVCardDataHandler
{
    [Name("TYPE")]
    ValueTask FormatType(string? type);
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardIdentifierHandler : IPayloadHandler
{
    [Name("HOME")]
    ValueTask IsHome();

    [Name("WORK")]
    ValueTask IsWork();

    [Name("PREF")]
    ValueTask IsPreferred();
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardAddressHandler : IVCardIdentifierHandler
{
    [Name("POSTAL")]
    ValueTask IsPostal();

    [Name("PARCEL")]
    ValueTask IsParcel();

    [Name("DOM")]
    ValueTask IsDomestic();

    [Name("INTL")]
    ValueTask IsInternational();
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardDeliveryAddressHandler : IVCardAddressHandler
{
    [Name("POBOX")]
    ValueTask PostOfficeBox(string? text);

    [Name("EXTADD")]
    ValueTask ExtendedAddress(string? text);

    [Name("STREET")]
    ValueTask StreetAddress(string? text);

    [Name("LOCALITY")]
    ValueTask Locality(string? text);

    [Name("REGION")]
    ValueTask Region(string? text);

    [Name("PCODE")]
    ValueTask PostalCode(string? text);

    [Name("CTRY")]
    ValueTask Country(string? text);
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardAddressLabelHandler : IVCardAddressHandler
{
    [Name("LINE")]
    ValueTask Line(string? text);
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardTelephoneHandler : IVCardIdentifierHandler
{
    [Name("VOICE")]
    ValueTask IsVoice();

    [Name("FAX")]
    ValueTask IsFax();

    [Name("PAGER")]
    ValueTask IsPager();

    [Name("MSG")]
    ValueTask IsText();

    [Name("CELL")]
    ValueTask IsCell();

    [Name("VIDEO")]
    ValueTask IsVideo();

    [Name("BBS")]
    ValueTask IsBbs();

    [Name("MODEM")]
    ValueTask IsModem();

    [Name("ISDN")]
    ValueTask IsIsdn();

    [Name("PCS")]
    ValueTask IsPcs();

    [Name("NUMBER")]
    ValueTask Number(string? number);
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardEmailHandler : IVCardIdentifierHandler
{
    [Name("INTERNET")]
    ValueTask IsInternet();

    [Name("X400")]
    ValueTask IsX400();

    [Name("USERID")]
    ValueTask Identifier(string? value);
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardGeoHandler : IPayloadHandler
{
    [Name("LAT")]
    ValueTask Latitude(decimal? value);

    [Name("LON")]
    ValueTask Longitude(decimal? value);
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardPersonHandler : IVCardResourceHandler
{
    [Name("vCard")]
    ValueTask<IVCardHandler> VCard();
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardOrganizationHandler : IPayloadHandler
{
    [Name("ORGNAME")]
    ValueTask Name(string? name);

    [Name("ORGUNIT")]
    ValueTask Unit(string? text);
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardCategoriesHandler : IPayloadHandler
{
    [Name("KEYWORD")]
    ValueTask Keyword(string? keyword);
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardPronunciationHandler : IVCardDataHandler
{
    [Name("PHONETIC")]
    ValueTask PhoneticTranscription(string? text);
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardPrivacyClassificationHandler : IPayloadHandler
{
    [Name("PUBLIC")]
    ValueTask IsPublic();

    [Name("PRIVATE")]
    ValueTask IsPrivate();

    [Name("CONFIDENTIAL")]
    ValueTask IsConfidential();
}

[ComplexType, Namespace(VCardTemp)]
public interface IVCardCredentialHandler : IPayloadHandler
{
    [Name("TYPE")]
    ValueTask Type(string? type);

    [Name("CRED")]
    ValueTask Value(string? value);
}
