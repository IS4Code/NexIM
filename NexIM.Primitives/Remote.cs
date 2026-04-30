using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NexIM.Primitives;

/// <summary>
/// Stores a reference to a value that may be accessible from a remote location.
/// </summary>
/// <typeparam name="T">
/// The type of the stored value.
/// </typeparam>
public readonly struct Remote<T> : IEquatable<Remote<T>> where T : notnull
{
    readonly object? _data;

    T? dataAsImmediate => IsEmpty ? default : (T)_data;

    [MemberNotNullWhen(false, nameof(_data), nameof(dataAsImmediate))]
    public bool IsEmpty => _data is null;

    /// <summary>
    /// Creates a <see cref="Remote{T}"/> instance from an immediate value.
    /// </summary>
    public Remote(T? immediateValue)
    {
        _data = immediateValue;
    }

    /// <summary>
    /// Creates a <see cref="Remote{T}"/> instance from an <see cref="IRemoteProvider{T}"/> instance.
    /// </summary>
    public Remote(IRemoteProvider<T> provider)
    {
        _data = provider;
    }

    /// <summary>
    /// Retrieves a property of the remote value.
    /// </summary>
    public ValueTask<TResult> Get<TResult>(Expression<Func<T, TResult>> retrieveExpression, Func<TResult> defaultFactory, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(_data is IRemoteProvider<T> dataProvider)
        {
            return dataProvider.Get(retrieveExpression, defaultFactory, cancellationToken);
        }
        if(IsEmpty)
        {
            // No value provided, use fallback
            return new(defaultFactory());
        }
        return new(ExpressionCache<T, TResult>.Compile(retrieveExpression)(dataAsImmediate));
    }

    public Remote<TOther>? TryCast<TOther>() where TOther : notnull
    {
        switch(_data)
        {
            case IRemoteProvider<TOther> dataProvider:
                return new(dataProvider);
            case TOther value:
                return new(value);
            default:
                return null;
        }
    }

    public bool Equals(Remote<T> other)
    {
        if(_data == other._data)
        {
            return true;
        }
        if(_data is IRemoteProvider<T> dataProvider)
        {
            return dataProvider.Equals(other._data);
        }
        if(other._data is IRemoteProvider<T> otherProvider)
        {
            return otherProvider.Equals(_data);
        }
        return EqualityComparer<T?>.Default.Equals(dataAsImmediate, other.dataAsImmediate);
    }

    public override bool Equals(object obj)
    {
        return obj is Remote<T> other && Equals(other);
    }

    public static bool operator ==(Remote<T> a, Remote<T> b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(Remote<T> a, Remote<T> b)
    {
        return !a.Equals(b);
    }

    public override int GetHashCode()
    {
        if(_data is IRemoteProvider<T> dataProvider)
        {
            return dataProvider.GetHashCode();
        }
        return EqualityComparer<T?>.Default.GetHashCode(dataAsImmediate);
    }
}

public interface IRemoteProvider<T> : IEquatable<IRemoteProvider<T>>, IEquatable<T?>
{
    ValueTask<TResult> Get<TResult>(Expression<Func<T, TResult>> retrieveExpression, Func<TResult> defaultFactory, CancellationToken cancellationToken);
}

public abstract class RemoteProvider<T> : IRemoteProvider<T>
{
    readonly SemaphoreSlim semaphore = new(1, 1);

    Task<T?>? task;

    protected virtual ValueTask<TResult>? TryGetImmediate<TResult>(Expression<Func<T, TResult>> retrieveExpression, CancellationToken cancellationToken)
    {
        if(this is IResultRemoteProvider<TResult> provider)
        {
            return provider.TryGetImmediate(retrieveExpression, cancellationToken);
        }
        return null;
    }

    protected abstract ValueTask<T?> Load(CancellationToken cancellationToken);

    public ValueTask<TResult> Get<TResult>(Expression<Func<T, TResult>> retrieveExpression, Func<TResult> defaultFactory, CancellationToken cancellationToken)
    {
        return TryGetImmediate(retrieveExpression, cancellationToken) ?? Inner();

        async ValueTask<TResult> Inner()
        {
            bool created = false;
            var task = this.task;
            if(task == null)
            {
                await semaphore.WaitAsync();
                try
                {
                    task = this.task;
                    if(task == null)
                    {
                        // Still null, start loading
                        this.task = task = Load(cancellationToken).AsTask();
                        created = true;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }

            while(true)
            {
                T? instance;
                try
                {
                    instance = await task;
                }
                catch(TaskCanceledException) when(!created && task.IsCanceled)
                {
                    // The previous task was cancelled, try again
                    await semaphore.WaitAsync();
                    try
                    {
                        var newTask = this.task;
                        if(newTask != task && newTask != null)
                        {
                            // Another request in the meantime, use that one
                            task = newTask;
                            continue;
                        }
                        // Still the same task
                        this.task = task = Load(cancellationToken).AsTask();
                        created = true;
                        continue;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }

                if(instance is null)
                {
                    // No value
                    return defaultFactory();
                }

                var func = ExpressionCache<T, TResult>.Compile(retrieveExpression);
                return func(instance);
            }
        }
    }

    public abstract bool Equals(IRemoteProvider<T> other);
    public abstract bool Equals(T? other);

    public sealed override bool Equals(object obj)
    {
        switch(obj)
        {
            case IRemoteProvider<T> provider:
                return Equals(provider);
            case T other:
                return Equals(other);
            default:
                return false;
        }
    }

    public abstract override int GetHashCode();

    public interface IResultRemoteProvider<TResult>
    {
        ValueTask<TResult>? TryGetImmediate(Expression<Func<T, TResult>> retrieveExpression, CancellationToken cancellationToken);
    }
}

sealed file class ExpressionCache<TObject, TResult>
{
    static readonly ConditionalWeakTable<Expression<Func<TObject, TResult>>, Func<TObject, TResult>> cache = new();
    static readonly ConditionalWeakTable<Expression<Func<TObject, TResult>>, Func<TObject, TResult>>.CreateValueCallback factory = expr => expr.Compile();

    public static Func<TObject, TResult> Compile(Expression<Func<TObject, TResult>> expression)
    {
        return cache.GetValue(expression, factory);
    }
}
