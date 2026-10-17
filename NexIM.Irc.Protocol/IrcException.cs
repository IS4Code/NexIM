using System;

namespace NexIM.Irc.Protocol;

public class IrcException : ApplicationException
{
    public override string? Message { get; }
    public MessageReply Reply { get; }

    internal IrcException(MessageReply reply)
    {
        Reply = reply;
    }

    public IrcException(string? message, MessageReply reply) : base(message)
    {
        Message = message;
        Reply = reply;
    }

    public IrcException(string? message, MessageReply reply, Exception? innerException) : base(message, innerException)
    {
        Message = message;
        Reply = reply;
    }

    public static IrcException NotRegistered(string? message = null) => new(message ?? "You have not registered", MessageReply.NotRegistered);
    public static IrcException NoOrigin(string? message = null) => new(message ?? "No origin specified", MessageReply.NoOrigin);
    public static IrcException NoTextToSend(string? message = null) => new(message ?? "No text to send", MessageReply.NoTextToSend);
    public static IrcException InputTooLong(string? message = null) => new(message ?? "Input line was too long", MessageReply.InputTooLong);
    public static IrcException NoMotd(string? message = null) => new(message ?? "MOTD File is missing", MessageReply.NoMotd);
    public static IrcException NoNicknameGiven(string? message = null) => new(message ?? "No nickname given", MessageReply.NoNicknameGiven);
    public static IrcException AlreadyRegistered(string? message = null) => new(message ?? "You may not reregister", MessageReply.AlreadyRegistered);
    public static IrcException PasswdMismatch(string? message = null) => new(message ?? "Password incorrect", MessageReply.PasswdMismatch);
    public static IrcException YoureBannedCreep(string? message = null) => new(message ?? "You are banned from this server.", MessageReply.YoureBannedCreep);
    public static IrcException NoPrivileges(string? message = null) => new(message ?? "Permission Denied- You're not an IRC operator", MessageReply.NoPrivileges);
    public static IrcException CantKillServer(string? message = null) => new(message ?? "You cant kill a server!", MessageReply.CantKillServer);
    public static IrcException NoOperHost(string? message = null) => new(message ?? "No O-lines for your host", MessageReply.NoOperHost);
    public static IrcException UModeUnknownFlag(string? message = null) => new(message ?? "Unknown MODE flag", MessageReply.UModeUnknownFlag);
    public static IrcException UsersDontMatch(string? message = null) => new(message ?? "Cant change mode for other users", MessageReply.UsersDontMatch);
    public static IrcException NickLocked(string? message = null) => new(message ?? "You must use a nick assigned to you", MessageReply.NickLocked);
    public static IrcException SaslFail(string? message = null) => new(message ?? "SASL authentication failed", MessageReply.SaslFail);
    public static IrcException SaslTooLong(string? message = null) => new(message ?? "SASL message too long", MessageReply.SaslTooLong);
    public static IrcException SaslAborted(string? message = null) => new(message ?? "SASL authentication aborted", MessageReply.SaslAborted);
    public static IrcException SaslAlready(string? message = null) => new(message ?? "You have already authenticated using SASL", MessageReply.SaslAlready);
}
