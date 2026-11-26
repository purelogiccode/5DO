using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FiveDo.Core;
using MDSSharp;

namespace FiveDo.Storage;

/// <summary>
/// Adapts an Alcohol 120% <c>.mds</c>/<c>.mdf</c> image to the 3DO <see cref="ITrackedDiscSource"/> contract.
/// </summary>
/// <remarks>
/// MDSSharp parses the descriptor and prepares a CUE for the underlying data file(s). A
/// plain raw or cooked CD is referenced in place; split, subchannel-bearing, pregap-padded,
/// compressed, and encrypted images are prepared into a temp cache first. The resulting CUE
/// is opened through <see cref="CueDiscImage"/>, which supplies the same track table and
/// native-sector semantics as the CUE, CHD, CDI, and NRG readers.
/// </remarks>
public sealed class MdsDiscImage : ITrackedDiscSource
{
    private readonly CueDiscImage _inner;

    private MdsDiscImage(CueDiscImage inner)
    {
        _inner = inner;
    }

    /// <summary>Gets the tracks in disc order.</summary>
    public IReadOnlyList<DiscTrack> Tracks => _inner.Tracks;

    /// <summary>Gets the total number of logical sectors across every track.</summary>
    public int TotalSectorCount => _inner.TotalSectorCount;

    /// <summary>Gets the number of logical sectors on the disc.</summary>
    public int SectorCount => _inner.SectorCount;

    /// <summary>Gets the normalized user-data sector size (2048 bytes).</summary>
    public int SectorSize => _inner.SectorSize;

    /// <summary>Opens an Alcohol 120% MDS/MDF image.</summary>
    /// <param name="path">Path to the <c>.mds</c> or <c>.mdx</c> descriptor.</param>
    /// <returns>An open disc source. The caller owns and must dispose it.</returns>
    /// <exception cref="DiscImageException">
    /// The descriptor or its data file is missing, malformed, encrypted with a password, or
    /// describes a DVD rather than a 3DO CD.
    /// </exception>
    public static MdsDiscImage Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            throw new DiscImageException($"MDS descriptor '{path}' does not exist.");
        }

        MdsDisc disc;
        try
        {
            disc = MdsParser.Parse(path);
        }
        catch (Exception exception) when (exception is InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            throw new DiscImageException($"MDS image '{path}' is invalid: {exception.Message}", exception);
        }

        if (disc.IsDvdImage)
        {
            throw new DiscImageException(
                $"MDS image '{path}' declares DVD media; 5DO supports 3DO CD images only.");
        }

        string cacheDirectory;
        try
        {
            var dataFiles = new List<string>(disc.DataFilePaths);
            foreach (string dataFile in disc.DataFilePaths)
            {
                if (SplitImageJoiner.TryGetVolumeSet(dataFile) is { } volumes)
                {
                    dataFiles.AddRange(volumes);
                }
            }

            cacheDirectory = DiscPreparationCache.GetDirectory(path, "mds", dataFiles);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            throw new DiscImageException(
                $"MDS image '{path}' could not create its preparation cache: {exception.Message}",
                exception);
        }

        try
        {
            return new MdsDiscImage(CueDiscImage.Open(GetPreparedCue(disc, cacheDirectory)));
        }
        catch (DiscImageException)
        {
            // A stale or partially prepared cache entry must not hide a readable source.
            DiscPreparationCache.Invalidate(cacheDirectory);
            try
            {
                Directory.CreateDirectory(cacheDirectory);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or NotSupportedException)
            {
                throw new DiscImageException(
                    $"MDS image '{path}' could not recreate its preparation cache: {exception.Message}",
                    exception);
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            throw new DiscImageException(
                $"MDS image '{path}' could not be prepared: {exception.Message}",
                exception);
        }

        try
        {
            return new MdsDiscImage(CueDiscImage.Open(GetPreparedCue(disc, cacheDirectory)));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            throw new DiscImageException(
                $"MDS image '{path}' could not be prepared: {exception.Message}",
                exception);
        }
    }

    /// <summary>Reads one 2048-byte user-data sector.</summary>
    /// <param name="lba">Logical block address, zero-based.</param>
    /// <param name="destination">Destination span of at least 2048 bytes.</param>
    public void ReadSector(int lba, Span<byte> destination) => _inner.ReadSector(lba, destination);

    /// <summary>Reads one native track sector (2048, 2336, or 2352 bytes).</summary>
    /// <param name="lba">Logical block address, zero-based.</param>
    /// <param name="destination">Destination span.</param>
    /// <returns>The number of bytes written.</returns>
    public int ReadTrackSector(int lba, Span<byte> destination) => _inner.ReadTrackSector(lba, destination);

    /// <summary>Disposes the prepared CUE reader.</summary>
    public void Dispose() => _inner.Dispose();

    private static string GetPreparedCue(MdsDisc disc, string cacheDirectory)
    {
        string? existing = DiscPreparationCache.FindPreparedCue(cacheDirectory);
        if (existing is not null)
        {
            return existing;
        }

        lock (DiscPreparationCache.Sync)
        {
            existing = DiscPreparationCache.FindPreparedCue(cacheDirectory);
            if (existing is not null)
            {
                return existing;
            }

            string cuePath = Prepare(disc, cacheDirectory);
            DiscPreparationCache.PublishCue(cuePath, cacheDirectory);
            return Path.Combine(cacheDirectory, DiscPreparationCache.PreparedCueName);
        }
    }

    private static string Prepare(MdsDisc disc, string cacheDirectory)
    {
        if (CanReferenceDataFileDirectly(disc))
        {
            return MdsInputPreparer.WriteCueAsync(
                disc,
                cacheDirectory,
                disc.DataFilePaths[0],
                CancellationToken.None).GetAwaiter().GetResult();
        }

        MdsInputPreparer.Result result = MdsInputPreparer.PrepareAsync(
            disc,
            cacheDirectory,
            onLog: null,
            CancellationToken.None).GetAwaiter().GetResult();

        if (!result.Success || result.CuePath is null)
        {
            throw new DiscImageException(
                $"MDS image '{disc.MdsPath}' could not be prepared: {result.FailureReason}");
        }

        return result.CuePath;
    }

    private static bool CanReferenceDataFileDirectly(MdsDisc disc)
    {
        return disc.DataFilePaths.Count == 1
            && SplitImageJoiner.TryGetVolumeSet(disc.DataFilePaths[0]) is null
            && disc.SessionCount == 1
            && !disc.HasPregapInfo
            && disc.Tracks.All(static track => track.PregapSectors == 0)
            && !disc.NeedsSubchannelStrip
            && !disc.HasEncryptedTrackData
            && !disc.HasCompressedTrackData
            && !disc.IsMdxContainer
            && disc.AllTracksDescribable
            && disc.Tracks.All(static track => track.SectorSize is MdsDisc.RawSectorSize
                or MdsDisc.CookedSectorSize
                or MdsDisc.Mode2XaSectorSize
                or MdsDisc.Mode2Form2SectorSize);
    }
}
