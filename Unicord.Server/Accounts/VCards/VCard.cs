using System;
using System.Collections.Generic;
using MessagePack;
using Unicord.Primitives;

namespace Unicord.Server.Accounts.VCards;

[MessagePackObject]
public sealed class VCard
{
    [Key(0)] public string? Version;
    [Key(1)] public string? FormattedName;

    [Key(2)] public string? FamilyName;
    [Key(3)] public string? GivenName;
    [Key(4)] public string? MiddleName;
    [Key(5)] public string? Prefix;
    [Key(6)] public string? Suffix;

    [Key(7)] public string? Nicknames;
    [Key(8)] public VCardMedia? Photo;
    [Key(9)] public DateTimeOffset? Birthday;

    [Key(10)] public List<VCardDeliveryAddress>? DeliveryAddresses;
    [Key(11)] public List<VCardAddressLabel>? AddressLabels;
    [Key(12)] public List<VCardTelephone>? Telephones;
    [Key(13)] public List<VCardEmail>? Emails;

    [Key(14)] public string? XmppAddress;
    [Key(15)] public string? MailUserAgent;
    [Key(16)] public TimeZoneOffset? TimeZone;
    [Key(17)] public decimal? Latitude;
    [Key(18)] public decimal? Longitude;

    [Key(19)] public string? Title;
    [Key(20)] public string? Role;

    [Key(21)] public VCardMedia? Logo;
    [Key(22)] public VCardPerson? AdministrativeAgent;
    [Key(23)] public string? OrganizationName;
    [Key(24)] public List<string>? OrganizationUnits;
    [Key(25)] public List<string>? CategoriesKeywords;

    [Key(26)] public string? Note;
    [Key(27)] public string? VCardProduct;
    [Key(28)] public DateTimeOffset? Revised;
    [Key(29)] public string? SortString;

    [Key(30)] public VCardPronunciation? Pronunciation;
    [Key(31)] public string? UniqueIdentifier;
    [Key(32)] public Uri? AssociatedUrl;

    [Key(33)] public VCardPrivacyClassification? PrivacyClassification;
    [Key(34)] public string? CredentialType;
    [Key(35)] public string? CredentialValue;

    [Key(36)] public string? Description;
}

public abstract class VCardResource
{
    [Key(0)] public Uri? ExternalValue;
}

public abstract class VCardData : VCardResource
{
    [Key(1)] public TemporaryFile? BinaryValue;
}

[MessagePackObject]
public sealed class VCardMedia : VCardData
{
    [Key(2)] public string? FormatType;
}

public abstract class VCardIdentifier
{
    [Key(0)] public VCardIdentifierFlags IdentifierFlags;
}

[Flags]
public enum VCardIdentifierFlags
{
    IsPreferred = 1,
    IsHome = 1 << 1,
    IsWork = 1 << 2
}

public abstract class VCardAddress : VCardIdentifier
{
    [Key(1)] public VCardAddressFlags AddressFlags;
    [Key(2)] public VCardAddressType? AddressType;
}

[Flags]
public enum VCardAddressFlags
{
    IsPostal = 1,
    IsParcel = 1 << 1
}

public enum VCardAddressType
{
    Domestic,
    International
}

[MessagePackObject]
public sealed class VCardDeliveryAddress : VCardAddress
{
    [Key(3)] public string? PostOfficeBox;
    [Key(4)] public string? ExtendedAddress;
    [Key(5)] public string? StreetAddress;
    [Key(6)] public string? Locality;
    [Key(7)] public string? Region;
    [Key(8)] public string? PostalCode;
    [Key(9)] public string? Country;
}

[MessagePackObject]
public sealed class VCardAddressLabel : VCardAddress
{
    [Key(3)] public List<string>? Lines;
}

[MessagePackObject]
public sealed class VCardTelephone : VCardIdentifier
{
    [Key(1)] public VCardTelephoneFlags TelephoneFlags;

    [Key(2)] public string? Number;
}

[Flags]
public enum VCardTelephoneFlags
{
    IsVoice = 1,
    IsFax = 1 << 1,
    IsPager = 1 << 2,
    IsText = 1 << 3,
    IsCell = 1 << 4,
    IsVideo = 1 << 5,
    IsBbs = 1 << 6,
    IsModem = 1 << 7,
    IsIsdn = 1 << 8,
    IsPcs = 1 << 9
}

[MessagePackObject]
public sealed class VCardEmail : VCardIdentifier
{
    [Key(1)] public VCardEmailFlags EmailFlags;
    [Key(2)] public string? Identifier;
}

[Flags]
public enum VCardEmailFlags
{
    IsInternet = 1,
    IsX400 = 1 << 1
}

[MessagePackObject]
public sealed class VCardPerson : VCardResource
{
    [Key(1)] public VCard? VCard;
}

[MessagePackObject]
public sealed class VCardPronunciation : VCardData
{
    [Key(2)] public string? PhoneticTranscription;
}

public enum VCardPrivacyClassification
{
    Public, Private, Confidential
}
