using System;
using System.Collections.Concurrent;
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

    public NexServer Server => Account.Server;

    /// <summary>
    /// The set of targets to which directed presence has been sent and which
    /// must be informed when this session is closed.
    /// </summary>
    readonly ConcurrentDictionary<Identifier, StatusUpdateEvent> directedPresence = new();

    static readonly Func<string> resourceFactory = () => IdentifierHelper.CreateGuid(out _).ToString("N");
    
    // Backing field for explicitly set resources
    string? _resource;

    /// <summary>
    /// The unique local identifier of this session for the account.
    /// </summary>
    public string Resource {
        get => LazyInitializer.EnsureInitialized(ref _resource, resourceFactory);
        private set => _resource = value;
    }

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
    public bool ReceivesPresenceUpdates { get; private set; }

    public ClientSession(Account account, string? resource)
    {
        Account = account;
        Resource = resource!;
    }

    private StatusReport Report(StatusCode code)
    {
        return new(Identifier, code);
    }

    public async ValueTask<StatusCode> Bind(string resource)
    {
        Resource = resource;
        return await Account.AddSession(this);
    }

    protected void SubscribeToRosterUpdates()
    {
        ReceivesRosterUpdates = true;
    }

    protected void SubscribeToPresenceUpdates()
    {
        ReceivesPresenceUpdates = true;
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
                    // Enables receiving presence events
                    SubscribeToPresenceUpdates();

                    // Default presence for the session (broadcasted to contacts)
                    var status = await OnStatusUpdate(statusEvent);
                    if(status != StatusCode.Success)
                    {
                        return Report(status);
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

    private async ValueTask<StatusCode> OnStatusUpdate(StatusUpdateEvent? statusEvent)
    {
        if(!Configuration.PreserveUnavailableStatus && statusEvent?.IsUnavailable == true)
        {
            statusEvent = null;
        }

        // Preserve the status data
        var previous = Interlocked.Exchange(ref lastStatusUpdate, statusEvent);

        var status = await Account.AddOrUpdateSession(this);
        if(status != StatusCode.Success)
        {
            return status;
        }

        if((statusEvent?.Data.Status.Availability, previous?.Data.Status.Availability) is (not Availability.Unavailable, null or Availability.Unavailable))
        {
            // Going live - probe other contacts
            await ProbeContacts();
        }

        return status;
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
                    if(!await Account.CanSharePresenceWith(evnt.From))
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
