using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.Xmpp.Protocol;

[ComplexType, Namespace(DiscoInfo)]
public interface IDiscoInfoQueryHandler : IDataHandler
{
    [Name("identity")]
    ValueTask Identity([Name("name")] LanguageTaggedString? name, [Name("category")] Token<DiscoCategory>? category, [Name("type")] Token<DiscoType>? type);

    [Name("feature")]
    ValueTask Feature([Name("var")] Token<DiscoFeature>? feature);
}

[ComplexType, Namespace(DiscoItems)]
public interface IDiscoItemsQueryHandler : IPayloadHandler
{
    [Name("item")]
    ValueTask Item([Name("jid")] XmppResource? identifier, [Name("name")] LanguageTaggedString? name, [Name("node")] string? node);
}

[SimpleType]
public enum DiscoFeature
{
    [Name(Constants.DiscoInfo)] DiscoInfo,
    [Name(Constants.DiscoItems)] DiscoItems,
    [Name(Constants.ChatStates)] ChatStates,
    [Name(XmppPing)] Ping,
    [Name(XmppTime)] Time,
    [Name(AmpAction + "alert")] AmpActionAlert,
    [Name(AmpAction + "drop")] AmpActionDrop,
    [Name(AmpAction + "error")] AmpActionError,
    [Name(AmpAction + "notify")] AmpActionNotify,
    [Name(AmpCondition + "deliver")] AmpConditionDeliver,
    [Name(AmpCondition + "expire-at")] AmpConditionExpireAt,
    [Name(AmpCondition + "match-resource")] AmpConditionMatchResource,
    [Name(Caps)] EntityCapabilities,
    [Name(Caps + "#optimize")] OptimizedCapabilities
}

[SimpleType]
public enum DiscoNode
{
    [Name(Constants.Amp)] Amp
}

[SimpleType]
public enum DiscoCategory
{
    [Name("account")] Account,
    [Name("auth")] Authentication,
    [Name("authz")] Authorization,
    [Name("automation")] Automation,
    [Name("client")] Client,
    [Name("collaboration")] Collaboration,
    [Name("component")] Component,
    [Name("conference")] Conference,
    [Name("directory")] Directory,
    [Name("gateway")] Gateway,
    [Name("headline")] Headline,
    [Name("hierarchy")] Hierarchy,
    [Name("proxy")] Proxy,
    [Name("pubsub")] PubSub,
    [Name("server")] Server,
    [Name("store")] Store
}

[SimpleType]
public enum DiscoType
{
    // Account
    [Name("admin")] Admin,
    [Name("anonymous")] Anonymous,
    [Name("registered")] Registered,

    // Authentication
    [Name("cert")] Certificate,
    [Name("ntlm")] Ntlm,
    [Name("pam")] Pam,
    [Name("radius")] Radius,

    // Authentication or Store or Component
    [Name("generic")] Generic,

    // Authentication or Store
    [Name("ldap")] Ldap,

    // Authorization
    [Name("ephemeral")] Ephemeral,

    // Automation
    [Name("command-list")] CommandList,
    [Name("command-node")] CommandNode,
    [Name("rpc")] Rpc,
    [Name("soap")] Soap,
    [Name("translation")] Translation,

    // Client
    [Name("bot")] Bot,
    [Name("console")] Console,
    [Name("game")] Game,
    [Name("handheld")] Handheld,
    [Name("pc")] PC,
    [Name("phone")] Phone,
    [Name("tablet")] Tablet,
    [Name("web")] Web,

    // Client or Gateway
    [Name("sms")] Sms,

    // Collaboration
    [Name("whiteboard")] Whiteboard,

    // Component
    [Name("archive")] Archive,
    [Name("c2s")] ClientToServer,
    [Name("load")] Load,
    [Name("log")] Log,
    [Name("presence")] Presence,
    [Name("router")] Router,
    [Name("s2s")] ServerToServer,
    [Name("sm")] SM,
    [Name("stats")] Stats,

    // Conference
    [Name("text")] Text,

    // Conference or Gateway
    [Name("irc")] Irc,

    // Directory
    [Name("chatroom")] Chatroom,
    [Name("group")] Group,
    [Name("user")] User,
    [Name("waitinglist")] WaitingList,

    // Gateway
    [Name("aim")] Aim,
    [Name("discord")] Discord,
    [Name("facebook")] Facebook,
    [Name("gadu-gadu")] GaduGadu,
    [Name("http-ws")] HttpWebServices,
    [Name("icq")] Icq,
    [Name("lcs")] Lcs,
    [Name("mattermost")] Mattermost,
    [Name("mrim")] MailRuIM,
    [Name("msn")] Msn,
    [Name("myspaceim")] MySpaceIM,
    [Name("ocs")] OfficeCommunicationsServer,
    [Name("pstn")] Pstn,
    [Name("qq")] QQ,
    [Name("sametime")] Sametime,
    [Name("signal")] Signal,
    [Name("simple")] SipSimple,
    [Name("skype")] Skype,
    [Name("smtp")] Smtp,
    [Name("steam")] Steam,
    [Name("telegram")] Telegram,
    [Name("tlen")] Tlen,
    [Name("xfire")] Xfire,
    [Name("xmpp")] Xmpp,
    [Name("yahoo")] Yahoo,

    // Headline
    [Name("newmail")] NewMail,
    [Name("rss")] Rss,
    [Name("weather")] Weather,

    // Hierarchy
    [Name("branch")] Branch,

    // Hierarchy or PubSub
    [Name("leaf")] Leaf,

    // Proxy
    [Name("bytestreams")] Bytestreams,

    // PubSub
    [Name("collection")] Collection,
    [Name("pep")] PersonalEventingService,
    [Name("service")] Service,

    // Server
    [Name("im")] IM,

    // Store
    [Name("berkeley")] Berkeley,
    [Name("file")] File,
    [Name("mysql")] MySql,
    [Name("oracle")] Oracle,
    [Name("postgres")] PostgreSql
}
