using System;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using NexIM.Irc.Protocol;
using NexIM.Irc.Tools;
using NexIM.Tools;

namespace NexIM.Irc.Server.Commands;

partial class Stream
{
    const Capabilities knownCapabilities = Capabilities.Sasl | Capabilities.MessageTags;
    const int knownCapabilitiesCount = 2;

    bool capNegotiating;
    int capVersion;

    static readonly bstring capAck = "ACK"u8.ToArray();
    static readonly bstring capNak = "NAK"u8.ToArray();
    private ValueTask CapCommand(in CommandMessage message)
    {
        if(message.Parameters.Count < 1)
        {
            return SendReply(MessageReply.NeedMoreParams, message.Command, Texts.NotEnoughParameters);
        }

        var session = this.GetSession();

        var subCommand = message.Parameters[0];
        var command = subCommand.AsSpan();
        var rest = command.Slice(1);
        var args = message.Parameters.AsSpan().Slice(1);
        switch(command[0])
        {
            case (byte)'l' or (byte)'L':
                if(Ascii.EqualsIgnoreCase(rest, "S"u8))
                {
                    // CAP LS
                    if(!session.IsAuthenticated)
                    {
                        // Enter CAP negotiation
                        capNegotiating = true;
                    }
                    int currentVersion = capVersion;
                    if(args.Length > 0)
                    {
                        if(!Int32.TryParse(args[0], out currentVersion))
                        {
                            // Version not recognized
                            break;
                        }
                        if(currentVersion > capVersion)
                        {
                            // Store if higher
                            capVersion = currentVersion;
                        }
                    }
                    return SendCapabilities(message.Command, subCommand, knownCapabilities, currentVersion >= 302);
                }

                if(Ascii.EqualsIgnoreCase(rest, "IST"u8))
                {
                    // CAP LIST
                    return SendCapabilities(message.Command, subCommand, session.Capabilities, withValues: false);
                }
                break;

            case (byte)'r' or (byte)'R':
                if(Ascii.EqualsIgnoreCase(rest, "EQ"u8))
                {
                    // CAP REQ
                    if(!session.IsAuthenticated)
                    {
                        capNegotiating = true;
                    }
                    if(args.Length < 1)
                    {
                        break;
                    }
                    var requested = args[0];
                    var capabilities = session.Capabilities;
                    if(!TryRequestCapabilities(requested.AsSpan(), ref capabilities))
                    {
                        return SendReply(message.Command, capNak, requested);
                    }
                    session.Capabilities = capabilities;
                    return SendReply(message.Command, capAck, requested);
                }
                break;

            case (byte)'e' or (byte)'E':
                if(Ascii.EqualsIgnoreCase(rest, "ND"u8))
                {
                    // CAP END
                    capNegotiating = false;
                    return TryCompleteRegistration();
                }
                break;
        }
        return SendReply(MessageReply.InvalidCapCmd, subCommand, Texts.CapInvalidCommand);
    }

    private static bool TryGetCapability(ReadOnlySpan<byte> name, out Capabilities capability)
    {
        var rest = name.Slice(1);
        switch(name[0])
        {
            case (byte)'s' or (byte)'S':
                if(Ascii.EqualsIgnoreCase(rest, "asl"u8))
                {
                    // sasl
                    capability = Capabilities.Sasl;
                    return true;
                }
                break;
            case (byte)'m' or (byte)'M':
                if(Ascii.EqualsIgnoreCase(rest, "essage-tags"u8))
                {
                    // message-tags
                    capability = Capabilities.MessageTags;
                    return true;
                }
                break;
        }
        capability = Capabilities.None;
        return false;
    }

    private bool TryRequestCapabilities(ReadOnlySpan<byte> span, ref Capabilities capabilities)
    {
        while(!span.IsEmpty)
        {
            span.TrySplitAfter((byte)' ', out var name);
            var disable = name.TryMatchFirst((byte)'-');
            if(name.IsEmpty)
            {
                continue;
            }

            if(!TryGetCapability(name, out var capability))
            {
                return false;
            }

            if(disable)
            {
                capabilities &= ~capability;
            }
            else
            {
                capabilities |= capability;
            }
        }
        return true;
    }

    private async ValueTask SendCapabilities(bstring command, bstring subCommand, Capabilities included, bool withValues)
    {
        var builder = new ByteStringBuilder(0, knownCapabilitiesCount, this);

        if((included & Capabilities.MessageTags) != 0)
        {
            builder.AppendLiteral("message-tags");
        }

        if((included & Capabilities.Sasl) != 0)
        {
            if(!builder.IsEmpty)
            {
                builder.AppendLiteral(" ");
            }
            builder.AppendLiteral("sasl");
            if(withValues)
            {
                bool first = true;
                foreach(var mechanism in this.GetServer().GetSaslMechanisms(this.GetSession()))
                {
                    if(first)
                    {
                        builder.AppendLiteral("=");
                        first = false;
                    }
                    else
                    {
                        builder.AppendLiteral(",");
                    }
                    builder.AppendFormatted(mechanism);
                }
            }
        }

        var caps = builder.ToByteString(out var rented);
        try
        {
            await SendReply(command, subCommand, caps);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
