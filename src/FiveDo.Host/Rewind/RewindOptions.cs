using System;

namespace FiveDo.Host.Rewind;

/// <summary>
/// Configuration for the rewind history buffer.
/// </summary>
/// <remarks>
/// The buffer keeps compressed keyframes and a per-frame input history. Keyframes are
/// captured at most once every <see cref="KeyframeInterval"/> frames; rewinding to a frame
/// between keyframes loads the nearest earlier keyframe and replays the recorded input.
/// </remarks>
public sealed record RewindOptions
{
    /// <summary>Gets the smallest supported history length in seconds.</summary>
    public const int MinSeconds = 30;

    /// <summary>Gets the largest supported history length in seconds.</summary>
    public const int MaxSeconds = 300;

    /// <summary>Gets the smallest supported keyframe interval in frames.</summary>
    public const int MinKeyframeInterval = 1;

    /// <summary>Gets the largest supported keyframe interval in frames.</summary>
    public const int MaxKeyframeInterval = 600;

    /// <summary>Gets the default keyframe interval in frames.</summary>
    public const int DefaultKeyframeInterval = 120;

    /// <summary>Gets the default memory limit for compressed keyframes.</summary>
    public const long DefaultMemoryLimitBytes = 256L * 1024 * 1024;

    /// <summary>Gets the default rewind options.</summary>
    public static RewindOptions Default { get; } = new();

    /// <summary>Gets a value indicating whether the rewind buffer records history.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets the history length in seconds.</summary>
    public int Seconds { get; init; } = 60;

    /// <summary>Gets the maximum number of frames between keyframes.</summary>
    public int KeyframeInterval { get; init; } = DefaultKeyframeInterval;

    /// <summary>Gets the memory limit for compressed keyframes in bytes.</summary>
    public long MemoryLimitBytes { get; init; } = DefaultMemoryLimitBytes;

    /// <summary>Validates the option values.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is outside its supported range.</exception>
    public void Validate()
    {
        ValidateSeconds(Seconds);
        ValidateKeyframeInterval(KeyframeInterval);
        ValidateMemoryLimit(MemoryLimitBytes);
    }

    private static void ValidateSeconds(int seconds)
    {
        if (seconds is < MinSeconds or > MaxSeconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(seconds),
                seconds,
                $"The rewind history must be between {MinSeconds} and {MaxSeconds} seconds.");
        }
    }

    private static void ValidateKeyframeInterval(int keyframeInterval)
    {
        if (keyframeInterval is < MinKeyframeInterval or > MaxKeyframeInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(keyframeInterval),
                keyframeInterval,
                $"The keyframe interval must be between {MinKeyframeInterval} and {MaxKeyframeInterval} frames.");
        }
    }

    private static void ValidateMemoryLimit(long memoryLimitBytes)
    {
        if (memoryLimitBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(memoryLimitBytes),
                memoryLimitBytes,
                "The rewind memory limit must be positive.");
        }
    }
}
