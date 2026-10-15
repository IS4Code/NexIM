using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NexIM.Tools;

public abstract class StringTransformationCache<TResult>
{
    // Transformed interned strings are never collected
    readonly ConcurrentDictionary<string, TResult> internedCache = new(ReferenceEqualityComparer.Instance);
    readonly ConditionalWeakTable<string, object?> normalCache = new();

    readonly Func<string, TResult> internedFactory;
    readonly ConditionalWeakTable<string, object?>.CreateValueCallback normalFactory;

    public StringTransformationCache()
    {
        internedFactory = str => Transform(str);
        normalFactory = str => Transform(str);
    }

    protected abstract TResult Transform(string input);

    public TResult Get(string value)
    {
        if(Object.ReferenceEquals(value, String.IsInterned(value)))
        {
            return internedCache.GetOrAdd(value, internedFactory);
        }
        return (TResult)normalCache.GetValue(value, normalFactory)!;
    }

    sealed class ReferenceEqualityComparer : IEqualityComparer<object?>
    {
        public static ReferenceEqualityComparer Instance = new();

        private ReferenceEqualityComparer()
        {

        }

        public new bool Equals(object? x, object? y)
        {
            return Object.ReferenceEquals(x, y);
        }

        public int GetHashCode(object? obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}

public abstract class StringTransformationCache<TResult, TFormattable> : StringTransformationCache<TResult>
{
    protected abstract TResult Concat(ReadOnlySpan<TResult> results);

    protected abstract TResult Transform<TValue>(TValue value, string format) where TValue : TFormattable, IFormattable;

    public TResult Get([InterpolatedStringHandlerArgument("")] InterpolatedStringHandler handler)
    {
        return handler.ToResult();
    }

    [InterpolatedStringHandler, StructLayout(LayoutKind.Auto)]
#pragma warning disable CS9113
    public struct InterpolatedStringHandler(int literalLength, int formattedCount, StringTransformationCache<TResult, TFormattable> instance)
#pragma warning restore CS9113
    {
        readonly TResult[] results = new TResult[formattedCount * 2 + 1];
        int resultsCount;

        public void AppendLiteral(string s)
        {
            AppendFormatted(s);
        }

        public void AppendFormatted(string s)
        {
            results[resultsCount++] = instance.Get(s);
        }

        public void AppendFormatted(TResult result)
        {
            results[resultsCount++] = result;
        }

        public void AppendFormatted<T>(T t) where T : TFormattable, IFormattable
        {
            AppendFormatted(t, "");
        }

        public void AppendFormatted<T>(T t, string format) where T : TFormattable, IFormattable
        {
            results[resultsCount++] = instance.Transform(t, format);
        }

        internal readonly TResult ToResult()
        {
            return instance.Concat(results.AsSpan(0, resultsCount));
        }
    }
}
