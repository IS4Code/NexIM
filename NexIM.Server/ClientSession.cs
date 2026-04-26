using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Server.Accounts;
using NexIM.Server.Events;

namespace NexIM.Server;

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
    public Account Account { get; }

    public Server Server => Account.Server;

    /// <summary>
    /// The set of targets to which directed presence has been sent and which
    /// must be informed when this session is closed.
    /// </summary>
    readonly ConcurrentDictionary<Identifier, StatusUpdateEvent> directedPresence = new();

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

    StatusUpdateEvent? lastStatusUpdate = null;

    /// <summary>
    /// The last presence data of this session.
    /// </summary>
    public PresenceData Presence => lastStatusUpdate?.Data ?? PresenceData.Empty;

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

    public UploadedFile AcquireUploadedFile(TemporaryFile fileSource, string? name, string? contentType)
    {
        var file = UploadedFile.MoveFrom(fileSource, Account);
        file.Name = name;
        file.ContentType = contentType;
        Account.AddUploadedFile(file);
        return file;
    }

    public async ValueTask<StatusReports> Inbound(Event evnt)
    {
        // Ensure the event correctly identifies the session
        evnt = evnt.WithFrom(Identifier);

        switch(evnt)
        {
            case StatusUpdateEvent statusEvent:
                var to = statusEvent.To;

                if(to.Contains(Identifier.Bare))
                {
                    // Default presence for the session (broadcasted to contacts)
                    var previous = OnStatusUpdate(statusEvent);

                    if((statusEvent.Data.Status.Availability, previous?.Data.Status.Availability) is (not Availability.Unavailable, null or Availability.Unavailable))
                    {
                        // Going live - probe other contacts
                        await ProbeContacts();
                    }

                    if(to.Count == 1)
                    {
                        // No other target
                        break;
                    }
                }

                OnDirectedStatusUpdate(statusEvent);
                break;

            case QueryEvent { Data: RosterQueryData }:
                // Enables receiving roster events
                SubscribeToRosterUpdates();
                break;
        }
        return await Account.Post(evnt);
    }

    private StatusUpdateEvent? OnStatusUpdate(StatusUpdateEvent? statusEvent)
    {
        if(!Configuration.PreserveUnavailableStatus && statusEvent?.IsUnavailable == true)
        {
            statusEvent = null;
        }

        // Preserve the status data
        var previous = Interlocked.Exchange(ref lastStatusUpdate, statusEvent);

        Account.AddOrUpdateSession(this);

        return previous;
    }

    private void OnDirectedStatusUpdate(StatusUpdateEvent statusEvent)
    {
        // Maintain directed presence
        foreach(var target in statusEvent.To)
        {
            if(target == Identifier.Bare)
            {
                // Own account
                continue;
            }

            // Update current presence
            if(!Configuration.PreserveUnavailableStatus && statusEvent.IsUnavailable)
            {
                directedPresence.TryRemove(target, out _);
            }
            else
            {
                directedPresence[target] = statusEvent;
            }
        }
    }

    public async ValueTask<StatusReports> Outbound(Event evnt)
    {
        switch(evnt)
        {
            case StatusRequestEvent:
                // Respond with the last known presence immediately
                if(!directedPresence.TryGetValue(evnt.From, out var statusUpdate))
                {
                    if(!Account.CanSharePresenceWith(evnt.From))
                    {
                        // Only share undirected presence with contacts
                        return Report(StatusCode.SubscriptionRequired);
                    }
                    statusUpdate = lastStatusUpdate;
                }
                if(
                    statusUpdate is null ||
                    (!Configuration.PreserveUnavailableStatus && statusUpdate.IsUnavailable)
                )
                {
                    return Report(StatusCode.Unavailable);
                }
                return await Inbound(statusUpdate.WithTo(evnt.From));

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

    private async ValueTask ProbeContacts()
    {
        // Broadcast status information request of all contacts
        var evnt = new StatusRequestEvent {
            Origin = EventOrigin.FromTo(Identifier, Identifier.Bare),
            Processing = EventProcessing.Create(),
            Data = PresenceData.Empty
        };

        await Account.Post(evnt);
    }

    public async ValueTask DisposeAsync()
    {
        lastStatusUpdate = null;

        var builder = Identifiers.Builder.Empty;

        if(Presence.Status.Availability != Availability.Unavailable)
        {
            // Report to the account
            builder.Add(Identifier.Bare);
        }

        // Check entities to which directed presence is maintained
        foreach(var pair in directedPresence)
        {
            if(!pair.Value.IsUnavailable)
            {
                builder.Add(pair.Key);
            }
        }

        directedPresence.Clear();

        if(builder.TryToSet() is not { } to)
        {
            return;
        }

        var unavailableEvent = new StatusUpdateEvent {
            Origin = EventOrigin.FromTo(Identifier, to),
            Processing = EventProcessing.Create(),
            Data = PresenceData.Empty
        };

        // Deliver this information
        await Account.Post(unavailableEvent);
    }
}
