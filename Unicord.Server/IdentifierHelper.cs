using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Unicord.Server;

internal static class IdentifierHelper
{
    public static DateTimeOffset IdentifierTimeNow => GetPreciseDateTime();

    public static Guid CreateGuid() => CreateGuid(IdentifierTimeNow);

    public static Guid CreateGuid(DateTimeOffset timestamp)
    {
        // Precision lost when converted to milliseconds
        int subMs = (int)(timestamp.UtcTicks % TimeSpan.TicksPerMillisecond);

        // Set 10 bits of sub-millisecond timestamp per Section 6.2 (Method 3) of RFC 9562.
        Span<Guid> guid = stackalloc Guid[] { Guid.CreateVersion7(timestamp) };
        var asShort = MemoryMarshal.Cast<Guid, ushort>(guid);
        ref ushort c = ref asShort[3];

        // Needs to be rescaled to cover the bits
        subMs = subMs * 1024 / (int)TimeSpan.TicksPerMillisecond;

        // Fill bits after ver
        c = unchecked((ushort)(c & 0xF003 | (subMs << 2)));

        return guid[0];
    }

    // Same as right shift followed by left shift by 32
    static readonly int stopwatchBitsShift = 32 - Math.Clamp(
        // How many least significant bits can be trimmed to fit within TimeSpan precision
        (int)Math.Floor(Math.Log2(
            Stopwatch.Frequency / (double) TimeSpan.TicksPerSecond
        )), 0, 32
    );

    // The smallest power of two that is truncated by shifting the stopwatch time
    static readonly long stopwatchWraparoundInterval = 1L << (64 - stopwatchBitsShift);

    // Mask to select the upper bits that are lost for stopwatch
    static readonly long stopwatchLostBitsMask = ~(stopwatchWraparoundInterval - 1);

    /// <remarks>
    /// The lower 32 bits correspond to the lower 32 bits of <see cref="DateTime.Ticks"/>.
    /// The upper 32 bits correspond to the lower 32 bits of <see cref="Stopwatch.GetTimestamp"/>,
    /// shifted right by <see cref="GetTrimmedStopwatchPrecisionBits"/>.
    /// </remarks>
    static long lastMeasuredTimeBits;

    const long timeComponentMask = 0xFFFFFFFF;

    const long maxStopwatchAdjustment = 20 * TimeSpan.TicksPerMillisecond;

    /// <summary>
    /// Retrieves the precise time by utilizing both
    /// <see cref="DateTime.UtcNow"/> and <see cref="Stopwatch"/>.
    /// </summary>
    private static DateTimeOffset GetPreciseDateTime()
    {
        // Load previously measured time
        var previous = Interlocked.Read(ref lastMeasuredTimeBits);
        var now = DateTime.UtcNow;
        var timestamp = Stopwatch.GetTimestamp();

        long lowerTimeTicks = now.Ticks & timeComponentMask;
        if(lowerTimeTicks != (previous & timeComponentMask))
        {
            // Clock got updated; pack and store current time information
            var bits =
                lowerTimeTicks |
                unchecked((long)((ulong)timestamp << stopwatchBitsShift) & ~timeComponentMask);

            var original = Interlocked.CompareExchange(ref lastMeasuredTimeBits, bits, previous);
            if(original == previous)
            {
                // No thread intervened
                return now;
            }
            // Another thread got to run in the meantime, very likely with the same clock value
            if((original & timeComponentMask) != lowerTimeTicks)
            {
                // Actually not the same clock value, so the current one is still precise enough
                return now;
            }

            // The other thread's clock measurement is authoritative; the time must be adjusted against that
            previous = original;
        }

        long previousTimestamp = unchecked((long)((ulong)(previous & ~timeComponentMask) >> stopwatchBitsShift));

        // Restore upper bits using current timestamp
        var restoredTimestamp = previousTimestamp | (timestamp & stopwatchLostBitsMask);

        long elapsed;
        if(
            GetElapsed(0) ||
            GetElapsed(-stopwatchWraparoundInterval) ||
            GetElapsed(stopwatchWraparoundInterval)
        )
        {
            // Use the correction (scaled down to minimize monotonicity issues)
            return now.AddTicks(elapsed / 2);
        }

        // The clock is equal but stopwatch shows a big difference
        // This is an extreme edge case and we can't do anything with it anyway

        return now;

        bool GetElapsed(long adjustment)
        {
            elapsed = Stopwatch.GetElapsedTime(restoredTimestamp + adjustment, timestamp).Ticks;

            // Sanity check - can't drift too much
            return -maxStopwatchAdjustment <= elapsed && elapsed <= maxStopwatchAdjustment;
        }
    }
}
