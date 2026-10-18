using System;

namespace NexIM.Tools;

public static class SpanExtensions
{
    public static void Advance<T>(this ref ReadOnlySpan<T> span, int count)
    {
        span = span.Slice(count);
    }

    public static void Advance<T>(this ref Span<T> span, int count)
    {
        span = span.Slice(count);
    }

    public static T Read<T>(this ref ReadOnlySpan<T> span)
    {
        var first = span[0];
        span.Advance(1);
        return first;
    }

    public static T Read<T>(this ref Span<T> span)
    {
        var first = span[0];
        span.Advance(1);
        return first;
    }

    public static ReadOnlySpan<T> Read<T>(this ref ReadOnlySpan<T> span, int length)
    {
        var prefix = span.Slice(0, length);
        span.Advance(length);
        return prefix;
    }

    public static Span<T> Read<T>(this ref Span<T> span, int length)
    {
        var prefix = span.Slice(0, length);
        span.Advance(length);
        return prefix;
    }

    public static void Write<T>(this ref Span<T> span, T element)
    {
        span[0] = element;
        span.Advance(1);
    }

    public static void Write<T>(this ref Span<T> span, ReadOnlySpan<T> elements)
    {
        elements.CopyTo(span);
        span.Advance(elements.Length);
    }

    public static void Write<T>(this ref Span<T> span, ReadOnlySpan<T> elements, T suffix)
    {
        elements.CopyTo(span);
        var length = elements.Length;
        span[length] = suffix;
        span.Advance(length + 1);
    }

    public static bool TrySplitAfter<T>(this ref ReadOnlySpan<T> span, T element, out ReadOnlySpan<T> prefix)
    {
        var index = span.IndexOf(element);
        if(index == -1)
        {
            prefix = span;
            span = default;
            return false;
        }
        prefix = span.Slice(0, index);
        span = span.Slice(index + 1);
        return true;
    }

    public static bool TrySplitAfter<T>(this ref Span<T> span, T element, out Span<T> prefix)
    {
        var index = ((ReadOnlySpan<T>)span).IndexOf(element);
        if(index == -1)
        {
            prefix = span;
            span = default;
            return false;
        }
        prefix = span.Slice(0, index);
        span = span.Slice(index + 1);
        return true;
    }

    public static bool TrySplitBefore<T>(this ref ReadOnlySpan<T> span, T element, out ReadOnlySpan<T> suffix)
    {
        var index = span.IndexOf(element);
        if(index == -1)
        {
            suffix = default;
            return false;
        }
        suffix = span.Slice(index + 1);
        span = span.Slice(0, index);
        return true;
    }

    public static bool TrySplitBefore<T>(this ref Span<T> span, T element, out Span<T> suffix)
    {
        var index = ((ReadOnlySpan<T>)span).IndexOf(element);
        if(index == -1)
        {
            suffix = default;
            return false;
        }
        suffix = span.Slice(index + 1);
        span = span.Slice(0, index);
        return true;
    }

    public static bool TryMatchFirst<T>(this ref ReadOnlySpan<T> span, T firstElement) where T : IEquatable<T>
    {
        if(span.IsEmpty)
        {
            return false;
        }
        if(span[0].Equals(firstElement))
        {
            span.Advance(1);
            return true;
        }
        return false;
    }

    public static bool TryMatchFirst<T>(this ref Span<T> span, T firstElement) where T : IEquatable<T>
    {
        if(span.IsEmpty)
        {
            return false;
        }
        if(span[0].Equals(firstElement))
        {
            span.Advance(1);
            return true;
        }
        return false;
    }

    public static bool TryMatchFirst<T>(this ref ReadOnlySpan<T> span, ReadOnlySpan<T> firstElements)
    {
        if(span.IsEmpty)
        {
            return false;
        }
        if(firstElements.IndexOf(span[0]) != -1)
        {
            span.Advance(1);
            return true;
        }
        return false;
    }

    public static bool TryMatchFirst<T>(this ref Span<T> span, ReadOnlySpan<T> firstElements)
    {
        if(span.IsEmpty)
        {
            return false;
        }
        if(firstElements.IndexOf(span[0]) != -1)
        {
            span.Advance(1);
            return true;
        }
        return false;
    }

    public static bool TryMatchStart<T>(this ref ReadOnlySpan<T> span, ReadOnlySpan<T> prefix)
    {
        if(span.IsEmpty)
        {
            return false;
        }
        if(span.StartsWith(prefix))
        {
            span.Advance(prefix.Length);
            return true;
        }
        return false;
    }

    public static bool TryMatchStart<T>(this ref Span<T> span, ReadOnlySpan<T> prefix)
    {
        if(span.IsEmpty)
        {
            return false;
        }
        if(((ReadOnlySpan<T>)span).StartsWith(prefix))
        {
            span.Advance(prefix.Length);
            return true;
        }
        return false;
    }

    public static bool StripFirst<T>(this ref ReadOnlySpan<T> span, T element)
    {
        var index = span.IndexOfAnyExcept(element);
        if(index == -1)
        {
            span = default;
            return false;
        }
        span.Advance(index);
        return true;
    }

    public static bool StripFirst<T>(this ref Span<T> span, T element)
    {
        var index = ((ReadOnlySpan<T>)span).IndexOfAnyExcept(element);
        if(index == -1)
        {
            span = default;
            return false;
        }
        span.Advance(index);
        return true;
    }
}
