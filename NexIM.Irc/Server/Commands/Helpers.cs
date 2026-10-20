using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NexIM.Irc.Protocol;
using NexIM.Irc.Tools;
using NexIM.Tools;

namespace NexIM.Irc.Server.Commands;

partial class Stream : ByteStringBuilder.IProvider
{
    StringTransformationCache<bstring> ByteStringBuilder.IProvider.Encoder => ByteStringCache.Global.Get(Encoding);
    IFormatProvider ByteStringBuilder.IProvider.FormatProvider => CultureInfo.InvariantCulture;
    ArrayPool<byte> ByteStringBuilder.IProvider.ArrayPool => ArrayPool<byte>.Shared;

    private ValueTask SendCommand(IrcEndPoint source, bstring command)
    {
        return SendCommandWithParams(source, command, ReadOnlySpan<bstring>.Empty);
    }

    private ValueTask SendCommand(IrcEndPoint source, bstring command, bstring param1)
    {
        return SendCommandWithParams(source, command, [param1]);
    }

    private ValueTask SendCommand(IrcEndPoint source, bstring command, bstring param1, bstring param2)
    {
        return SendCommandWithParams(source, command, [param1, param2]);
    }

    private ValueTask SendCommand(IrcEndPoint source, bstring command, bstring param1, bstring param2, bstring param3)
    {
        return SendCommandWithParams(source, command, [param1, param2, param3]);
    }

    private ValueTask SendCommand(IrcEndPoint source, bstring command, bstring param1, bstring param2, bstring param3, bstring param4)
    {
        return SendCommandWithParams(source, command, [param1, param2, param3, param4]);
    }

    private ValueTask SendCommand(IrcEndPoint source, bstring command, string param1)
    {
        return SendCommand(source, command, encoder.Get(param1));
    }

    private ValueTask SendCommand(IrcEndPoint source, bstring command, bstring param1, string param2)
    {
        return SendCommand(source, command, param1, encoder.Get(param2));
    }

    private ValueTask SendCommand(IrcEndPoint source, bstring command, bstring param1, bstring param2, string param3)
    {
        return SendCommand(source, command, param1, param2, encoder.Get(param3));
    }

    private ValueTask SendCommand(IrcEndPoint source, bstring command, bstring param1, bstring param2, bstring param3, string param4)
    {
        return SendCommand(source, command, param1, param2, param3, encoder.Get(param4));
    }

    private async ValueTask SendCommand(IrcEndPoint source, bstring command, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param1)
    {
        var encoded = param1.ToByteString(out var rented);
        try
        {
            await SendCommand(source, command, encoded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async ValueTask SendCommand(IrcEndPoint source, bstring command, bstring param1, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param2)
    {
        var encoded = param2.ToByteString(out var rented);
        try
        {
            await SendCommand(source, command, param1, encoded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async ValueTask SendCommand(IrcEndPoint source, bstring command, bstring param1, bstring param2, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param3)
    {
        var encoded = param3.ToByteString(out var rented);
        try
        {
            await SendCommand(source, command, param1, param2, encoded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async ValueTask SendCommand(IrcEndPoint source, bstring command, bstring param1, bstring param2, bstring param3, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param4)
    {
        var encoded = param4.ToByteString(out var rented);
        try
        {
            await SendCommand(source, command, param1, param2, param3, encoded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private ValueTask SendCommand(bstring command)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, command);
    }

    private ValueTask SendCommand(bstring command, bstring param1)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, command, param1);
    }

    private ValueTask SendCommand(bstring command, bstring param1, bstring param2)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, command, param1, param2);
    }

    private ValueTask SendCommand(bstring command, bstring param1, bstring param2, bstring param3)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, command, param1, param2, param3);
    }

    private ValueTask SendCommand(bstring command, bstring param1, bstring param2, bstring param3, bstring param4)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, command, param1, param2, param3);
    }

    private ValueTask SendCommand(bstring command, string param1)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, command, param1);
    }

