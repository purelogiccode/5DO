using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>Result of reading every sector of a disc image.</summary>
/// <param name="Ok">Whether every sector was read without an error.</param>
/// <param name="Sectors">Declared logical sector count.</param>
/// <param name="SectorsVerified">Sectors read successfully.</param>
/// <param name="Tracks">Number of tracks reported by the source.</param>
/// <param name="BytesVerified">Native payload bytes read.</param>
/// <param name="Hash">FNV-1a 64-bit hash of the native payloads.</param>
/// <param name="Errors">First read errors, capped by the caller.</param>
public sealed record DiscVerificationResult(
    bool Ok,
    int Sectors,
    int SectorsVerified,
    int Tracks,
    long BytesVerified,
    ulong Hash,
    IReadOnlyList<string> Errors);

/// <summary>Progress from a full disc verification pass.</summary>
/// <param name="SectorsVerified">Sectors read so far.</param>
/// <param name="TotalSectors">Sectors the disc reports.</param>
/// <param name="BytesVerified">Native track bytes read so far.</param>
/// <param name="TrackNumber">Current one-based track number, or zero before the first read.</param>
/// <param name="TrackCount">Number of tracks in the pass.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DiscVerificationProgress(
    int SectorsVerified,
    int TotalSectors,
    long BytesVerified,
    int TrackNumber,
    int TrackCount);

/// <summary>
/// Reads every sector of a disc image to surface CHD/CUE/BIN corruption without booting.
/// </summary>
/// <remarks>
/// Native track sectors are preferred when available, so audio tracks and raw data are
/// verified too. The hash is FNV-1a over the bytes in logical order and is diagnostic
/// only; it is stable for the same container contents.
/// </remarks>
public static class DiscVerifier
{
    private const ulong Fnv64Offset = 14695981039346656037;
    private const ulong Fnv64Prime = 1099511628211;

    /// <summary>Verifies every sector of a disc source.</summary>
    /// <param name="disc">Disc source to read. The verifier does not dispose it.</param>
    /// <param name="maxErrors">Maximum errors to retain. Default 32.</param>
    /// <param name="progress">Optional progress callback, called periodically.</param>
    /// <returns>The verification result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="disc"/> is null.</exception>
    public static DiscVerificationResult Verify(
        IDiscSource disc,
        int maxErrors = 32,
        Action<DiscVerificationProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(disc);
        ArgumentOutOfRangeException.ThrowIfNegative(maxErrors);
        var errors = new List<string>(Math.Min(maxErrors, 8));
        ulong hash = Fnv64Offset;
        long bytes = 0;
        var verified = 0;
        var trackCount = disc is ITrackedDiscSource tracked ? tracked.Tracks.Count : 1;
        var totalSectors = disc is ITrackedDiscSource trackedSource
            ? trackedSource.TotalSectorCount
            : disc.SectorCount;
        progress?.Invoke(new DiscVerificationProgress(0, totalSectors, 0, 0, trackCount));

        if (disc is ITrackedDiscSource trackedDisc)
        {
            foreach (DiscTrack track in trackedDisc.Tracks)
            {
                var buffer = new byte[Math.Max(1, track.SectorSize)];
                for (var i = 0; i < track.SectorCount; i++)
                {
                    int lba = track.StartLba + i;
                    try
                    {
                        int read = trackedDisc.ReadTrackSector(lba, buffer);
                        if (read <= 0)
                        {
                            AddError(errors, maxErrors, lba, "empty sector");
                            continue;
                        }

                        for (var b = 0; b < read; b++)
                        {
                            hash = unchecked((hash ^ buffer[b]) * Fnv64Prime);
                        }

                        bytes += read;
                        verified++;
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        AddError(errors, maxErrors, lba, exception.Message);
                    }

                    if (verified % 4096 == 0)
                    {
                        progress?.Invoke(new DiscVerificationProgress(
                            verified,
                            totalSectors,
                            bytes,
                            track.Number,
                            trackCount));
                    }
                }
            }
        }
        else
        {
            var buffer = new byte[2048];
            for (var lba = 0; lba < disc.SectorCount; lba++)
            {
                try
                {
                    disc.ReadSector(lba, buffer);
                    for (var b = 0; b < buffer.Length; b++)
                    {
                        hash = unchecked((hash ^ buffer[b]) * Fnv64Prime);
                    }

                    bytes += buffer.Length;
                    verified++;
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    AddError(errors, maxErrors, lba, exception.Message);
                }

                if (verified % 4096 == 0)
                {
                    progress?.Invoke(new DiscVerificationProgress(
                        verified,
                        totalSectors,
                        bytes,
                        1,
                        1));
                }
            }
        }

        progress?.Invoke(new DiscVerificationProgress(
            verified,
            totalSectors,
            bytes,
            trackCount,
            trackCount));
        return new DiscVerificationResult(
            Ok: verified == totalSectors && errors.Count == 0 && totalSectors > 0,
            Sectors: totalSectors,
            SectorsVerified: verified,
            Tracks: trackCount,
            BytesVerified: bytes,
            Hash: hash,
            Errors: errors);
    }

    private static void AddError(List<string> errors, int maxErrors, int lba, string message)
    {
        if (errors.Count >= maxErrors)
        {
            return;
        }

        errors.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"LBA {lba}: {message}"));
    }
}
