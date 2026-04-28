using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using MessagePack;
using NexIM.Primitives;

namespace NexIM.Server.Accounts.VCards;

[MessagePackObject]
public sealed class VCard
{
    /// <summary>
    /// Contains all data resources owned by the vCard.
    /// </summary>
    [IgnoreMember]
    public IEnumerable<VCardData> Data {
        get {
            var data = Photos ?? Enumerable.Empty<VCardData>();
            if(Logos is { } logos)
            {
                data = data.Concat(logos);
            }
            if(Pronunciations is { } pronunciations)
            {
                data = data.Concat(pronunciations);
            }
            if(AdministrativeAgents is { } agents)
            {
                foreach(var agent in agents)
                {
                    if(agent.VCard is { } agentVCard)
                    {
                        data = data.Concat(agentVCard.Data);
                    }
                }
            }
            return data;
        }
    }

    [Key(0)] public string? Version;
    [Key(1)] public string? FormattedName;

    [Key(2)] public string? FamilyName;
    [Key(3)] public string? GivenName;
    [Key(4)] public string? MiddleName;
    [Key(5)] public string? Prefix;
    [Key(6)] public string? Suffix;

    [Key(7)] public List<string>? Nicknames;
    [Key(8)] public List<VCardMedia>? Photos;
    [Key(9)] public DateComponents? Birthday;

    [Key(10)] public List<VCardDeliveryAddress>? DeliveryAddresses;
    [Key(11)] public List<VCardAddressLabel>? AddressLabels;
    [Key(12)] public List<VCardTelephone>? Telephones;
    [Key(13)] public List<VCardEmail>? Emails;

    [Key(14)] public List<string>? XmppAddresses;
    [Key(15)] public List<string>? MailUserAgents;
    [Key(16)] public List<TimeZoneOffset>? TimeZones;
    [Key(17)] public List<VCardGeographicalPosition>? GeographicalPositions;

    [Key(18)] public List<string>? Titles;
    [Key(19)] public List<string>? Roles;

    [Key(20)] public List<VCardMedia>? Logos;
    [Key(21)] public List<VCardPerson>? AdministrativeAgents;
    [Key(22)] public List<VCardOrganization>? Organizations;
    [Key(23)] public List<List<string>>? CategoriesKeywords;

    [Key(24)] public List<string>? Notes;
    [Key(25)] public string? VCardProduct;
    [Key(26)] public DateComponents? Revised;
    [Key(27)] public List<string>? SortStrings;

    [Key(28)] public List<VCardPronunciation>? Pronunciations;
    [Key(29)] public string? UniqueIdentifier;
    [Key(30)] public List<ValueUri>? AssociatedUrls;

    [Key(31)] public VCardPrivacyClassification? PrivacyClassification;
    [Key(32)] public List<VCardCredentials>? Credentials;

    [Key(33)] public List<string>? Descriptions;
}

public abstract class VCardResource
{
    [Key(0)] public ValueUri? ExternalValue;
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

[MessagePackObject]
[StructLayout(LayoutKind.Auto)]
public struct VCardGeographicalPosition
{
    [Key(0)] public decimal Latitude;
    [Key(1)] public decimal Longitude;
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
[StructLayout(LayoutKind.Auto)]
public struct VCardOrganization
{
    [Key(0)] public string Name;
    [Key(1)] public List<string>? Units;
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

[MessagePackObject]
[StructLayout(LayoutKind.Auto)]
public struct VCardCredentials
{
    [Key(0)] public string? Type;
    [Key(1)] public string Value;
}
