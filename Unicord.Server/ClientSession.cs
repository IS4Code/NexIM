using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unicord.Server.Accounts;
using Unicord.Server.Events;

namespace Unicord.Server;

/// <summary>
/// Represents an active connected session from a client.
/// </summary>
/// <remarks>
/// This class does not implement <see cref="IEventHandler"/>
/// because it does not perform routing; outgoing events
/// must be routed through <see cref="Outbound(Event)"/>
/// while incoming events through <see cref="Inbound(Event)"/>.
/// </remarks>
public abstract class ClientSession : IAsyncDisposable
{
    static readonly PresenceData DefaultPresence = new()
    {
        Presentation = default,
        Status = default,
        Priority = default
    };

    const bool ReportUnavailableStatus = false;

    public Account Account { get; }

    /// <summary>
    /// The set of targets to which directed presence has been sent and which
    /// must be informed when this session is closed.
    /// </summary>
    readonly ConcurrentDictionary<Identifier, PresenceData> directedPresence = new();

    /// <summary>
    /// The unique local identifier of this session for the account.
    /// </summary>
    public string Resource { get; private set; }

    /// <summary>
    /// The full identifier of this session.
    /// </summary>
    public Identifier Identifier => new(Account.Name, Resource);

    /// <summary>
    /// The priority of this session amongst others.
    /// </summary>
    public sbyte Priority => Presence.Priority ?? 0;

    /// <summary>
    /// The last presence data of this session.
    /// </summary>
    public PresenceData Presence { get; protected set; } = DefaultPresence;

    public ClientSession(Account account, string? resource)
    {
        this.Account = account;
        Resource = resource ?? Guid.NewGuid().ToString("N");
    }

    public void Bind(string resource)
    {
        Resource = resource;

        // TODO Handle when already exists (conflict)
        Account.AddSession(this);
    }

    static readonly Func<Identifier, PresenceData, PresenceData> addFactory = (_, value) => value;
    static readonly Func<Identifier, PresenceData, PresenceData, PresenceData> keepOriginalIfSameUpdateFactory = (_, previous, updated) => previous == updated ? previous : updated;

    public async ValueTask<ErrorCode> Inbound(Event evnt)
    {
        if(evnt.From != Identifier)
        {
            // Events originating from a session must be correctly identified
            return ErrorCode.InvalidRequest;
        }

        switch(evnt)
        {
            case StatusUpdateEvent statusEvent:
                var to = statusEvent.To;
                var presence = statusEvent.Data;

                if(presence == DefaultPresence)
                {
                    // Deduplicate
                    presence = DefaultPresence;
                }

                if(to.IsEmpty)
                {
                    // Broadcast event

                    if(presence == Presence)
                    {
                        // No update
                        return ErrorCode.Success;
                    }

                    if(presence.Status.Availability != Availability.Unavailable && Presence.Status.Availability == Availability.Unavailable)
                    {
                        // Going live - probe other contacts
                        await ProbeContacts(presence);
                    }

                    // Preserve the status data
                    Presence = presence;

                    Account.AddOrUpdateSession(this);
                    break;
                }

                List<Identifier>? notUpdatedRecipients = null;

                // Maintain directed presence
                foreach(var target in statusEvent.To)
                {
                    // Update current presence
                    var result = directedPresence.AddOrUpdate(target, addFactory, keepOriginalIfSameUpdateFactory, presence);
                    if(!Object.ReferenceEquals(result, presence))
                    {
                        // Not updated - remove from to
                        (notUpdatedRecipients ??= new()).Add(target);
                    }
                    else if(Object.ReferenceEquals(presence, DefaultPresence))
                    {
                        // No extra information other than unavailable, can be removed
                        directedPresence.TryRemove(new(target, presence));
                    }
                }

                if(notUpdatedRecipients != null)
                {
                    // Only keep recipients with differing presence
                    evnt = evnt.WithTo(to.RemoveRange(notUpdatedRecipients));
                }
                break;
        }
        return await Account.Post(evnt);
    }

    public ValueTask<ErrorCode> Outbound(Event evnt)
    {
        if(evnt is StatusRequestEvent)
        {
            // Respond with the last known presence immediately
            if(!directedPresence.TryGetValue(evnt.From, out var presence))
            {
                // Assume authorized to use global presence (otherwise the account should block it)
                presence = Presence;
            }
            if(!ReportUnavailableStatus && presence.Status.Availability == Availability.Unavailable)
            {
                // Not needed to report unavailable
                return new(ErrorCode.Success);
            }
            return Inbound(new StatusUpdateEvent
            {
                Origin = new()
                {
                    From = Identifier,
                    To = new(evnt.From),
                    TransactionIdentifier = default
                },
                Processing = EventProcessing.NewInternal(),
                Data = presence
            });
        }

        if(evnt is PresenceEvent && !evnt.To.Contains(Identifier) && Presence.Status.Availability == Availability.Unavailable)
        {
            // Not available for undirected presence
            return new(ErrorCode.NotAvailable);
        }

        return Write(evnt);
    }

    protected abstract ValueTask<ErrorCode> Write(Event evnt);

    protected internal abstract ValueTask<ErrorCode> ContactUpdated(Contact contact, ICollection<Contact> current);
    protected internal abstract ValueTask<ErrorCode> ContactRemoved(Contact contact, ICollection<Contact> current);

    private async ValueTask ProbeContacts(PresenceData data)
    {
        // Broadcast status information request of all contacts
        var evnt = new StatusRequestEvent
        {
            Origin = new()
            {
                From = Identifier,
                To = default,
                TransactionIdentifier = default
            },
            Processing = EventProcessing.NewInternal(),
            Data = data
        };

        await Account.Post(evnt);
    }

    public async ValueTask DisposeAsync()
    {
        Presence = DefaultPresence;

        // Check entities to which directed presence is maintained
        var to = new IdentifierSet(directedPresence.Where(p => p.Value.Status.Availability != Availability.Unavailable).Select(p => p.Key));
        directedPresence.Clear();

        if(to.IsEmpty)
        {
            // No such entities
            return;
        }

        var date = DateTime.UtcNow;
        var unavailableEvent = new StatusUpdateEvent
        {
            Origin = new()
            { 
                From = Identifier,
                To = to,
                TransactionIdentifier = null
            },
            Processing = EventProcessing.NewInternal(),
            Data = DefaultPresence
        };

        // Deliver this information
        await Account.Post(unavailableEvent);
    }
}
