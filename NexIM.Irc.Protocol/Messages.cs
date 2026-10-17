using System;
using System.Runtime.InteropServices;
using NexIM.Tools;

namespace NexIM.Irc.Protocol;

[StructLayout(LayoutKind.Auto)]
public readonly record struct CommandMessage(
    ByteStringView Command,
    MessageParameters Parameters = default,
    IrcAddress Source = default,
    NonEmptyDictionary<ByteStringView, ByteStringView>? ServerTags = null,
    NonEmptyDictionary<ByteStringView, ByteStringView>? ClientTags = null
)
{
    public bool ContainsTags => ServerTags != null || ClientTags != null;
}

[StructLayout(LayoutKind.Auto)]
public readonly record struct ReplyMessage(
    MessageReply Reply,
    MessageParameters Parameters = default,
    IrcAddress Source = default,
    NonEmptyDictionary<ByteStringView, ByteStringView>? ServerTags = null,
    NonEmptyDictionary<ByteStringView, ByteStringView>? ClientTags = null
)
{
    public bool ContainsTags => ServerTags != null || ClientTags != null;
}

[StructLayout(LayoutKind.Auto)]
public readonly struct MessageParameters(
    ByteStringView[]? parameters,
    int count
) : IEquatable<MessageParameters>
{
    ByteStringView[] args => parameters ?? Array.Empty<ByteStringView>();

    public int Count => count;
    public ByteStringView this[int index] => AsSpan()[index];

    public ReadOnlySpan<ByteStringView> AsSpan() => args.AsSpan(0, count);

    public Enumerator GetEnumerator()
    {
        return new(parameters ?? Array.Empty<ByteStringView>(), count);
    }

    public bool Equals(MessageParameters other)
    {
        if(count != other.Count)
        {
            return false;
        }
        var args = this.args;
        var args2 = other.args;
        for(int i = 0; i < count; i++)
        {
            if(!args[i].Equals(args2[i]))
            {
                return false;
            }
        }
        return true;
    }

    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator
    {
        readonly ByteStringView[] parameters;
        readonly int count;
        int index;

        internal Enumerator(ByteStringView[] parameters, int count)
        {
            this.parameters = parameters;
            this.count = count;
            index = -1;
        }

        public readonly ByteStringView Current => parameters[index];

        public bool MoveNext()
        {
            int next = index + 1;
            if(next >= count)
            {
                return false;
            }
            index = next;
            return true;
        }
    }
}

