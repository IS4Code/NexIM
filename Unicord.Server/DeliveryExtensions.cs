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

    public static ValueTask<StatusCode> Route<TKey, TArgs>(this Identifiers to, Func<Identifier, TKey> router, Func<TKey, Identifiers, TArgs, ValueTask<StatusCode>> target, TArgs args)
    {
        if(to.TryGetSingle(out var single))
        {
            // Single element
            return target(router(single), to, args);
        }

        // Store result tasks
        var results = new List<ValueTask<StatusCode>>();
        foreach(var partition in to.OrderedPartitionBy(router))
        {
            try
            {
                results.Add(target(partition.Key, partition.Value, args));
            }
            catch(Exception e)
            {
                results.Add(ValueTask.FromException<StatusCode>(e));
            }
        }

        return Combine(results);
    }

    public static ValueTask<StatusCode> Route<TKey, TArgs>(this Identifiers to, Func<Identifier, TKey> router, Func<TKey, Identifiers, TArgs, IEnumerable<ValueTask<StatusCode>>> target, TArgs args)
    {
        if(to.TryGetSingle(out var single))
        {
            // Single element
            return target(router(single), to, args).Combine();
        }

        // Store result tasks
        var results = new List<ValueTask<StatusCode>>();
        foreach(var partition in to.OrderedPartitionBy(router))
        {
            try
            {
                results.AddRange(target(partition.Key, partition.Value, args));
            }
            catch(Exception e)
            {
                results.Add(ValueTask.FromException<StatusCode>(e));
            }
        }

        return Combine(results);
    }

    public static async ValueTask<StatusCode> Combine(this IEnumerable<ValueTask<StatusCode>> results)
    {
        bool anySuccess = false;
        StatusCode errorCode = StatusCode.Success;
        List<Exception>? exceptions = null;
        foreach(var task in results)
        {
            try
            {
                var result = await task;
                if(result == StatusCode.Success)
                {
                    anySuccess = true;
                }
                else
                {
                    errorCode = result;
                }
            }
            catch(Exception e)
            {
                (exceptions ??= new()).Add(e);
            }
        }

        if(exceptions != null)
        {
            // Expose all exceptions
            await ValueTask.FromException<StatusCode>(new AggregateException(exceptions));
        }

        // Suppress errors if anything was successful
        return anySuccess ? StatusCode.Success : errorCode;
    }
}
