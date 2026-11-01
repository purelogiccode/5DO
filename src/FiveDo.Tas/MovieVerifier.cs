using System;
using System.Collections.Generic;

namespace FiveDo.Tas;

/// <summary>The result of verifying a completed movie playback.</summary>
/// <param name="Ok">Whether every recorded checkpoint matched.</param>
/// <param name="Failures">Human-readable mismatch descriptions.</param>
public sealed record MovieVerification(bool Ok, IReadOnlyList<string> Failures)
{
    /// <summary>Gets a single-line summary suitable for logs and status bars.</summary>
    public string Summary => Ok
        ? "movie verified"
        : $"movie mismatch: {string.Join("; ", Failures)}";
}

/// <summary>
/// Compares a playback result with the evidence recorded in a movie header.
/// </summary>
public static class MovieVerifier
{
    /// <summary>Verifies a completed playback.</summary>
    /// <param name="header">Expected movie metadata.</param>
    /// <param name="playedFrames">Number of frames actually played.</param>
    /// <param name="frameHash">Final assembled frame hash.</param>
    /// <param name="programCounter">Final ARM program counter.</param>
    /// <param name="stateHash">Optional final state hash.</param>
    /// <returns>The verification result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="header"/> is null.</exception>
    public static MovieVerification Verify(
        MovieHeader header,
        int playedFrames,
        uint frameHash,
        uint programCounter,
        uint? stateHash = null)
    {
        ArgumentNullException.ThrowIfNull(header);
        var failures = new List<string>();
        if (playedFrames != header.FrameCount)
        {
            failures.Add($"frames {playedFrames} != {header.FrameCount}");
        }

        if (header.FinalFrameHash is { } expectedFrame && expectedFrame != frameHash)
        {
            failures.Add($"frame 0x{frameHash:x8} != 0x{expectedFrame:x8}");
        }

        if (header.FinalProgramCounter is { } expectedPc && expectedPc != programCounter)
        {
            failures.Add($"pc 0x{programCounter:x8} != 0x{expectedPc:x8}");
        }

        if (header.FinalStateHash is { } expectedState)
        {
            if (stateHash is not { } actualState)
            {
                failures.Add("state hash was not computed");
            }
            else if (expectedState != actualState)
            {
                failures.Add($"state 0x{actualState:x8} != 0x{expectedState:x8}");
            }
        }

        return new MovieVerification(failures.Count == 0, failures);
    }

    /// <summary>Compares the options recorded in a header with a live option dictionary.</summary>
    /// <param name="header">Movie metadata.</param>
    /// <param name="options">Current options.</param>
    /// <returns>Mismatch descriptions; an empty list means the options match.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static IReadOnlyList<string> CompareOptions(
        MovieHeader header,
        IReadOnlyDictionary<string, string> options)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(options);
        var differences = new List<string>();
        if (header.Options is null)
        {
            return differences;
        }

        foreach ((var key, var expected) in header.Options)
        {
            if (!options.TryGetValue(key, out var actual))
            {
                differences.Add($"{key} is missing (recorded {expected})");
            }
            else if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                differences.Add($"{key} is {actual} (recorded {expected})");
            }
        }

        return differences;
    }
}
