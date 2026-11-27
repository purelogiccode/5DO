using System.Diagnostics;
using System.Threading;

namespace FiveDo.Lobby;

/// <summary>
/// A small thread-safe fixed one-second budget used by the UDP endpoints. It is
/// deliberately simple: overshoot is bounded and the window resets on the next packet.
/// </summary>
internal sealed class WindowRateLimiter
{
    private readonly long _limit;
    private long _windowStarted = Stopwatch.GetTimestamp();
    private long _used;

    public WindowRateLimiter(long limitPerSecond)
    {
        _limit = limitPerSecond;
    }

    public bool TryAcquire(long amount)
    {
        if (_limit <= 0)
        {
            return true;
        }

        long now = Stopwatch.GetTimestamp();
        long started = Interlocked.Read(ref _windowStarted);
        if (now - started >= Stopwatch.Frequency
            && Interlocked.CompareExchange(ref _windowStarted, now, started) == started)
        {
            Interlocked.Exchange(ref _used, 0);
        }

        long used = Interlocked.Add(ref _used, amount);
        return used <= _limit;
    }
}
