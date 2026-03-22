using System;
using System.Collections.Immutable;
using System.Linq;

namespace Unicord.Server.Events;

/// <summary>
/// Represents an arbitrary payload of an event.
/// </summary>
public abstract record EventData
{
    /// <summary>
    /// Stores protocol-specific extensions.
    /// </summary>
    public ImmutableDictionary<ExtensionType, object> Extensions { get; set; } = ImmutableDictionary<ExtensionType, object>.Empty;

    public virtual bool Equals(EventData? other)
    {
        return
            other != null &&
            Extensions.Count == other.Extensions.Count &&
            Extensions.All(other.Extensions.Contains);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        foreach(var pair in Extensions)
        {
            hashCode.Add(pair.Key);
            hashCode.Add(pair.Value);
        }
        return hashCode.ToHashCode();
    }
}

public enum ExtensionType
{
    Xmpp
}
