using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using NexIM.Server.Events;

namespace NexIM.Server.Accounts;

partial class Account
{
    readonly ConcurrentDictionary<Guid, ContactPresenceCache> contactsPresenceCache = new();

    private bool TryGetLastContactPresence(Identity id, DateTimeOffset? when, string? resource, [MaybeNullWhen(false)] out PresenceEvent evnt)
    {
        if(!contactsPresenceCache.TryGetValue(id.Identifier, out var cache))
        {
            evnt = null;
            return false;
        }
        return cache.TryGetLast(resource, when, out evnt);
    }

    private bool TryGetLastContactPresence(Identity id, DateTimeOffset? when, [MaybeNullWhen(false)] out ICollection<PresenceEvent> events)
    {
        if(!contactsPresenceCache.TryGetValue(id.Identifier, out var cache))
        {
            events = null;
            return false;
        }
        return cache.TryGetLast(when, out events);
    }

    static readonly Func<Guid, PresenceEvent, ContactPresenceCache> createRequestCache = (_, evnt) => {
        var cache = new ContactPresenceCache();
        cache.OnRequested(evnt);
        return cache;
    };
    static readonly Func<Guid, ContactPresenceCache, PresenceEvent, ContactPresenceCache> updateRequestCache = (_, cache, evnt) => {
        cache.OnRequested(evnt);
        return cache;
    };

    private void OnStatusRequested(Identity id, PresenceEvent statusEvent)
    {
        if(contacts.ContainsKey(id.Identifier))
        {
            // Update request time
            contactsPresenceCache.AddOrUpdate(id.Identifier, createRequestCache, updateRequestCache, statusEvent);
        }
    }

    static readonly Func<Guid, PresenceEvent, ContactPresenceCache> createStatusCache = (_, evnt) => {
        var cache = new ContactPresenceCache();
        cache.Update(evnt);
        return cache;
    };
    static readonly Func<Guid, ContactPresenceCache, PresenceEvent, ContactPresenceCache> updateStatusCache = (_, cache, evnt) => {
        cache.Update(evnt);
        return cache;
    };

    private async ValueTask<StatusReports> OnStatusUpdate(PresenceEvent statusEvent)
    {
        if(statusEvent.From.Account is { } name)
        {
            if(await Server.FindIdentity(name) is { } id)
            {
                // Must be an existing identity (to correspond to a valid contact)
                OnStatusUpdate(id, statusEvent);
            }
        }
        return Report(StatusCode.Received);
    }

    private void OnStatusUpdate(Identity id, PresenceEvent statusEvent)
    {
        if(contacts.ContainsKey(id.Identifier))
        {
            // Store presence of a valid contact
            contactsPresenceCache.AddOrUpdate(id.Identifier, createStatusCache, updateStatusCache, statusEvent);
        }
    }

    sealed class ContactPresenceCache
    {
        const string accountKey = "";

        readonly ConcurrentDictionary<string, PresenceEvent> sessionsCache = new(StringComparer.Ordinal);
        DateTime? lastRequested;

        public void OnRequested(PresenceEvent statusEvent)
        {
            // Store known time of the probe
            lastRequested = statusEvent.Published.UtcDateTime;
        }

        public void Update(PresenceEvent evnt)
        {
            var key = evnt.From.Resource ?? accountKey;

            // Update publish time (local sessions are not kept republished)
            // TODO Omit cloning if recent
            sessionsCache[key] = evnt with { Processing = EventProcessing.Finish(evnt.Created) };
        }

        public bool TryGetLast(string? key, DateTimeOffset? when, [MaybeNullWhen(false)] out PresenceEvent evnt)
        {
            key ??= accountKey;
            if(!sessionsCache.TryGetValue(key, out evnt))
            {
                // Not present
                return false;
            }
            if(IsStale(evnt, when))
            {
                // Invalidate
                sessionsCache.TryRemove(new(key, evnt));
                return false;
            }
            return true;
        }

        public bool TryGetLast(DateTimeOffset? when, [MaybeNullWhen(false)] out ICollection<PresenceEvent> events)
        {
            if(when - lastRequested > Configuration.PresenceCacheStaleDelay)
            {
                // Needs a new probe, ignore any results
                events = null;
                return false;
            }

            foreach(var pair in sessionsCache)
            {
                if(IsStale(pair.Value, when))
                {
                    // Remove stale pair
                    if(sessionsCache.TryRemove(pair))
                    {
                        // TODO Inform about sessions going unavailable
                    }
                }
            }
            events = sessionsCache.Values;
            return true;
        }

        private bool IsStale(PresenceEvent evnt, DateTimeOffset? when)
        {
            if(lastRequested is not { } last || when == null)
            {
                // No probe sent or necessary
                return false;
            }
            if(evnt.Published >= last)
            {
                // Updated after last probe
                return false;
            }
            // Still time after last probe
            return when < last + Configuration.PresenceCacheInvalidateAfterProbeDelay;
        }
    }
}
