using System;
using System.Threading;

namespace ThreeDo.Audio;

/// <summary>
/// A lock-free single-producer/single-consumer ring buffer of interleaved stereo float samples.
/// </summary>
/// <remarks>
/// <para>
/// The emulation thread is the producer and the host audio callback is the consumer. Frame
/// counts are tracked instead of sample counts so a reader never observes a half-written
/// stereo pair.
/// </para>
/// <para>
/// Capacity is rounded up to a power of two so index wrapping uses a mask. The buffer never
/// allocates after construction.
/// </para>
/// </remarks>
public sealed class AudioRingBuffer
{
    private readonly float[] _samples;
    private readonly int _capacityFrames;
    private readonly int _frameMask;
    private uint _writeFrame;
    private uint _readFrame;

    /// <summary>Initializes a new instance of the <see cref="AudioRingBuffer"/> class.</summary>
    /// <param name="capacityFrames">Requested capacity in stereo frames. Rounded up to a power of two.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the capacity is not positive.</exception>
    public AudioRingBuffer(int capacityFrames)
    {
        if (capacityFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityFrames), capacityFrames, "The capacity must be positive.");
        }

        _capacityFrames = RoundUpPowerOfTwo(capacityFrames);
        _frameMask = _capacityFrames - 1;
        _samples = new float[_capacityFrames * 2];
    }

    /// <summary>Gets the capacity in stereo frames.</summary>
    public int CapacityFrames => _capacityFrames;

    /// <summary>Gets the number of frames ready to read.</summary>
    public int FramesAvailable
    {
        get
        {
            var write = Volatile.Read(ref _writeFrame);
            var read = Volatile.Read(ref _readFrame);
            return unchecked((int)(write - read));
        }
    }

    /// <summary>Gets the number of frames that can be written without dropping data.</summary>
    public int FramesFree => _capacityFrames - FramesAvailable;

    /// <summary>Writes interleaved stereo frames.</summary>
    /// <param name="frames">Interleaved left/right samples. An odd trailing sample is ignored.</param>
    /// <returns>The number of frames written; less than requested when the buffer is full.</returns>
    public int Write(ReadOnlySpan<float> frames)
    {
        var requested = frames.Length / 2;
        var available = FramesFree;
        var count = Math.Min(requested, available);
        var write = Volatile.Read(ref _writeFrame);

        for (var frame = 0; frame < count; frame++)
        {
            var destination = (int)((write + (uint)frame) & (uint)_frameMask) * 2;
            _samples[destination] = frames[frame * 2];
            _samples[destination + 1] = frames[(frame * 2) + 1];
        }

        Volatile.Write(ref _writeFrame, unchecked(write + (uint)count));
        return count;
    }

    /// <summary>Reads interleaved stereo frames.</summary>
    /// <param name="destination">Destination span. An odd trailing slot is not written.</param>
    /// <returns>The number of frames read; less than requested when the buffer is empty.</returns>
    public int Read(Span<float> destination)
    {
        var requested = destination.Length / 2;
        var available = FramesAvailable;
        var count = Math.Min(requested, available);
        var read = Volatile.Read(ref _readFrame);

        for (var frame = 0; frame < count; frame++)
        {
            var source = (int)((read + (uint)frame) & (uint)_frameMask) * 2;
            destination[frame * 2] = _samples[source];
            destination[(frame * 2) + 1] = _samples[source + 1];
        }

        Volatile.Write(ref _readFrame, unchecked(read + (uint)count));
        return count;
    }

    /// <summary>Drops all buffered frames. Only call when producer and consumer are stopped.</summary>
    public void Clear()
    {
        Volatile.Write(ref _readFrame, 0);
        Volatile.Write(ref _writeFrame, 0);
    }

    private static int RoundUpPowerOfTwo(int value)
    {
        var result = 1;
        while (result < value)
        {
            result <<= 1;
        }

        return result;
    }
}
