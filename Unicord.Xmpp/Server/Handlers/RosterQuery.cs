using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unicord.Primitives.Xml.Handlers;
using Unicord.Server.Accounts;
using Unicord.Server.Events;
using Unicord.Xmpp.Protocol;
using Unicord.Xmpp.Protocol.Handlers;
using Unicord.Xmpp.Server.Formats;

namespace Unicord.Xmpp.Server.Handlers;

internal class GetRosterQuery : BaseDelegatingRosterQueryHandler<CapturingHandler<IRosterQueryHandler>, EmptyDisposable, ICommandContext>
{
    protected sealed override CapturingHandler<IRosterQueryHandler> InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    internal string? Version { get; set; }

    private RosterQueryData GetData()
    {
        return new RosterQueryData {
            Roster = null,
            Tag = Version,
            Extensions = InnerHandler.ToExtensions()
        };
    }

    private RetrieveEvent GetEvent()
    {
        return new RetrieveEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = GetData()
        };
    }

    public async sealed override ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            this.Post(GetEvent());
        }
    }
}

internal abstract class DataRosterQuery : BaseDelegatingRosterQueryHandler<RosterParser<ICommandContext>, EmptyDisposable, ICommandContext>
{
    protected sealed override RosterParser<ICommandContext> InnerHandler { get; } = new();
    protected sealed override EmptyDisposable Disposable => default;

    internal string? Version { get; set; }

    protected abstract RosterQueryData GetData();

    protected abstract QueryEvent GetEvent();

    public async sealed override ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            this.Post(GetEvent());
        }
    }
}

internal sealed class SetRosterQuery : DataRosterQuery
{
    protected override RosterQueryData GetData()
    {
        if(InnerHandler.AddedContacts is { } added)
        {
            // Contacts are added
            if(!added.TryGetSingle(out var contact) || InnerHandler.RemovedContacts != null)
            {
                // Must be only one
                throw XmppStanzaException.BadRequest();
            }

            return new RosterUpdateData {
                Contact = contact,
                Roster = null,
                Tag = Version,
                Extensions = InnerHandler.ExtensionsHandler.ToExtensions()
            };
        }
        if(InnerHandler.RemovedContacts is { } removed)
        {
            // Contacts are removed
            if(!removed.TryGetSingle(out var contact) || InnerHandler.AddedContacts != null)
            {
                // Must be only one
                throw XmppStanzaException.BadRequest();
            }

            return new RosterRemoveData {
                Contact = contact,
                Roster = null,
                Tag = Version,
                Extensions = InnerHandler.ExtensionsHandler.ToExtensions()
            };
        }
        // Missing contacts
        throw XmppStanzaException.BadRequest();
    }

    protected override QueryEvent GetEvent()
    {
        return new UpdateEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = GetData()
        };
    }
}

internal sealed class ResultRosterQuery : DataRosterQuery
{
    protected override RosterQueryData GetData()
    {
        // Ignore removed contacts
        return new RosterQueryData {
            Tag = Version,
            Roster = InnerHandler.AddedContacts ?? (ICollection<Contact>)Array.Empty<Contact>()
        };
    }

    protected override QueryEvent GetEvent()
    {
        return new ResponseEvent {
            Origin = this.GetOrigin(),
            Processing = this.GetProcessing(),
            Data = GetData()
        };
    }
}
