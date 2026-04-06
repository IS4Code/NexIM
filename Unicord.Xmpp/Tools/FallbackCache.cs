using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Unicord.Xmpp.Tools;

internal class FallbackCache<TKey, TValue> where TKey : notnull where TValue : class
{
    static readonly TimeSpan expirationTimeout = TimeSpan.FromSeconds(2);

    public static readonly FallbackCache<TKey, TValue> Global = new();

    public void Set(TKey identifier, TValue value)
    {
        storage[identifier] = new(Task.FromResult(value));
    }

    readonly ConcurrentDictionary<TKey, WeakReference<Task<TValue>>> storage = new();
    readonly ConditionalWeakTable<Task<TValue>, TaskCompletionSource<TValue>> completions = new();
    readonly ConditionalWeakTable<TValue, Task<TValue>> aliveLink = new();

    // Must be set from all modification methods
    [ThreadStatic]
    static Task<TValue>? resultTask;

    readonly Func<TKey, Lazy<Task<TValue?>>, WeakReference<Task<TValue>>> addTaskFactory;
    readonly Func<TKey, WeakReference<Task<TValue>>, Lazy<Task<TValue?>>, WeakReference<Task<TValue>>> updateTaskFactory;

    public FallbackCache()
    {
        addTaskFactory = (_, fallback) => {
            // No previous value - start the fallback task
            var fallbackTask = fallback.Value;

            if(fallbackTask.IsCompletedSuccessfully && fallbackTask.Result is { } result)
            {
                // Already finished, just use the result (store in a new task to prevent circular references)
                resultTask = Task.FromResult(result);
                return new(resultTask);
            }

            // Start the TCS
            var tcs = new TaskCompletionSource<TValue>();
            var task = tcs.Task;
            // Link back to the TCS
            completions.AddOrUpdate(task, tcs);

            fallbackTask.ContinueWith(t => {
                if(t.IsCompletedSuccessfully && t.Result is { } result)
                {
                    // Store value
                    tcs.TrySetResult(result);

                    // TCS is no longer needed
                    completions.Remove(task);

                    // Keep task alive as long as result is alive
                    aliveLink.AddOrUpdate(result, task);
                }
            });

            // Output the task
            resultTask = task;
            return new(task);
        };

        updateTaskFactory = (key, previous, fallback) => {
            if(!previous.TryGetTarget(out var task))
            {
                // Expired, have to ask again
                return addTaskFactory(key, fallback);
            }
            if(task.IsCompleted)
            {
                // Completed; output unchanged
                resultTask = task;
                return previous;
            }
            if(!completions.TryGetValue(task, out var tcs))
            {
                // Task is in progress but not linked to a completion
                if(task.IsCompleted)
                {
                    // Completed before these previous two checks
                    resultTask = task;
                    return previous;
                }

                // TCS is lost for some weird reason; recreate it
                tcs = new();
                var newTask = tcs.Task;
                // Link back to the TCS
                completions.AddOrUpdate(newTask, tcs);

                // Link the original task to it
                task.ContinueWith(t => {
                    tcs.TrySetFromTask(t);
                    completions.Remove(newTask);
                });

                // Use the new task
                task = newTask;
                previous = new(task);
            }

            // Wait for an ongoing authoritative response that sets the TCS
            task.WaitAsync(expirationTimeout).ContinueWith(t => {
                if(t.IsCompletedSuccessfully)
                {
                    // Completed within timeout
                    completions.Remove(task);
                    return;
                }

                // Timeout - start the fallback task
                var fallbackTask = fallback.Value;

                fallbackTask.ContinueWith(t => {
                    if(t.IsCompletedSuccessfully && t.Result is { } result)
                    {
                        // Store value
                        tcs.TrySetResult(result);

                        // TCS is no longer needed
                        completions.Remove(task);

                        // Keep task alive as long as result is alive
                        aliveLink.AddOrUpdate(result, task);
                    }
                });
            });

            resultTask = task;
            return previous;
        };
    }

    public Task<TValue> Get(TKey identifier, Func<Task<TValue?>> fallback)
    {
        var fallbackLazy = new Lazy<Task<TValue?>>(fallback, LazyThreadSafetyMode.None);
        resultTask = default;
        storage.AddOrUpdate(identifier, addTaskFactory, updateTaskFactory, fallbackLazy);
        var result = resultTask!;
        resultTask = null;
        return result;
    }
}
