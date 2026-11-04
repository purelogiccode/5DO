using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Host;

/// <summary>Progress for a library disc-health check.</summary>
/// <param name="Completed">Number of discs checked.</param>
/// <param name="Total">Total number of discs.</param>
/// <param name="Path">Disc that was just checked.</param>
public sealed record LibraryHealthProgress(int Completed, int Total, string Path);

/// <summary>
/// Reads disc images end to end and matches them against an optional Redump/No-Intro
/// DAT so the library can show whether a failure is the emulator or the image.
/// </summary>
public static class LibraryHealthVerifier
{
    /// <summary>Verifies one disc image.</summary>
    /// <param name="discPath">Disc path.</param>
    /// <param name="dat">Optional DAT used for the known-dump match.</param>
    /// <param name="maxErrors">Maximum read errors retained in the detail.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The health record. Read and open failures are reported, not thrown.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public static DiscHealthRecord Verify(
        string discPath,
        RedumpDat? dat,
        int maxErrors = 8,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(discPath);
        ArgumentOutOfRangeException.ThrowIfNegative(maxErrors);
        cancellationToken.ThrowIfCancellationRequested();
        var info = new FileInfo(discPath);
        if (!info.Exists)
        {
            return Fail(discPath, info, "The disc image does not exist.");
        }

        try
        {
            using IDiscSource disc = DiscImageFactory.Open(discPath, chdReadAheadHunks: 0);
            DiscVerificationResult read = DiscVerifier.Verify(disc, maxErrors);
            cancellationToken.ThrowIfCancellationRequested();
            if (!read.Ok)
            {
                var detail = read.Errors.Count > 0
                    ? read.Errors[0]
                    : $"Read {read.SectorsVerified:N0} of {read.Sectors:N0} sectors.";
                return Create(discPath, info, DiscHealthStatus.ReadError, detail, read.Tracks);
            }

            if (dat is null)
            {
                return Create(
                    discPath,
                    info,
                    DiscHealthStatus.NoDat,
                    "Readable. Configure a Redump/No-Intro DAT to check the dump.",
                    read.Tracks);
            }

            DiscDatResult match = DiscHealth.Verify(disc, dat);
            cancellationToken.ThrowIfCancellationRequested();
            DiscHealthRecord record = Create(
                discPath,
                info,
                match.KnownDump ? DiscHealthStatus.Verified : DiscHealthStatus.Mismatch,
                match.KnownDump ? $"Known dump: {match.GameName}." : "Readable, but no DAT entry matched.",
                match.Tracks.Count);
            record.MatchedTracks = match.MatchedTracks;
            if (match.KnownDump)
            {
                record.GameName = match.GameName;
                foreach (DiscDatTrackResult track in match.Tracks)
                {
                    if (track.Matched)
                    {
                        record.RomName = track.Rom;
                        break;
                    }
                }
            }

            if (match.Tracks.Count > 0)
            {
                DiscDatTrackResult first = match.Tracks[0];
                record.Crc32 = first.Crc32;
                record.Md5 = first.Md5;
                record.Sha1 = first.Sha1;
            }

            return record;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Corrupt images can surface arbitrary container errors; the library must
            // report them instead of terminating the scan.
            Serilog.Log.Debug(exception, "Disc health check failed for {Path}.", discPath);
            return Fail(discPath, info, exception.Message);
        }
    }

    /// <summary>Verifies every library entry, reporting progress as it goes.</summary>
    /// <param name="entries">Library entries to check.</param>
    /// <param name="dat">Optional DAT used for the known-dump match.</param>
    /// <param name="progress">Optional progress receiver.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One record per entry, in input order.</returns>
    public static IReadOnlyList<DiscHealthRecord> VerifyAll(
        IEnumerable<GameLibraryEntry> entries,
        RedumpDat? dat,
        IProgress<LibraryHealthProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var paths = new List<string>();
        foreach (GameLibraryEntry entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Path))
            {
                paths.Add(entry.Path);
            }
        }

        var records = new List<DiscHealthRecord>(paths.Count);
        for (var i = 0; i < paths.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            records.Add(Verify(paths[i], dat, cancellationToken: cancellationToken));
            progress?.Report(new LibraryHealthProgress(i + 1, paths.Count, paths[i]));
        }

        return records;
    }

    private static DiscHealthRecord Create(
        string path,
        FileInfo info,
        DiscHealthStatus status,
        string detail,
        int trackCount)
    {
        DiscHealthRecord record = DiscHealthRecord.Create(path, info, status);
        record.Detail = detail;
        record.TrackCount = trackCount;
        return record;
    }

    private static DiscHealthRecord Fail(string path, FileInfo info, string detail)
    {
        DiscHealthRecord record = DiscHealthRecord.Create(path, info, DiscHealthStatus.ReadError);
        record.Detail = detail;
        return record;
    }
}
