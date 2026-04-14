using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unicord.Server.Accounts;
using Unicord.Server.Events;

namespace Unicord.Server;

internal static class DeliveryExtensions
{
    public static TEvent WithTo<TEvent>(this TEvent evnt, Identifiers to) where TEvent : Event
    {
        var origin = evnt.Origin;
        if(origin.To == to)
        {
            return evnt;
        }
        origin.To = to;
        return evnt with { Origin = origin };
    }

    public static TEvent WithFrom<TEvent>(this TEvent evnt, Identifier from) where TEvent : Event
    {
        var origin = evnt.Origin;
        if(origin.From == from)
        {
            return evnt;
        }
        origin.From = from;
        return evnt with { Origin = origin };
    }

    public static TEvent WithOrigin<TEvent>(this TEvent evnt, EventOrigin origin) where TEvent : Event
    {
        if(evnt.Origin == origin)
        {
            return evnt;
        }
        return evnt with { Origin = origin };
    }

    public static ValueTask<StatusReports> Route<TKey, TArgs>(this Identifiers to, Func<Identifier, TKey> router, Func<TKey, Identifiers, TArgs, ValueTask<StatusReports>> target, TArgs args)
    {
        if(to.TryGetSingle(out var single))
        {
            // Single element
            return target(router(single), to, args);
        }

        // Store result tasks
        var results = new List<ValueTask<StatusReports>>();
        foreach(var partition in to.OrderedPartitionBy(router))
        {
            try
            {
                results.Add(target(partition.Key, partition.Value, args));
            }
            catch(Exception e)
            {
                results.Add(ValueTask.FromException<StatusReports>(e));
            }
        }

        return Combine(results);
    }

    public static ValueTask<StatusReports> Route<TKey, TArgs>(this Identifiers to, Func<Identifier, TKey> router, Func<TKey, Identifiers, TArgs, IEnumerable<ValueTask<StatusReports>>> target, TArgs args)
    {
        if(to.TryGetSingle(out var single))
        {
            // Single element
            return target(router(single), to, args).Combine();
        }

        // Store result tasks
        var results = new List<ValueTask<StatusReports>>();
        foreach(var partition in to.OrderedPartitionBy(router))
        {
            try
            {
                results.AddRange(target(partition.Key, partition.Value, args));
            }
            catch(Exception e)
            {
                results.Add(ValueTask.FromException<StatusReports>(e));
            }
        }

        return Combine(results);
    }

    public static async ValueTask<StatusReports> Combine(this IEnumerable<ValueTask<StatusReports>> results)
    {
        List<Exception>? exceptions = null;

        var builder = StatusReports.Builder.Empty;

        foreach(var task in results)
        {
            try
            {
                builder.Add(await task);
            }
            catch(Exception e)
            {
                (exceptions ??= new()).Add(e);
            }
        }

        if(exceptions != null)
        {
            // Expose all exceptions
            await ValueTask.FromException<StatusReports>(new AggregateException(exceptions));
        }

        return builder.TryToSet() ?? default;
    }
}
