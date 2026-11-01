using System;
using System.Collections.Generic;
using FiveDo.Core;

namespace FiveDo.Tas;

/// <summary>
/// Metadata stored in a 5DO movie. The header is JSON and is independent of the
/// input payload so tools can inspect a movie without decompressing it.
/// </summary>
public sealed record MovieHeader
{
    /// <summary>Gets the movie format version.</summary>
    public int FormatVersion { get; init; } = MovieFormat.Version;

    /// <summary>Gets the emulator version that recorded the movie.</summary>
    public string EmulatorVersion { get; init; } = FiveDoVersion.Version;

    /// <summary>Gets the recording timestamp in round-trip UTC format.</summary>
    public string CreatedUtc { get; init; } = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Gets the display name of the game, when known.</summary>
    public string? GameName { get; init; }

    /// <summary>Gets the 5DO/4DO game identifier, when known.</summary>
    public string? GameId { get; init; }

    /// <summary>Gets the disc checksum reported by <c>GameId.Compute</c>.</summary>
    public string? DiscHash { get; init; }

    /// <summary>Gets the disc file name only, never a full local path.</summary>
    public string? DiscFile { get; init; }

    /// <summary>Gets the MD5 of the primary BIOS used for the recording.</summary>
    public string? BiosMd5 { get; init; }

    /// <summary>Gets the catalog or file name of the primary BIOS.</summary>
    public string? BiosName { get; init; }

    /// <summary>Gets the emulated frame at which the first packet was sampled.</summary>
    public long StartFrame { get; init; }

    /// <summary>Gets the number of recorded frames.</summary>
    public int FrameCount { get; init; }

    /// <summary>Gets the final assembled frame hash, when the movie is complete.</summary>
    public uint? FinalFrameHash { get; init; }

    /// <summary>Gets the final ARM program counter, when the movie is complete.</summary>
    public uint? FinalProgramCounter { get; init; }

    /// <summary>Gets the final deterministic state hash, when the movie is complete.</summary>
    public uint? FinalStateHash { get; init; }

    /// <summary>Gets a value indicating whether an embedded starting state is present.</summary>
    public bool HasState { get; init; }

    /// <summary>Gets the emulation options that were active while recording.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Meziantou.Analyzer",
        "MA0016",
        Justification = "Source-generated JSON requires a concrete dictionary for the header contract.")]
    public Dictionary<string, string>? Options { get; init; }

    /// <summary>Gets optional free-form notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Gets the frame after the last recorded frame.</summary>
    public long EndFrame => StartFrame + FrameCount;
}
