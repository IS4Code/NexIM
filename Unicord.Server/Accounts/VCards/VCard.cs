using System;
using System.Collections.Generic;
using Unicord.Primitives;

namespace Unicord.Server.Accounts.VCards;

public sealed class VCard
{
    public string? Version;
    public string? FormattedName;

    public string? FamilyName, GivenName, MiddleName, Prefix, Suffix;

    public string? Nicknames;
    public VCardMedia? Photo;
    public DateTimeOffset? Birthday;

    public List<VCardDeliveryAddress>? DeliveryAddresses;
    public List<VCardAddressLabel>? AddressLabels;
    public List<VCardTelephone>? Telephones;
    public List<VCardEmail>? Emails;

    public string? XmppAddress;
    public string? MailUserAgent;
    public TimeZoneOffset? TimeZone;
    public decimal? Latitude, Longitude;

    public string? Title, Role;

    public VCardMedia? Logo;
    public VCardPerson? AdministrativeAgent;
    public string? OrganizationName;
    public List<string>? OrganizationUnits;
    public List<string>? CategoriesKeywords;

    public string? Note;
    public string? VCardProduct;
    public DateTimeOffset? Revised;
    public string? SortString;

    public VCardPronunciation? Pronunciation;
    public string? UniqueIdentifier;
    public Uri? AssociatedUrl;

    public VCardPrivacyClassification? PrivacyClassification;
    public string? CredentialType, CredentialValue;

    public string? Description;
}

public class VCardResource
{
    public Uri? ExternalValue;
}

public class VCardData : VCardResource
{
    public TemporaryFile? BinaryValue;
}

public sealed class VCardMedia : VCardData
{
    public string? FormatType;
}

public class VCardIdentifier
{
    public VCardIdentifierFlags IdentifierFlags;
}

[Flags]
public enum VCardIdentifierFlags
{
    IsPreferred = 1,
    IsHome = 1 << 1,
    IsWork = 1 << 2
}

public class VCardAddress : VCardIdentifier
{
    public VCardAddressFlags AddressFlags;
    public VCardAddressType? AddressType;
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

public sealed class VCardDeliveryAddress : VCardAddress
{
    public string? PostOfficeBox;
    public string? ExtendedAddress;
    public string? StreetAddress;
    public string? Locality;
    public string? Region;
    public string? PostalCode;
    public string? Country;
}

public sealed class VCardAddressLabel : VCardAddress
{
    public List<string>? Lines;
}

public sealed class VCardTelephone : VCardIdentifier
{
    public VCardTelephoneFlags TelephoneFlags;

    public string? Number;
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

public sealed class VCardEmail : VCardIdentifier
{
    public VCardEmailFlags EmailFlags;
    public string? Identifier;
}

[Flags]
public enum VCardEmailFlags
{
    IsInternet = 1,
    IsX400 = 1 << 1
}

public sealed class VCardPerson : VCardResource
{
    public VCard? VCard;
}

public sealed class VCardPronunciation : VCardData
{
    public string? PhoneticTranscription;
}

public enum VCardPrivacyClassification
{
    Public, Private, Confidential
}
