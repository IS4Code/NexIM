using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NexIM.Primitives;

/// <summary>
/// Provides access to a value that may be located in a remote location.
/// </summary>
/// <typeparam name="TObject">
/// The type of the stored value.
/// </typeparam>
public readonly struct Remote<TObject> : IEquatable<Remote<TObject>> where TObject : notnull
{
    readonly object? _data;

    TObject? dataAsImmediate => IsEmpty ? default : (TObject)_data;

    [MemberNotNullWhen(false, nameof(_data), nameof(dataAsImmediate))]
    public bool IsEmpty => _data switch {
        IRemoteProvider provider => !provider.References<TObject>(),
        null => true,
        _ => false
    };

    /// <summary>
    /// Creates a <see cref="Remote{T}"/> instance from an immediate value.
    /// </summary>
    public Remote(TObject? immediateValue)
    {
        _data = immediateValue;
    }

    /// <summary>
    /// Creates a <see cref="Remote{T}"/> instance from an <see cref="IRemoteProvider"/> instance.
    /// </summary>
    public Remote(IRemoteProvider provider)
    {
        _data = provider;
    }

    /// <summary>
    /// Retrieves a property of the remote value.
    /// </summary>
    public ValueTask<TResult> Get<TResult>(Expression<Func<TObject, TResult>> retrieveExpression, Func<ValueTask<TResult>> defaultFactory, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(_data is IRemoteProvider dataProvider)
        {
            return dataProvider.Evaluate(retrieveExpression, defaultFactory, cancellationToken);
        }
        if(IsEmpty)
        {
            // No value provided, use fallback
            return defaultFactory();
        }
        return new(ExpressionCache<TObject, TResult>.Compile(retrieveExpression)(dataAsImmediate));
    }

    public Remote<TOther>? TryCast<TOther>() where TOther : notnull
    {
        switch(_data)
        {
            case IRemoteProvider dataProvider:
                if(dataProvider.References<TOther>())
                {
                    return new(dataProvider);
                }
                return null;
            case TOther value:
                return new(value);
            default:
                return null;
        }
    }

    public bool TryGetValue([MaybeNullWhen(false)] out TObject result)
    {
        switch(_data)
        {
            case IRemoteProvider:
                goto default;
            case TObject obj:
                result = obj;
                return true;
            default:
                result = default;
                return false;
        }
    }

    public bool Equals(Remote<TObject> other)
    {
        if(_data == other._data)
        {
            return true;
        }
        if(_data is IRemoteProvider dataProvider)
        {
            if(other._data is IRemoteProvider otherProvider)
            {
                return dataProvider.Equals(otherProvider);
            }
            return dataProvider.References(other.dataAsImmediate);
        }
        else
        {
            if(other._data is IRemoteProvider otherProvider)
            {
                return otherProvider.References(dataAsImmediate);
            }
        }
        return EqualityComparer<TObject?>.Default.Equals(dataAsImmediate, other.dataAsImmediate);
    }

    public override bool Equals(object obj)
    {
        return obj is Remote<TObject> other && Equals(other);
    }

    public static bool operator ==(Remote<TObject> a, Remote<TObject> b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(Remote<TObject> a, Remote<TObject> b)
    {
        return !a.Equals(b);
    }

    public override int GetHashCode()
    {
        if(_data is IRemoteProvider dataProvider)
        {
            return dataProvider.GetHashCode();
        }
        return EqualityComparer<TObject?>.Default.GetHashCode(dataAsImmediate);
    }
}

public interface IRemoteProvider : IEquatable<IRemoteProvider>
{
    ValueTask<TResult> Evaluate<TObject, TResult>(Expression<Func<TObject, TResult>> retrieveExpression, Func<ValueTask<TResult>> defaultFactory, CancellationToken cancellationToken) where TObject : notnull;
    bool References<TObject>() where TObject : notnull;
    bool References<TObject>(TObject? other) where TObject : notnull;
}

public abstract class RemoteProvider<TObject> : IRemoteProvider, RemoteProvider<TObject>.IReferenceRemoteProvider<TObject> where TObject : notnull
{
    readonly SemaphoreSlim semaphore = new(1, 1);

    Task<TObject?>? task;

    protected virtual ValueTask<TResult>? TryGetImmediate<TResult>(LambdaExpression retrieveExpression, CancellationToken cancellationToken)
    {
        if(this is IResultRemoteProvider<TResult> provider)
        {
            return provider.TryGetImmediate(retrieveExpression, cancellationToken);
        }
        return null;
    }

    protected abstract ValueTask<TObject?> Load(CancellationToken cancellationToken);

    public ValueTask<TResult> Evaluate<TActual, TResult>(Expression<Func<TActual, TResult>> retrieveExpression, Func<ValueTask<TResult>> defaultFactory, CancellationToken cancellationToken) where TActual : notnull
    {
        return TryGetImmediate<TResult>(retrieveExpression, cancellationToken) ?? Inner();

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
                TObject? instance;
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

                if(instance is not TActual actual)
                {
                    // No value
                    return await defaultFactory();
                }

                var func = ExpressionCache<TActual, TResult>.Compile(retrieveExpression);
                return func(actual);
            }
        }
    }

    public abstract bool Equals(IRemoteProvider other);
    public abstract bool References(TObject? other);

    public virtual bool References<TActual>() where TActual : notnull
    {
        return this is IReferenceRemoteProvider<TActual>;
    }

    public virtual bool References<TActual>(TActual? other) where TActual : notnull
    {
        if(this is IReferenceRemoteProvider<TActual> provider)
        {
            return provider.References(other);
        }
        return false;
    }

    public sealed override bool Equals(object obj)
    {
        return obj is IRemoteProvider other && Equals(other);
    }

    public abstract override int GetHashCode();

    public interface IResultRemoteProvider<TResult>
    {
        ValueTask<TResult>? TryGetImmediate(LambdaExpression retrieveExpression, CancellationToken cancellationToken);
    }

    public interface IReferenceRemoteProvider<in TActual> where TActual : notnull
    {
        bool References(TActual? other);
    }
}

public abstract class DerivedRemoteProvider<TBase, TDerived> : RemoteProvider<TDerived>, RemoteProvider<TDerived>.IReferenceRemoteProvider<TBase> where TDerived : notnull, TBase where TBase : notnull
{
    bool IReferenceRemoteProvider<TBase>.References(TBase? other)
    {
        return other is TDerived derived && References(derived);
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
