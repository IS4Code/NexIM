using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Unicord.Server.Events;

/// <summary>
/// Represents an arbitrary payload of an event.
/// </summary>
public abstract record EventData
{
    /// <summary>
    /// Stores protocol-specific extensions.
    /// </summary>
    public EventExtensions Extensions { get; set; }
}

[StructLayout(LayoutKind.Auto)]
public readonly struct EventExtensions : IReadOnlyCollection<object>, IEquatable<EventExtensions>
{
    static readonly ImmutableHashSet<object> empty = ImmutableHashSet<object>.Empty;

    readonly ImmutableHashSet<object>? _data;
    ImmutableHashSet<object> data => _data ?? empty;

    public int Count => data.Count;
    public bool IsEmpty => Count == 0;

    private EventExtensions(ImmutableHashSet<object> data)
    {
        _data = data;
    }

    public EventExtensions(object? payload)
    {
        if(payload != null)
        {
            _data = ImmutableHashSet.Create(payload);
        }
    }

    public bool Equals(EventExtensions other)
    {
        return data.SetEquals(other.data);
    }

    public override bool Equals(object? obj)
    {
        return obj is EventExtensions other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        foreach(var obj in data)
        {
            hashCode.Add(obj);
        }
        return hashCode.ToHashCode();
    }

    public static bool operator ==(EventExtensions a, EventExtensions b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(EventExtensions a, EventExtensions b)
    {
        return !a.Equals(b);
    }

    public override string ToString()
    {
        return String.Join(", ", data);
    }

    public Enumerator GetEnumerator()
    {
        return new(data.GetEnumerator());
    }

    IEnumerator<object> IEnumerable<object>.GetEnumerator()
    {
        return ((IEnumerable<object>)data).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator : IEnumerator<object>
    {
        ImmutableHashSet<object>.Enumerator inner;

        internal Enumerator(ImmutableHashSet<object>.Enumerator inner)
        {
            this.inner = inner;
        }

        public object Current => inner.Current;

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            return inner.MoveNext();
        }

        public void Reset()
        {
            inner.Reset();
        }

        public void Dispose()
        {
            inner.Dispose();
        }
    }
}
