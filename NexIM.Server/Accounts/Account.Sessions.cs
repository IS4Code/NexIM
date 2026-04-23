using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace NexIM.Server.Accounts;

partial class Account
{
    AccountSessions sessions = AccountSessions.Create();

    public void AddSession(ClientSession session)
    {
        sessions = sessions.Add(session);
    }

    public void AddOrUpdateSession(ClientSession session)
    {
        sessions = sessions.AddOrUpdate(session);
    }

    public void RemoveSession(ClientSession session)
    {
        sessions = sessions.Remove(session);
    }

    public ClientSession? GetSession(string resource)
    {
        if(!sessions.ByIdentifier.TryGetValue(resource, out var session))
        {
            return null;
        }
        // Found
        return session;
    }

    public IEnumerable<ClientSession> GetSessions(bool checkPriority)
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

    readonly record struct AccountSessions(
        ImmutableSortedDictionary<sbyte, ImmutableHashSet<ClientSession>> ByPriority,
        ImmutableDictionary<string, ClientSession> ByIdentifier
    )
    {
        static readonly ImmutableSortedDictionary<sbyte, ImmutableHashSet<ClientSession>> emptyPriorities = ImmutableSortedDictionary.Create<sbyte, ImmutableHashSet<ClientSession>>(new DescendingPriority());
        static readonly ImmutableHashSet<ClientSession> emptySessions = ImmutableHashSet<ClientSession>.Empty;
        static readonly ImmutableDictionary<string, ClientSession> emptyIdentifiers = ImmutableDictionary.Create<string, ClientSession>(StringComparer.Ordinal);

        public bool IsEmpty => ByPriority.Count + ByIdentifier.Count == 0;

        public static AccountSessions Create()
        {
            return new AccountSessions(emptyPriorities, emptyIdentifiers);
        }

        public static AccountSessions Create(ClientSession session)
        {
            return Create().Add(session);
        }

        public AccountSessions Add(ClientSession session)
        {
            if(!ByPriority.TryGetValue(session.Priority, out var sessions))
            {
                // No sessions with this priority
                sessions = emptySessions;
            }
            return new(
                ByPriority.SetItem(session.Priority, sessions.Add(session)),
                ByIdentifier.Add(session.Resource, session)
            );
        }

        public AccountSessions Remove(ClientSession session)
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

            var identifier = session.Resource;
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

        public AccountSessions AddOrUpdate(ClientSession session)
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

            var identifier = session.Resource;
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

        private bool FindSessionPriority(ClientSession session, ref sbyte priority, [MaybeNullWhen(false)] out ImmutableHashSet<ClientSession> sessions)
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

        private bool FindSessionIdentifier(ClientSession session, [MaybeNullWhen(false)] out string identifier)
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
