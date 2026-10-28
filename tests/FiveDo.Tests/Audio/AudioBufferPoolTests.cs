using System.Collections.Generic;
using System.Linq;
using FiveDo.Audio;

namespace FiveDo.Tests.Audio;

public sealed class AudioBufferPoolTests
{
    [Fact]
    public void TryRent_ReturnsEveryOwnedBufferExactlyOnce()
    {
        var pool = new AudioBufferPool([10u, 20u, 30u]);
        var rented = new List<uint>();
        while (pool.TryRent(out var buffer))
        {
            rented.Add(buffer);
        }

        Assert.Equal(3, rented.Count);
        Assert.Equal(3, rented.Distinct().Count());
        Assert.Equal(0, pool.Count);
        Assert.False(pool.TryRent(out _));
    }

    [Fact]
    public void RefillAfterUnqueue_NeverRentsABufferStillQueued()
    {
        // Regression: the fill loop used the queued count as a buffer index. OpenAL
        // releases the oldest buffers first, so that rewrote buffers still attached to
        // the source, which corrupted audio that was about to play and clicked.
        uint[] names = [1, 2, 3, 4];
        var pool = new AudioBufferPool(names);
        var queued = new List<uint>();
        for (var i = 0; i < names.Length; i++)
        {
            Assert.True(pool.TryRent(out var buffer));
            queued.Add(buffer);
        }

        Assert.False(pool.TryRent(out _));

        // The device finished the two oldest buffers and OpenAL released them.
        var released = queued.GetRange(0, 2);
        queued.RemoveRange(0, 2);
        pool.Return(released.ToArray());

        while (queued.Count < names.Length)
        {
            Assert.True(pool.TryRent(out var buffer));
            Assert.DoesNotContain(buffer, queued);
            queued.Add(buffer);
        }

        Assert.Equal(names.Length, queued.Count);
        Assert.Equal(names.Length, queued.Distinct().Count());
    }

    [Fact]
    public void Reset_MakesEveryBufferAvailableAgain()
    {
        var pool = new AudioBufferPool([1u, 2u, 3u]);
        Assert.True(pool.TryRent(out var first));
        Assert.True(pool.TryRent(out var second));
        Assert.Equal(1, pool.Count);

        pool.Reset([1u, 2u, 3u]);

        Assert.Equal(3, pool.Count);
        var rented = new List<uint>();
        while (pool.TryRent(out var buffer))
        {
            rented.Add(buffer);
        }

        Assert.Equal(3, rented.Distinct().Count());
        Assert.Contains(first, rented);
        Assert.Contains(second, rented);
    }
}