    private ValueTask SendCommand(bstring command, bstring param1, string param2)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, command, param1, param2);
    }

    private ValueTask SendCommand(bstring command, bstring param1, bstring param2, string param3)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, command, param1, param2, param3);
    }

    private ValueTask SendCommand(bstring command, bstring param1, bstring param2, bstring param3, string param4)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, command, param1, param2, param3, param4);
    }

    private ValueTask SendCommand(bstring command, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param1)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, command, param1);
    }

    private ValueTask SendCommand(bstring command, bstring param1, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param2)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, command, param1, param2);
    }

    private ValueTask SendCommand(bstring command, bstring param1, bstring param2, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param3)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, command, param1, param2, param3);
    }

    private ValueTask SendCommand(bstring command, bstring param1, bstring param2, bstring param3, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param4)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, command, param1, param2, param3, param4);
    }

    private ValueTask SendCommand(IrcEndPoint source, MessageReply reply)
    {
        return SendCommandWithParams(source, reply, ReadOnlySpan<bstring>.Empty);
    }

    private ValueTask SendCommand(IrcEndPoint source, MessageReply reply, bstring param1)
    {
        return SendCommandWithParams(source, reply, [param1]);
    }

    private ValueTask SendCommand(IrcEndPoint source, MessageReply reply, bstring param1, bstring param2)
    {
        return SendCommandWithParams(source, reply, [param1, param2]);
    }

    private ValueTask SendCommand(IrcEndPoint source, MessageReply reply, bstring param1, bstring param2, bstring param3)
    {
        return SendCommandWithParams(source, reply, [param1, param2, param3]);
    }

    private ValueTask SendCommand(IrcEndPoint source, MessageReply reply, bstring param1, bstring param2, bstring param3, bstring param4)
    {
        return SendCommandWithParams(source, reply, [param1, param2, param3, param4]);
    }

    private ValueTask SendCommand(IrcEndPoint source, MessageReply reply, string param1)
    {
        return SendCommand(source, reply, encoder.Get(param1));
    }

    private ValueTask SendCommand(IrcEndPoint source, MessageReply reply, bstring param1, string param2)
    {
        return SendCommand(source, reply, param1, encoder.Get(param2));
    }

    private ValueTask SendCommand(IrcEndPoint source, MessageReply reply, bstring param1, bstring param2, string param3)
    {
        return SendCommand(source, reply, param1, param2, encoder.Get(param3));
    }

    private ValueTask SendCommand(IrcEndPoint source, MessageReply reply, bstring param1, bstring param2, bstring param3, string param4)
    {
        return SendCommand(source, reply, param1, param2, param3, encoder.Get(param4));
    }

    private async ValueTask SendCommand(IrcEndPoint source, MessageReply reply, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param1)
    {
        var encoded = param1.ToByteString(out var rented);
        try
        {
            await SendCommand(source, reply, encoded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async ValueTask SendCommand(IrcEndPoint source, MessageReply reply, bstring param1, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param2)
    {
        var encoded = param2.ToByteString(out var rented);
        try
        {
            await SendCommand(source, reply, param1, encoded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async ValueTask SendCommand(IrcEndPoint source, MessageReply reply, bstring param1, bstring param2, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param3)
    {
        var encoded = param3.ToByteString(out var rented);
        try
        {
            await SendCommand(source, reply, param1, param2, encoded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async ValueTask SendCommand(IrcEndPoint source, MessageReply reply, bstring param1, bstring param2, bstring param3, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param4)
    {
        var encoded = param4.ToByteString(out var rented);
        try
        {
            await SendCommand(source, reply, param1, param2, param3, encoded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private ValueTask SendCommand(MessageReply reply)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, reply);
    }

    private ValueTask SendCommand(MessageReply reply, bstring param1)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, reply, param1);
    }

    private ValueTask SendCommand(MessageReply reply, bstring param1, bstring param2)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, reply, param1, param2);
    }

    private ValueTask SendCommand(MessageReply reply, bstring param1, bstring param2, bstring param3)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, reply, param1, param2, param3);
    }

    private ValueTask SendCommand(MessageReply reply, bstring param1, bstring param2, bstring param3, bstring param4)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, reply, param1, param2, param3);
    }

    private ValueTask SendCommand(MessageReply reply, string param1)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, reply, param1);
    }

    private ValueTask SendCommand(MessageReply reply, bstring param1, string param2)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, reply, param1, param2);
    }

    private ValueTask SendCommand(MessageReply reply, bstring param1, bstring param2, string param3)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, reply, param1, param2, param3);
    }

    private ValueTask SendCommand(MessageReply reply, bstring param1, bstring param2, bstring param3, string param4)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, reply, param1, param2, param3, param4);
    }

    private ValueTask SendCommand(MessageReply reply, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param1)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, reply, param1);
    }

    private ValueTask SendCommand(MessageReply reply, bstring param1, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param2)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, reply, param1, param2);
    }

    private ValueTask SendCommand(MessageReply reply, bstring param1, bstring param2, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param3)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, reply, param1, param2, param3);
    }

    private ValueTask SendCommand(MessageReply reply, bstring param1, bstring param2, bstring param3, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param4)
    {
        var session = this.GetSession();
        return SendCommand(session.LocalAddress, reply, param1, param2, param3, param4);
    }

    private ValueTask SendReply(IrcEndPoint source, bstring command, IrcEndPoint target)
    {
        return SendCommand(source, command, target.ReplyTarget);
    }

    private ValueTask SendReply(IrcEndPoint source, bstring command, IrcEndPoint target, bstring param1)
    {
        return SendCommand(source, command, target.ReplyTarget, param1);
    }

    private ValueTask SendReply(IrcEndPoint source, bstring command, IrcEndPoint target, bstring param1, bstring param2)
    {
        return SendCommand(source, command, target.ReplyTarget, param1, param2);
    }

    private ValueTask SendReply(IrcEndPoint source, bstring command, IrcEndPoint target, bstring param1, bstring param2, bstring param3)
    {
        return SendCommand(source, command, target.ReplyTarget, param1, param2, param3);
    }

    private ValueTask SendReply(IrcEndPoint source, bstring command, IrcEndPoint target, string param1)
    {
        return SendReply(source, command, target, encoder.Get(param1));
    }

    private ValueTask SendReply(IrcEndPoint source, bstring command, IrcEndPoint target, bstring param1, string param2)
    {
        return SendReply(source, command, target, param1, encoder.Get(param2));
    }

    private ValueTask SendReply(IrcEndPoint source, bstring command, IrcEndPoint target, bstring param1, bstring param2, string param3)
    {
        return SendReply(source, command, target, param1, param2, encoder.Get(param3));
    }

    private async ValueTask SendReply(IrcEndPoint source, bstring command, IrcEndPoint target, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param1)
    {
        var encoded = param1.ToByteString(out var rented);
        try
        {
            await SendReply(source, command, target, encoded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async ValueTask SendReply(IrcEndPoint source, bstring command, IrcEndPoint target, bstring param1, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param2)
    {
        var encoded = param2.ToByteString(out var rented);
        try
        {
            await SendReply(source, command, target, param1, encoded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async ValueTask SendReply(IrcEndPoint source, bstring command, IrcEndPoint target, bstring param1, bstring param2, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param3)
    {
        var encoded = param3.ToByteString(out var rented);
        try
        {
            await SendReply(source, command, target, param1, param2, encoded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private ValueTask SendReply(bstring command)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, command, session.RemoteAddress);
    }

    private ValueTask SendReply(bstring command, bstring param1)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, command, session.RemoteAddress, param1);
    }

    private ValueTask SendReply(bstring command, bstring param1, bstring param2)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, command, session.RemoteAddress, param1, param2);
    }

    private ValueTask SendReply(bstring command, bstring param1, bstring param2, bstring param3)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, command, session.RemoteAddress, param1, param2, param3);
    }

    private ValueTask SendReply(bstring command, bstring param1, bstring param2, bstring param3, bstring param4)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, command, session.RemoteAddress, param1, param2, param3);
    }

    private ValueTask SendReply(bstring command, string param1)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, command, session.RemoteAddress, param1);
    }

    private ValueTask SendReply(bstring command, bstring param1, string param2)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, command, session.RemoteAddress, param1, param2);
    }

    private ValueTask SendReply(bstring command, bstring param1, bstring param2, string param3)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, command, session.RemoteAddress, param1, param2, param3);
    }

    private ValueTask SendReply(bstring command, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param1)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, command, session.RemoteAddress, param1);
    }

    private ValueTask SendReply(bstring command, bstring param1, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param2)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, command, session.RemoteAddress, param1, param2);
    }

    private ValueTask SendReply(bstring command, bstring param1, bstring param2, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param3)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, command, session.RemoteAddress, param1, param2, param3);
    }

    private ValueTask SendReply(IrcEndPoint source, MessageReply reply, IrcEndPoint target)
    {
        return SendCommand(source, reply, target.ReplyTarget);
    }

    private ValueTask SendReply(IrcEndPoint source, MessageReply reply, IrcEndPoint target, bstring param1)
    {
        return SendCommand(source, reply, target.ReplyTarget, param1);
    }

    private ValueTask SendReply(IrcEndPoint source, MessageReply reply, IrcEndPoint target, bstring param1, bstring param2)
    {
        return SendCommand(source, reply, target.ReplyTarget, param1, param2);
    }

    private ValueTask SendReply(IrcEndPoint source, MessageReply reply, IrcEndPoint target, bstring param1, bstring param2, bstring param3)
    {
        return SendCommand(source, reply, target.ReplyTarget, param1, param2, param3);
    }

    private ValueTask SendReply(IrcEndPoint source, MessageReply reply, IrcEndPoint target, string param1)
    {
        return SendReply(source, reply, target, encoder.Get(param1));
    }

    private ValueTask SendReply(IrcEndPoint source, MessageReply reply, IrcEndPoint target, bstring param1, string param2)
    {
        return SendReply(source, reply, target, param1, encoder.Get(param2));
    }

    private ValueTask SendReply(IrcEndPoint source, MessageReply reply, IrcEndPoint target, bstring param1, bstring param2, string param3)
    {
        return SendReply(source, reply, target, param1, param2, encoder.Get(param3));
    }

    private async ValueTask SendReply(IrcEndPoint source, MessageReply reply, IrcEndPoint target, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param1)
    {
        var encoded = param1.ToByteString(out var rented);
        try
        {
            await SendReply(source, reply, target, encoded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async ValueTask SendReply(IrcEndPoint source, MessageReply reply, IrcEndPoint target, bstring param1, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param2)
    {
        var encoded = param2.ToByteString(out var rented);
        try
        {
            await SendReply(source, reply, target, param1, encoded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async ValueTask SendReply(IrcEndPoint source, MessageReply reply, IrcEndPoint target, bstring param1, bstring param2, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param3)
    {
        var encoded = param3.ToByteString(out var rented);
        try
        {
            await SendReply(source, reply, target, param1, param2, encoded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private ValueTask SendReply(MessageReply reply)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, reply, session.RemoteAddress);
    }

    private ValueTask SendReply(MessageReply reply, bstring param1)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, reply, session.RemoteAddress, param1);
    }

    private ValueTask SendReply(MessageReply reply, bstring param1, bstring param2)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, reply, session.RemoteAddress, param1, param2);
    }

    private ValueTask SendReply(MessageReply reply, bstring param1, bstring param2, bstring param3)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, reply, session.RemoteAddress, param1, param2, param3);
    }

    private ValueTask SendReply(MessageReply reply, bstring param1, bstring param2, bstring param3, bstring param4)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, reply, session.RemoteAddress, param1, param2, param3);
    }

    private ValueTask SendReply(MessageReply reply, string param1)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, reply, session.RemoteAddress, param1);
    }

    private ValueTask SendReply(MessageReply reply, bstring param1, string param2)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, reply, session.RemoteAddress, param1, param2);
    }

    private ValueTask SendReply(MessageReply reply, bstring param1, bstring param2, string param3)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, reply, session.RemoteAddress, param1, param2, param3);
    }

    private ValueTask SendReply(MessageReply reply, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param1)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, reply, session.RemoteAddress, param1);
    }

    private ValueTask SendReply(MessageReply reply, bstring param1, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param2)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, reply, session.RemoteAddress, param1, param2);
    }

    private ValueTask SendReply(MessageReply reply, bstring param1, bstring param2, [InterpolatedStringHandlerArgument("")] ByteStringBuilder param3)
    {
        var session = this.GetSession();
        return SendReply(session.LocalAddress, reply, session.RemoteAddress, param1, param2, param3);
    }
}
