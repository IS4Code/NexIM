using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Unicord.Xmpp.Protocol.Grammar;

namespace Unicord.Xmpp.Server;

public class XmppNameTable : Vocabulary,
    IEqualityComparer<XmppNameTable.PreHashed<XmppNameTable.WeakStringReference>>,
    IAlternateEqualityComparer<XmppNameTable.PreHashed<ReadOnlyMemory<char>>, XmppNameTable.PreHashed<XmppNameTable.WeakStringReference>>
{
    readonly ConcurrentDictionary<PreHashed<WeakStringReference>, ValueTuple> data;
    readonly ConcurrentDictionary<PreHashed<WeakStringReference>, ValueTuple>.AlternateLookup<PreHashed<ReadOnlyMemory<char>>> lookup;

    readonly ConcurrentBag<PreHashed<WeakStringReference>> deadRecords = new();

    // The number of persistent strings in the table
    int minimumCount;

    public int WeakReferencesCount => data.Count - minimumCount;

    public XmppNameTable()
    {
        data = new(this);
        lookup = data.GetAlternateLookup<PreHashed<ReadOnlyMemory<char>>>();

        Initialize();
    }

    protected override void Initialize()
    {
        if(data != null)
        {
            // Called from the base constructor when data is not yet initialized
            base.Initialize();
        }
    }

    [ThreadStatic]
    static string? createResult, findResult;

    public override string Add(ReadOnlyMemory<char> memory)
    {
        try
        {
            var record = CreateRecord(memory);

            while(true)
            {
                // Both variables might be set as a result of another thread managing
                // to sneak in the pair in the middle of the record's construction.
                createResult = null;
                findResult = null;
                var added = lookup.TryAdd(record, default);
                if((added ? createResult : findResult) is { } result)
                {
                    // Instance located
                    createResult = null;
                    findResult = null;

                    if(added && Object.ReferenceEquals(String.IsInterned(result), result))
                    {
                        // Added as an interned string which will never be removed
                        Interlocked.Increment(ref minimumCount);
                    }

                    return result;
                }

                // This path shouldn't be taken as this operation must result
                // in a call to either Create or Equals.

                if((result = Get(record)) is not null)
                {
                    return result;
                }
            }
        }
        finally
        {
            RemovePendingReferences();
        }
    }

    public override string? Get(ReadOnlyMemory<char> memory)
    {
        return Get(CreateRecord(memory));
    }

    private string? Get(PreHashed<ReadOnlyMemory<char>> record)
    {
        findResult = null;
        if(!lookup.TryGetValue(record, out var key, out _))
        {
            // Not present
            return null;
        }

        if(findResult is { } result)
        {
            findResult = null;
            return result;
        }

        // This path shouldn't be taken as checking for equality
        // must result in a call to Equals.

        if(!key.Value.TryGetString(out result))
        {
            // Dead record
            data.TryRemove(new(key, default));
            return null;
        }

        return result;
    }

    public void RemoveDeadReferences()
    {
        try
        {
            foreach(var pair in data)
            {
                if(!pair.Key.Value.TryGetString(out _))
                {
                    deadRecords.Add(pair.Key);
                }
            }
        }
        finally
        {
            RemovePendingReferences();
        }
    }

    private void RemovePendingReferences()
    {
        // Remove entries known to be dead
        while(deadRecords.TryTake(out var record))
        {
            data.TryRemove(new(record, default));
        }
    }

    private PreHashed<ReadOnlyMemory<char>> CreateRecord(ReadOnlyMemory<char> memory)
    {
        return new(GetHashCode(memory.Span), memory);
    }

    protected virtual int GetHashCode(ReadOnlySpan<char> span)
    {
        return String.GetHashCode(span, StringComparison.Ordinal);
    }

    protected virtual bool Equals(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        return a.Equals(b, StringComparison.Ordinal);
    }

    protected virtual string Create(ReadOnlyMemory<char> memory)
    {
        return memory.ToString();
    }

    bool IEqualityComparer<PreHashed<WeakStringReference>>.Equals(PreHashed<WeakStringReference> x, PreHashed<WeakStringReference> y)
    {
        // Primary lookup must identify precise records
        return x.Value.Instance == y.Value.Instance;
    }

    int IEqualityComparer<PreHashed<WeakStringReference>>.GetHashCode(PreHashed<WeakStringReference> obj)
    {
        return obj.HashCode;
    }

    bool IAlternateEqualityComparer<PreHashed<ReadOnlyMemory<char>>, PreHashed<WeakStringReference>>.Equals(PreHashed<ReadOnlyMemory<char>> alternate, PreHashed<WeakStringReference> other)
    {
        if(!other.Value.TryGetString(out var str))
        {
            // Dead record found by the same hash as an existing one - schedule for cleanup
            // (not to mess with the implementation's reentrancy)
            deadRecords.Add(other);
            return false;
        }
        if(!Equals(alternate.Value.Span, str.AsSpan()))
        {
            // Hash collision
            return false;
        }
        // Expose the located string
        findResult = str;
        return true;
    }

    int IAlternateEqualityComparer<PreHashed<ReadOnlyMemory<char>>, PreHashed<WeakStringReference>>.GetHashCode(PreHashed<ReadOnlyMemory<char>> alternate)
    {
        return alternate.HashCode;
    }

    PreHashed<WeakStringReference> IAlternateEqualityComparer<PreHashed<ReadOnlyMemory<char>>, PreHashed<WeakStringReference>>.Create(PreHashed<ReadOnlyMemory<char>> alternate)
    {
        var result = Create(alternate.Value);
        // Expose the last created instance
        createResult = result;
        return new(alternate.HashCode, new(result));
    }

    /// <summary>
    /// Stores a custom pre-computed hash alongside value for faster comparison.
    /// </summary>
    readonly record struct PreHashed<T>(int HashCode, T Value) where T : struct
    {
        public override int GetHashCode()
        {
            return HashCode;
        }

        public override string? ToString()
        {
            return Value.ToString();
        }
    }

    /// <summary>
    /// Stores a weak reference to a string.
    /// </summary>
    readonly record struct WeakStringReference(object Instance)
    {
        public WeakStringReference(string str) : this(String.IsInterned(str) is { } interned ? interned : new WeakReference<string>(str))
        {
            // Interned strings don't need a weak reference
        }

        public bool TryGetString([MaybeNullWhen(false)] out string str)
        {
            switch(Instance)
            {
                case string interned:
                    str = interned;
                    return true;
                case WeakReference<string> weakRef:
                    return weakRef.TryGetTarget(out str);
                default:
                    str = null;
                    return false;
            }
        }

        public override string? ToString()
        {
            return TryGetString(out var str) ? str : "<dead>";
        }
    }
}
