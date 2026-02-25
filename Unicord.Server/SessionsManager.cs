using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Unicord.Server;

public class SessionsManager
{
    readonly ConcurrentDictionary<AccountName, AccountSessions> activeSessions = new();

    readonly Func<AccountName, IClientSession, AccountSessions> addSessionsInitializer;
    readonly Func<AccountName, AccountSessions, IClientSession, AccountSessions> addSessionsUpdater;

    readonly Func<AccountName, IClientSession, AccountSessions> addOrUpdateSessionsInitializer;
    readonly Func<AccountName, AccountSessions, IClientSession, AccountSessions> addOrUpdateSessionsUpdater;

    readonly Func<AccountName, IClientSession, AccountSessions> removeSessionsInitializer;
    readonly Func<AccountName, AccountSessions, IClientSession, AccountSessions> removeSessionsUpdater;

    public SessionsManager()
    {
        addSessionsInitializer = (account, session) => {
            return AccountSessions.Create(session);
        };
        addSessionsUpdater = (account, existing, session) => {
            return existing.Add(session);
        };
        addOrUpdateSessionsInitializer = (account, session) => {
            return AccountSessions.Create(session);
        };
        addOrUpdateSessionsUpdater = (account, existing, session) => {
            return existing.AddOrUpdate(session);
        };
        removeSessionsInitializer = (account, _) => {
            return AccountSessions.Create();
        };
        removeSessionsUpdater = (account, existing, session) => {
            return existing.Remove(session);
        };
    }

    public void AddSession(AccountName account, IClientSession session)
    {
        activeSessions.AddOrUpdate(
            account, addSessionsInitializer, addSessionsUpdater, session
        );
    }

    public void AddOrUpdateSession(AccountName account, IClientSession session)
    {
        var result = activeSessions.AddOrUpdate(account, addOrUpdateSessionsInitializer, addOrUpdateSessionsUpdater, session);
        if(result.IsEmpty)
        {
            activeSessions.TryRemove(new KeyValuePair<AccountName, AccountSessions>(account, result));
        }
    }

    public void RemoveSession(AccountName account, IClientSession session)
    {
        var result = activeSessions.AddOrUpdate(account, removeSessionsInitializer, removeSessionsUpdater, session);
        if(result.IsEmpty)
        {
            activeSessions.TryRemove(new KeyValuePair<AccountName, AccountSessions>(account, result));
        }
    }

    public IEnumerable<IClientSession> GetSessions(AccountName account, string? identifier, bool checkPriority)
    {
        if(!activeSessions.TryGetValue(account, out var sessions))
        {
            // No online account
            return Array.Empty<IClientSession>();
        }
        if(identifier == null)
        {
            if(checkPriority)
            {
                // All sessions with non-zero descending priority
                var byPriority =
                    sessions.ByPriority
                    .TakeWhile(static pair => pair.Key >= 0)
                    .SelectMany(static pair => pair.Value);

                return byPriority;
            }
            else
            {
                // Order is irrelevant
                return sessions.ByIdentifier.Values;
            }
        }
        else if(sessions.ByIdentifier.TryGetValue(identifier, out var session))
        {
            // Found
            if(!checkPriority || session.Priority >= 0)
            {
                return new[] { session };
            }
        }
        return Array.Empty<IClientSession>();
    }

