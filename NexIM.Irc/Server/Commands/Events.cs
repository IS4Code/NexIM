using System.Threading.Tasks;
using NexIM.Irc.Protocol;
using NexIM.Server.Accounts;
using NexIM.Server.Events;
using NexIM.Tools;

namespace NexIM.Irc.Server.Commands;

partial class Stream
{
    private ValueTask PrivMsgCommand(in CommandMessage message)
    {
        if(message.Parameters.Count < 2)
        {
            return SendReply(MessageReply.NeedMoreParams, message.Command, Texts.NotEnoughParameters);
        }

        var session = this.GetSession();
        
        var content = MessageBodyCollection.Empty.Data.ToBuilder();
        content[(MessageFormat.Plain, default)] = Encoding.GetString(message.Parameters[1]);

        var data = new MessageData {
            State = ConversationState.Unspecified,
            Presentation = new Presentation { Nickname = Encoding.GetString(session.RemoteAddress.Nickname) },
            Body = new(content.ToImmutable()),
            ParentThreadIdentifier = null,
            Subject = null,
            ThreadIdentifier = null
        };
        return SendMessage(message.Parameters[1], data);
    }

    private async ValueTask SendMessage(bstring targets, MessageData data)
    {
        var session = this.GetSession();
        var source = session.RemoteAddress.ToIdentifier(session);
        GetTargets(targets, out var users, out var channels);
        var processing = EventProcessing.Create();
        if(users is { } usersVal)
        {
            var evnt = new MessageEvent {
                Type = MessageType.Chat,
                Origin = EventOrigin.FromTo(source, usersVal),
                Processing = processing,
                Data = data
            };

            var result = await this.GetClientSession().Inbound(evnt);
            foreach(var report in result)
            {
                await HandleReport(evnt, report);
            }
        }
        // TODO Parallel
        if(channels is { } channelsVal)
        {
            var evnt = new MessageEvent {
                Type = MessageType.GroupChat,
                Origin = EventOrigin.FromTo(source, channelsVal),
                Processing = processing,
                Data = data
            };

            var result = await this.GetClientSession().Inbound(evnt);
            foreach(var report in result)
            {
                await HandleReport(evnt, report);
            }
        }
    }

    private void GetTargets(bstring targets, out NonEmptySet<Identifier>? users, out NonEmptySet<Identifier>? channels)
    {
        var usersBuilder = NonEmptySet<Identifier>.Builder.Empty;
        var channelsBuilder = NonEmptySet<Identifier>.Builder.Empty;

        var span = targets.AsSpan();
        bool next;
        do
        {
            next = span.TrySplitAfter((byte)',', out var target);

            if(!target.IsEmpty)
            {
                var id = GetTarget(new(targets, target), out var channelType);
                if(channelType != null)
                {
                    // TODO Interpret channel type
                    channelsBuilder.Add(id);
                }
                else
                {
                    usersBuilder.Add(id);
                }
            }
        }
        while(next);

        users = usersBuilder.TryToSet();
        channels = channelsBuilder.TryToSet();
    }

    private Identifier GetTarget(bstring target, out IrcChannelType? channelType)
    {
        // TODO Error handling
        var addr = new IrcTarget(target);
        channelType = addr.TryToChannel(out var channel) ? channel.Type : null;
        return addr.ToIdentifier(this.GetSession());
    }
}
