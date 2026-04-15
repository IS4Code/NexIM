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
        new() {
            Presentation = default,
            Status = default,
            Priority = null,
            Capabilities = null
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

    public bool ReceivesRosterUpdates { get; private set; }
    public bool ReceivesPresenceUpdates => Presence.Status.Availability != Availability.Unavailable;

    public ClientSession(Account account, string? resource)
    {
        Account = account;
        Resource = resource ?? Guid.NewGuid().ToString("N");
    }

    private StatusReport Report(StatusCode code)
    {
        return new(Identifier, code);
    }

    public void Bind(string resource)
    {
        Resource = resource;

        // TODO Handle when already exists (conflict)
        Account.AddSession(this);
    }

    private void SubscribeToRosterUpdates()
    {
        ReceivesRosterUpdates = true;
    }

    static readonly Func<Identifier, PresenceStore, PresenceStore> addFactory = (_, value) => value;
    static readonly Func<Identifier, PresenceStore, PresenceStore, PresenceStore> keepOriginalIfSameUpdateFactory = (_, previous, updated) => previous == updated ? previous : updated;

    public async ValueTask<StatusReports> Inbound(Event evnt)
    {
        // Ensure the event correctly identifies the session
        evnt = evnt.WithFrom(Identifier);

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

                if(to.Contains(Identifier.Bare))
                {
                    // Default presence for the session (broadcasted to contacts)
                    await OnPresence(presence, presenceLanguage);

                    if(to.Count == 1)
                    {
                        // No other target
                        break;
                    }
                }

                if(!OnDirectedPresence(ref to, presence, presenceLanguage))
                {
                    // Removed redundant directed presence targets are all recipients
                    return Report(StatusCode.Success);
                }

                evnt = evnt.WithTo(to);
                break;
            
            case QueryEvent { Data: RosterQueryData }:
                // Enables receiving roster events
                SubscribeToRosterUpdates();
                break;
        }
        return await Account.Post(evnt);
    }

    private async ValueTask OnPresence(PresenceData presence, LanguageCode? presenceLanguage)
    {
        if(presence == Presence && presenceLanguage == PresenceLanguage)
        {
            // No update
            return;
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
    }

    private bool OnDirectedPresence(ref Identifiers to, PresenceData presence, LanguageCode? presenceLanguage)
    {
        var presenceStore = new PresenceStore(presence, presenceLanguage);

        List<Identifier>? notUpdatedRecipients = null;

        // Maintain directed presence
        foreach(var target in to)
        {
            if(target == Identifier.Bare)
            {
                // Own account
                continue;
            }

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
            return to.TryRemoveRange(notUpdatedRecipients, out to);
        }
        return true;
    }

    public async ValueTask<StatusReports> Outbound(Event evnt)
    {
        switch(evnt)
        {
            case StatusRequestEvent:
                // Respond with the last known presence immediately
                if(!directedPresence.TryGetValue(evnt.From, out var presenceStore))
                {
                    if(!Account.CanSharePresenceWith(evnt.From))
                    {
                        // Only share undirected presence with contacts
                        return Report(StatusCode.SubscriptionRequired);
                    }
                    presenceStore = currentPresence;
                }
                if(!ReportUnavailableStatus && presenceStore.Data.Status.Availability == Availability.Unavailable)
                {
                    // Not needed to report unavailable
                    return Report(StatusCode.Success);
                }
                return await Inbound(new StatusUpdateEvent {
                    Origin = EventOrigin.FromTo(Identifier, evnt.From, presenceStore.Language),
                    Processing = EventProcessing.NewInternal(),
                    Data = presenceStore.Data
                });

            case PresenceEvent { Data: var data } when directedPresence.ContainsKey(evnt.From):
                if(data?.Status.Availability == Availability.Unavailable)
                {
                    // No longer relevant to receive
                    directedPresence.TryRemove(evnt.From, out _);
                }
                break;

            case PresenceEvent when !ReceivesPresenceUpdates:
                // Not available for undirected presence
                // TODO Invisible?
                return Report(StatusCode.Unavailable);

            case QueryEvent { Data: RosterQueryData } when !ReceivesRosterUpdates:
                // Not interested in roster updates
                return Report(StatusCode.Success);
        }

        return new StatusReport(Identifier, await Write(evnt));
    }

    protected abstract ValueTask<StatusCode> Write(Event evnt);

    private async ValueTask ProbeContacts(PresenceData data, LanguageCode? dataLanguage)
    {
        // Broadcast status information request of all contacts
        var evnt = new StatusRequestEvent {
            Origin = EventOrigin.FromTo(Identifier, Identifier.Bare, dataLanguage),
            Processing = EventProcessing.NewInternal(),
            Data = data
        };

        await Account.Post(evnt);
    }

    public async ValueTask DisposeAsync()
    {
        currentPresence = DefaultPresence;

        // Check entities to which directed presence is maintained
        var availableTo = directedPresence.Where(p => p.Value.Data.Status.Availability != Availability.Unavailable).Select(p => p.Key);
        if(!Identifiers.TryCreateRange(availableTo, out var to))
        {
            // No such entities
            directedPresence.Clear();
            return;
        }
        directedPresence.Clear();

        var date = DateTime.UtcNow;
        var unavailableEvent = new StatusUpdateEvent {
            Origin = new() { 
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
