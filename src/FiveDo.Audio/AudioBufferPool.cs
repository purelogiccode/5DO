using System;

namespace FiveDo.Audio;

/// <summary>
/// Tracks which OpenAL buffers are free to fill and queue.
/// </summary>
/// <remarks>
/// OpenAL unqueues the oldest buffer first, so the number of buffers still queued does not
/// identify a free buffer. Indexing the buffer array by the queued count rewrites buffers
/// that are still attached to the source, corrupting audio that is about to play and
/// producing clicks. This pool hands out only buffers the source no longer owns.
/// </remarks>
internal sealed class AudioBufferPool
{
    private readonly uint[] _free;
    private int _count;

    /// <summary>Initializes a pool that owns every supplied buffer.</summary>
    /// <param name="buffers">OpenAL buffer names, all initially free.</param>
    internal AudioBufferPool(ReadOnlySpan<uint> buffers)
    {
        _free = new uint[buffers.Length];
        buffers.CopyTo(_free);
        _count = buffers.Length;
    }

    /// <summary>Gets the number of buffers available to rent.</summary>
    internal int Count => _count;

    /// <summary>Tries to rent a free buffer.</summary>
    /// <param name="buffer">Receives the buffer name, or zero when none is free.</param>
    /// <returns><see langword="true"/> when a buffer was rented.</returns>
    internal bool TryRent(out uint buffer)
    {
        if (_count == 0)
        {
            buffer = 0;
            return false;
        }

        buffer = _free[--_count];
        return true;
    }

    /// <summary>Returns a buffer after the source has released it.</summary>
    /// <param name="buffer">Buffer name returned by the source.</param>
    internal void Return(uint buffer)
    {
        if (_count >= _free.Length)
        {
            throw new InvalidOperationException("The buffer pool received more buffers than it owns.");
        }

        _free[_count++] = buffer;
    }

    /// <summary>Returns several buffers after the source has released them.</summary>
    /// <param name="buffers">Buffer names returned by the source.</param>
    internal void Return(ReadOnlySpan<uint> buffers)
    {
        foreach (var buffer in buffers)
        {
            Return(buffer);
        }
    }

    /// <summary>Marks every buffer free, for example after the source was stopped and drained.</summary>
    /// <param name="buffers">Every buffer owned by the pool.</param>
    internal void Reset(ReadOnlySpan<uint> buffers)
    {
        _count = 0;
        Return(buffers);
    }
}
