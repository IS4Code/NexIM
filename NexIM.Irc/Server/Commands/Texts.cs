#pragma warning disable CS8981
global using bstring = NexIM.Irc.Protocol.ByteStringView;
#pragma warning restore CS8981

namespace NexIM.Irc.Server.Commands;

partial class Stream
{
    static class Texts
    {
        public static readonly bstring NotEnoughParameters = "Not enough parameters"u8.ToArray();
        public static readonly bstring AlreadyRegistered = "You may not reregister"u8.ToArray();
        public static readonly bstring NoNicknameGiven = "No nickname given"u8.ToArray();
        public static readonly bstring ErroneusNickname = "Erroneous nickname"u8.ToArray();
        public static readonly bstring NicknameInUse = "Nickname is already in use"u8.ToArray();
        public static readonly bstring PasswordIncorrect = "Password incorrect"u8.ToArray();
        public static readonly bstring NoMotd = "MOTD File is missing"u8.ToArray();
        public static readonly bstring NoSuchServer = "No such server"u8.ToArray();

        // SASL
        public static readonly bstring SaslFail = "SASL authentication failed"u8.ToArray();
        public static readonly bstring SaslAborted = "SASL authentication aborted"u8.ToArray();
        public static readonly bstring SaslSuccess = "SASL authentication successful"u8.ToArray();
        public static readonly bstring SaslAlready = "You have already authenticated using SASL"u8.ToArray();
        public static readonly bstring SaslTooLong = "SASL message too long"u8.ToArray();

        // CAP
        public static readonly bstring CapInvalidCommand = "Invalid CAP command"u8.ToArray();
    }
}
