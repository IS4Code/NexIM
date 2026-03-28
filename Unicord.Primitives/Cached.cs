using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Unicord.Primitives;

/// <summary>
/// Stores a reference to a value that may be in the process of caching.
/// </summary>
/// <typeparam name="T">
/// The type of the stored value.
/// </typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct Cached<T> : IEquatable<Cached<T>>
{
    static readonly Task<T> defaultTask = default(ValueTask<T>).AsTask();

    readonly Task<T>? _task;

    Task<T> ResultTask => _task switch {
        ProxiedTask proxy => proxy.Start(),
        null => defaultTask,
        var task => task
    };

    Task<T> ComparisonTask => _task switch {
        ProxiedTask proxy => proxy.RealTask,
        null => defaultTask,
        var task => task
    };

    public bool IsAvailable => ComparisonTask.Status == TaskStatus.RanToCompletion;

    private Cached(Task<T> task)
    {
        _task = task;
    }

    public Task<T> GetValueAsync() => ResultTask;

    public static Cached<T> FromTask(Task<T> task)
    {
        return new(task);
    }

    public static Cached<T> FromFactory(Func<Task<T>> factory)
    {
        return new(new ProxiedTask(factory));
    }

    public static Cached<T> FromValue(T value)
    {
        return new(Task.FromResult(value));
    }

    public bool Equals(Cached<T> other)
    {
        return TaskEqualityComparer.Equals(ComparisonTask, other.ComparisonTask);
    }

    public override bool Equals(object obj)
    {
        return obj is Cached<T> other && Equals(other);
    }

    public static bool operator ==(Cached<T> a, Cached<T> b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(Cached<T> a, Cached<T> b)
    {
        return !a.Equals(b);
    }

    public override int GetHashCode()
    {
        return TaskEqualityComparer.GetHashCode(ComparisonTask);
    }

    /// <summary>
    /// Provides value equality for tasks.
    /// </summary>
    /// <remarks>
    /// The comparer maintains the same results for tasks that were compared
    /// prior to completion, so even if they result in the same value,
    /// they will remain non-equal.
    /// </remarks>
    static class TaskEqualityComparer
    {
        static readonly object observedAsCompleted = String.Empty;
        static readonly ConditionalWeakTable<Task<T>, object?> observedTasks = new();

        // Most values will be null, since the sentinel object appears only when the status changes during creation
        static readonly ConditionalWeakTable<Task<T>, object?>.CreateValueCallback observedCallback = task => task.IsCompleted ? observedAsCompleted : null;

        public static bool Equals(Task<T> a, Task<T> b)
        {
            if(a == b)
            {
                // Same instance
                return true;
            }
            if(IsIncomparable(a) || IsIncomparable(b))
            {
                // Observed as in progress
                return false;
            }
            // Both are completed
            var status = a.Status;
            if(status != b.Status)
            {
                // Different outcome
                return false;
            }
            if(status != TaskStatus.RanToCompletion)
            {
                // Exception
                return a.Exception.Equals(b.Exception);
            }
            // Compare the results
            return EqualityComparer<T>.Default.Equals(a.Result, b.Result);
        }

        public static int GetHashCode(Task<T> obj)
        {
            if(IsIncomparable(obj))
            {
                // Observed as in progress
                return obj.GetHashCode();
            }
            // Completed
            if(obj.Status != TaskStatus.RanToCompletion)
            {
                // Exception
                return obj.Exception.GetHashCode();
            }
            // Hash its result
            return EqualityComparer<T>.Default.GetHashCode(obj.Result);
        }

        private static bool IsIncomparable(Task<T> obj)
        {
            // Checks if task is in progress or was observed before as such
            return
                obj.IsCompleted
                ? observedTasks.TryGetValue(obj, out var status) && status != observedAsCompleted
                : observedTasks.GetValue(obj, observedCallback) != observedAsCompleted;
        }
    }

    /// <summary>
    /// An object deriving from <see cref="Task{TResult}"/>
    /// that exists just to provide a marker.
    /// Only the <see cref="Start"/>
    /// and <see cref="RealTask"/> members may be used.
    /// </summary>
    sealed class ProxiedTask : Task<T>
    {
        readonly Task<Task<T>> task;
        public Task<T> RealTask { get; }

        public ProxiedTask(Func<Task<T>> function) : base(static () => throw new InvalidOperationException())
        {
            task = new(function);
            RealTask = task.Unwrap();
        }

        public new Task<T> Start()
        {
            if(task.Status == TaskStatus.Created)
            {
                // Start first
                try
                {
                    task.Start();
                }
                catch(InvalidOperationException)
                {
                    // Concurrently started
                }
            }
            return RealTask;
        }
    }
}
