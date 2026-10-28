using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Audio;

/// <summary>
/// Records the exact PCM queued for playback plus a per-buffer timing log.
/// </summary>
/// <remarks>
/// <para>
/// Set the <c>FIVEDO_AUDIO_TAP</c> environment variable to a path prefix to enable the tap.
/// The WAV file is the signal the emulator handed to OpenAL, so comparing it with a
/// loopback recording of the device isolates emulator defects from driver, Bluetooth, and
/// speaker problems. The CSV log records queue depth, ring depth, underruns, peaks, and
/// OpenAL errors per buffer so a crack in either recording can be matched to the moment it
/// was queued.
/// </para>
/// <para>
/// Only the audio fill thread writes, and disposal happens after that thread stops.
/// </para>
/// </remarks>
internal sealed class AudioTap : IDisposable
{
    /// <summary>Environment variable that enables the tap when set to a path prefix.</summary>
    internal const string EnvironmentVariable = "FIVEDO_AUDIO_TAP";

    private const int PatchIntervalBuffers = 256;

    private readonly Pcm16WavWriter _wav;
    private readonly StreamWriter _log;
    private readonly long _start = Stopwatch.GetTimestamp();
    private long _buffers;
    private bool _disposed;

    /// <summary>Initializes a tap that writes <c>prefix.wav</c> and <c>prefix.csv</c>.</summary>
    /// <param name="prefix">Path prefix for the two output files.</param>
    internal AudioTap(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        _wav = new Pcm16WavWriter(prefix + ".wav", EmulatorConstants.SoundClock);
        _log = new StreamWriter(prefix + ".csv") { AutoFlush = true };
        _log.WriteLine("elapsedMs,buffer,frames,ringFrames,queuedBuffers,queuedFrames,underrun,peak,openAlError");
    }

    /// <summary>Gets the number of buffers written to the tap.</summary>
    internal long BuffersWritten => _buffers;

    /// <summary>Records one buffer exactly as it was queued.</summary>
    /// <param name="samples">Interleaved stereo samples queued for playback.</param>
    /// <param name="frames">Number of stereo frames in <paramref name="samples"/>.</param>
    /// <param name="ringFrames">Frames still waiting in the emulation ring after the read.</param>
    /// <param name="queuedBuffers">Buffers attached to the source after this one was queued.</param>
    /// <param name="queuedFrames">Frames attached to the source after this one was queued.</param>
    /// <param name="underrun">Whether the buffer is the silent filler queued after a dry ring.</param>
    /// <param name="peak">Peak absolute sample value in this buffer.</param>
    /// <param name="openAlError">OpenAL error reported while queueing, or <see langword="null"/>.</param>
    internal void Write(
        ReadOnlySpan<short> samples,
        int frames,
        int ringFrames,
        long queuedBuffers,
        long queuedFrames,
        bool underrun,
        float peak,
        string? openAlError)
    {
        if (_disposed)
        {
            return;
        }

        _wav.Write(samples);
        var elapsed = Stopwatch.GetElapsedTime(_start).TotalMilliseconds;
        _log.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{elapsed:F3},{_buffers},{frames},{ringFrames},{queuedBuffers},{queuedFrames},{(underrun ? 1 : 0)},{peak:F4},{openAlError}"));
        _buffers++;
        if (_buffers % PatchIntervalBuffers == 0)
        {
            _wav.PatchHeader();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _wav.Dispose();
        _log.Dispose();
    }
}