public enum MessageReply
{
    Welcome = 001,
    YourHost = 002,
    Created = 003,
    MyInfo = 004,
    ISupport = 005,
    Bounce = 010,
    Userhost = 302,
    IsOn = 303,
    Away = 301,
    Unaway = 305,
    NowAway = 306,
    WhoIsUser = 311,
    WhoIsServer = 312,
    WhoIsOperator = 313,
    WhoIsIdle = 317,
    EndOfWhoIs = 318,
    WhoIsChannels = 319,
    WhoWasUser = 314,
    EndOfWhoWas = 369,
    ListStart = 321,
    List = 322,
    ListEnd = 323,
    UniqOpIs = 325,
    ChannelModeIs = 324,
    NoTopic = 331,
    Topic = 332,
    Inviting = 341,
    Summoning = 342,
    InviteList = 346,
    EndOfInviteList = 347,
    ExceptList = 348,
    EndOfExceptList = 349,
    Version = 351,
    WhoReply = 352,
    EndOfWho = 315,
    NamReply = 353,
    EndOfNames = 366,
    Links = 364,
    EndOfLinks = 365,
    BANList = 367,
    EndOfBanList = 368,
    Info = 371,
    EndOfInfo = 374,
    MotdStart = 375,
    Motd = 372,
    EndOfMotd = 376,
    YoureOper = 381,
    Rehashing = 382,
    YoureService = 383,
    Time = 391,
    UsersSTART = 392,
    Users = 393,
    EndOfUsers = 394,
    NoUsers = 395,
    TraceLink = 200,
    TraceConnecting = 201,
    TraceHandShake = 202,
    TraceUnknown = 203,
    TraceOperator = 204,
    TraceUser = 205,
    TraceServer = 206,
    TraceService = 207,
    TraceNewType = 208,
    TraceClass = 209,
    TraceReconnect = 210,
    TraceLog = 261,
    TraceEnd = 262,
    StatsLinkInfo = 211,
    StatsCommands = 212,
    EndOfStats = 219,
    StatsUptime = 242,
    StatsOLine = 243,
    UModeIs = 221,
    ServList = 234,
    ServListEnd = 235,
    LUserClient = 251,
    LUserOP = 252,
    LUserUnknown = 253,
    LUserChannels = 254,
    LUserMe = 255,
    AdminMe = 256,
    AdminLoc1 = 257,
    AdminLoc2 = 258,
    AdminEmail = 259,
    TryAgain = 263,
    UnknownError = 400,
    NoSuchNick = 401,
    NoSuchServer = 402,
    NoSuchChannel = 403,
    CannotSendToChan = 404,
    TooManyChannels = 405,
    WasNoSuchNick = 406,
    TooManyTargets = 407,
    NoSuchService = 408,
    NoOrigin = 409,
    InvalidCapCmd = 410,
    NoRecipient = 411,
    NoTextToSend = 412,
    NoTopLevel = 413,
    WildTopLevel = 414,
    BadMask = 415,
    InputTooLong = 417,
    UnknownCommand = 421,
    NoMotd = 422,
    NoAdminInfo = 423,
    FileError = 424,
    NoNicknameGiven = 431,
    ErroneusNickname = 432,
    NicknameInUse = 433,
    NickCollision = 436,
    UnavailResource = 437,
    UserNotInChannel = 441,
    NotOnChannel = 442,
    UserOnChannel = 443,
    NoLogin = 444,
    SummonDisabled = 445,
    UsersDisabled = 446,
    NotRegistered = 451,
    NeedMoreParams = 461,
    AlreadyRegistered = 462,
    NoPermForHost = 463,
    PasswdMismatch = 464,
    YoureBannedCreep = 465,
    YouWillBeBanned = 466,
    KeySet = 467,
    ChannelIsFull = 471,
    UnknownMode = 472,
    InviteOnlyChan = 473,
    BannedFromChan = 474,
    BadChannelKey = 475,
    BadChanMask = 476,
    NoChanModes = 477,
    BanListFull = 478,
    NoPrivileges = 481,
    ChanOPrivsNeeded = 482,
    CantKillServer = 483,
    Restricted = 484,
    UniqOPrivsNeeded = 485,
    NoOperHost = 491,
    UModeUnknownFlag = 501,
    UsersDontMatch = 502,
    HelpNotFound = 524,
    InvalidKey = 525,
    StartTlsFailed = 691,
    InvalidModeParam = 696,
    NoPrivs = 723,
    ServiceInfo = 231,
    EndOfServices = 232,
    Service = 233,
    None = 300,
    WhoIsChanOp = 316,
    KillDone = 361,
    Closing = 362,
    CloseEnd = 363,
    InfoStart = 373,
    MyPortIs = 384,
    StatsCLine = 213,
    StatsNLine = 214,
    StatsILine = 215,
    StatsKLine = 216,
    StatsQLine = 217,
    StatsYLine = 218,
    StatsVLine = 240,
    StatsLLine = 241,
    StatsHLine = 244,
    StatsSLine = 244,
    StatsPing = 246,
    StatsBLine = 247,
    StatsDLine = 250,
    NoServiceHost = 492,

    // SASL
    LoggedIn = 900,
    LoggedOut = 901,
    NickLocked = 902,
    SaslSuccess = 903,
    SaslFail = 904,
    SaslTooLong = 905,
    SaslAborted = 906,
    SaslAlready = 907,
    SaslMechs = 908
}