    readonly record struct AccountSessions(
        ImmutableSortedDictionary<sbyte, ImmutableHashSet<IClientSession>> ByPriority,
        ImmutableDictionary<string, IClientSession> ByIdentifier
    )
    {
        static readonly ImmutableSortedDictionary<sbyte, ImmutableHashSet<IClientSession>> emptyPriorities = ImmutableSortedDictionary.Create<sbyte, ImmutableHashSet<IClientSession>>(new DescendingPriority());
        static readonly ImmutableHashSet<IClientSession> emptySessions = ImmutableHashSet.Create<IClientSession>();
        static readonly ImmutableDictionary<string, IClientSession> emptyIdentifiers = ImmutableDictionary.Create<string, IClientSession>(StringComparer.Ordinal);

        public bool IsEmpty => ByPriority.Count + ByIdentifier.Count == 0;

        public static AccountSessions Create()
        {
            return new AccountSessions(emptyPriorities, emptyIdentifiers);
        }

        public static AccountSessions Create(IClientSession session)
        {
            return Create().Add(session);
        }

        public AccountSessions Add(IClientSession session)
        {
            if(!ByPriority.TryGetValue(session.Priority, out var sessions))
            {
                // No sessions with this priority
                sessions = emptySessions;
            }
            return new(
                ByPriority.SetItem(session.Priority, sessions.Add(session)),
                ByIdentifier.Add(session.Identifier, session)
            );
        }

        public AccountSessions Remove(IClientSession session)
        {
            var byPriority = ByPriority;
            var priority = session.Priority;
            if(byPriority.TryGetValue(priority, out var sessions) || FindSessionPriority(session, ref priority, out sessions))
            {
                sessions = sessions.Remove(session);
                if(sessions.Count == 0)
                {
                    // No other sessions with that priorty
                    byPriority = byPriority.Remove(session.Priority);
                }
                else
                {
                    byPriority = byPriority.SetItem(session.Priority, sessions);
                }
            }

            var identifier = session.Identifier;
            var byIdentifier = ByIdentifier.Remove(identifier);
            if(byIdentifier.Count == ByIdentifier.Count)
            {
                // Not removed
                if(FindSessionIdentifier(session, out identifier))
                {
                    byIdentifier = ByIdentifier.Remove(identifier);
                }
            }

            return new(
                byPriority,
                byIdentifier
            );
        }

        public AccountSessions AddOrUpdate(IClientSession session)
        {
            var byPriority = ByPriority;
            var priority = session.Priority;
            if(byPriority.TryGetValue(priority, out var sessions) && sessions.Contains(session))
            {
                // No need to update
            }
            else if(FindSessionPriority(session, ref priority, out sessions))
            {
                // Exists with another priority
                var builder = byPriority.ToBuilder();

                // Remove from old collection
                sessions = sessions.Remove(session);
                if(sessions.Count == 0)
                {
                    builder.Remove(priority);
                }
                else
                {
                    builder[priority] = sessions;
                }

                priority = session.Priority;
                if(!builder.TryGetValue(priority, out sessions))
                {
                    // No sessions with this priority yet
                    sessions = emptySessions;
                }
                // Add to new collection
                sessions = sessions.Add(session);
                builder[priority] = sessions.Add(session);

                byPriority = builder.ToImmutable();
            }
            else
            {
                // Adding anew
                if(!byPriority.TryGetValue(priority, out sessions))
                {
                    // No sessions with this priority yet
                    sessions = emptySessions;
                }
                // Add to new collection
                sessions = sessions.Add(session);
                byPriority = byPriority.SetItem(priority, sessions);
            }

            var identifier = session.Identifier;
            var byIdentifier = ByIdentifier;
            if(byIdentifier.TryGetValue(identifier, out var existingSession))
            {
                if(existingSession != session)
                {
                    throw new InvalidOperationException("A session with this identifier already exists.");
                }
                // No need to update
            }
            else if(FindSessionIdentifier(session, out var oldIdentifier))
            {
                // Exists with another identifier
                var builder = byIdentifier.ToBuilder();
                builder.Remove(oldIdentifier);
                builder[identifier] = session;
                byIdentifier = builder.ToImmutable();
            }
            else
            {
                // Adding anew
                byIdentifier = byIdentifier.SetItem(identifier, session);
            }

            return new(
                byPriority,
                byIdentifier
            );
        }

        private bool FindSessionPriority(IClientSession session, ref sbyte priority, [MaybeNullWhen(false)] out ImmutableHashSet<IClientSession> sessions)
        {
            foreach(var pair in ByPriority)
            {
                if(pair.Value.Contains(session))
                {
                    priority = pair.Key;
                    sessions = pair.Value;
                    return true;
                }
            }
            sessions = null;
            return false;
        }

        private bool FindSessionIdentifier(IClientSession session, [MaybeNullWhen(false)] out string identifier)
        {
            foreach(var pair in ByIdentifier)
            {
                if(pair.Value == session)
                {
                    identifier = pair.Key;
                    return true;
                }
            }
            identifier = null;
            return false;
        }

        sealed class DescendingPriority : IComparer<sbyte>
        {
            int IComparer<sbyte>.Compare(sbyte x, sbyte y)
            {
                return y.CompareTo(x);
            }
        }
    }
}
