using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unicord.Primitives;
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
    static readonly PresenceStore DefaultPresence = new(
        new()
        {
            Presentation = default,
            Status = default,
            Priority = default
        }
    );

    const bool ReportUnavailableStatus = false;

    public Account Account { get; }

    public Server Server => Account.Server;

    /// <summary>
    /// The set of targets to which directed presence has been sent and which
    /// must be informed when this session is closed.
    /// </summary>
    readonly ConcurrentDictionary<Identifier, PresenceStore> directedPresence = new();

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

    PresenceStore currentPresence = DefaultPresence;

    /// <summary>
    /// The last presence data of this session.
    /// </summary>
    public PresenceData Presence => currentPresence.Data;

    /// <summary>
    /// The language of the last presence data of this session.
    /// </summary>
    public LanguageCode? PresenceLanguage => currentPresence.Language;

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

    static readonly Func<Identifier, PresenceStore, PresenceStore> addFactory = (_, value) => value;
    static readonly Func<Identifier, PresenceStore, PresenceStore, PresenceStore> keepOriginalIfSameUpdateFactory = (_, previous, updated) => previous == updated ? previous : updated;

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

                if(presence is null || presence == DefaultPresence.Data)
                {
                    // Deduplicate
                    presence = DefaultPresence.Data;
                }

                // Remember original language
                var presenceLanguage = evnt.Origin.TransactionLanguage;

                if(to.IsEmpty)
                {
                    // Broadcast event

                    if(presence == Presence && presenceLanguage == PresenceLanguage)
                    {
                        // No update
                        return ErrorCode.Success;
                    }

                    var previousStatus = Presence.Status;

                    // Preserve the status data
                    currentPresence = new(presence, presenceLanguage);

                    Account.AddOrUpdateSession(this);

                    if(presence.Status.Availability != Availability.Unavailable && previousStatus.Availability == Availability.Unavailable)
                    {
                        // Going live - probe other contacts
                        await ProbeContacts(presence, presenceLanguage);
                    }

                    break;
                }

                var presenceStore = new PresenceStore(presence, presenceLanguage);

                List<Identifier>? notUpdatedRecipients = null;

                // Maintain directed presence
                foreach(var target in statusEvent.To)
                {
                    // Update current presence
                    var result = directedPresence.AddOrUpdate(target, addFactory, keepOriginalIfSameUpdateFactory, presenceStore);
                    if(!Object.ReferenceEquals(result, presenceStore))
                    {
                        // Not updated - remove from to
                        (notUpdatedRecipients ??= new()).Add(target);
                    }
                    else if(Object.ReferenceEquals(presence, DefaultPresence.Data))
                    {
                        // No extra information other than unavailable, can be removed
                        directedPresence.TryRemove(new(target, presenceStore));
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
            if(!directedPresence.TryGetValue(evnt.From, out var presenceStore))
            {
                // Assume authorized to use global presence (otherwise the account should block it)
                presenceStore = currentPresence;
            }
            if(!ReportUnavailableStatus && presenceStore.Data.Status.Availability == Availability.Unavailable)
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
                    TransactionIdentifier = default,
                    TransactionLanguage = presenceStore.Language
                },
                Processing = EventProcessing.NewInternal(),
                Data = presenceStore.Data
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

    private async ValueTask ProbeContacts(PresenceData data, LanguageCode? dataLanguage)
    {
        // Broadcast status information request of all contacts
        var evnt = new StatusRequestEvent
        {
            Origin = new()
            {
                From = Identifier,
                To = default,
                TransactionIdentifier = default,
                TransactionLanguage = dataLanguage
            },
            Processing = EventProcessing.NewInternal(),
            Data = data
        };

        await Account.Post(evnt);
    }

    public async ValueTask DisposeAsync()
    {
        currentPresence = DefaultPresence;

        // Check entities to which directed presence is maintained
        var to = new IdentifierSet(directedPresence.Where(p => p.Value.Data.Status.Availability != Availability.Unavailable).Select(p => p.Key));
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
                TransactionIdentifier = null,
                TransactionLanguage = null
            },
            Processing = EventProcessing.NewInternal(),
            Data = DefaultPresence.Data
        };

        // Deliver this information
        await Account.Post(unavailableEvent);
    }

    record PresenceStore(PresenceData Data, LanguageCode? Language = default);
}
