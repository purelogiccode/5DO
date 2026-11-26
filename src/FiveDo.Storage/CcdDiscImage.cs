using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CCDSharp;
using CCDSharp.Models;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// Adapts a CloneCD <c>.ccd</c>/<c>.img</c>/<c>.sub</c> image to the 3DO
/// <see cref="ITrackedDiscSource"/> contract.
/// </summary>
/// <remarks>
/// CCDSharp parses the descriptor and produces a CUE that references the raw <c>.img</c>
/// in place. The CUE is opened through <see cref="CueDiscImage"/>, which gives the image
/// the same logical track table and native-sector semantics as CUE, CHD, CDI, NRG, and MDS.
/// The optional <c>.sub</c> file is not needed by the 3DO CD-ROM device and is ignored.
/// </remarks>
public sealed class CcdDiscImage : ITrackedDiscSource
{
    private readonly CueDiscImage _inner;

    private CcdDiscImage(CueDiscImage inner)
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

    /// <summary>Opens a CloneCD image.</summary>
    /// <param name="path">Path to the <c>.ccd</c> descriptor.</param>
    /// <returns>An open disc source. The caller owns and must dispose it.</returns>
    /// <exception cref="DiscImageException">
    /// The descriptor, <c>.img</c> data file, or track layout is missing or malformed, or the
    /// image uses CloneCD's scrambled-data-track mode.
    /// </exception>
    public static CcdDiscImage Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            throw new DiscImageException($"CloneCD descriptor '{path}' does not exist.");
        }

        if (!CcdConverter.IsCcdFile(path))
        {
            throw new DiscImageException(
                $"CloneCD image '{path}' is invalid: the [CloneCD] header is missing.");
        }

        DiscImage disc;
        try
        {
            disc = CcdConverter.Parse(path);
        }
        catch (Exception exception) when (exception is InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            throw new DiscImageException(
                $"CloneCD image '{path}' is invalid: {exception.Message}",
                exception);
        }

        if (disc.DataTracksScrambled)
        {
            throw new DiscImageException(
                $"CloneCD image '{path}' marks its data tracks as scrambled; "
                + "descramble the image before opening it in 5DO.");
        }

        string? imagePath = string.IsNullOrWhiteSpace(disc.ImgFilePath)
            || !File.Exists(disc.ImgFilePath)
                ? ResolveImagePath(path)
                : Path.GetFullPath(disc.ImgFilePath);
        if (imagePath is null)
        {
            throw new DiscImageException(
                $"CloneCD image '{path}' is missing its .img data file.");
        }

        string cacheDirectory;
        try
        {
            cacheDirectory = DiscPreparationCache.GetDirectory(path, "ccd", [imagePath]);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            throw new DiscImageException(
                $"CloneCD image '{path}' could not create its preparation cache: {exception.Message}",
                exception);
        }

        try
        {
            return new CcdDiscImage(CueDiscImage.Open(GetPreparedCue(disc, cacheDirectory, imagePath)));
        }
        catch (DiscImageException)
        {
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
                    $"CloneCD image '{path}' could not recreate its preparation cache: {exception.Message}",
                    exception);
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            throw new DiscImageException(
                $"CloneCD image '{path}' could not be prepared: {exception.Message}",
                exception);
        }

        try
        {
            return new CcdDiscImage(CueDiscImage.Open(GetPreparedCue(disc, cacheDirectory, imagePath)));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            throw new DiscImageException(
                $"CloneCD image '{path}' could not be prepared: {exception.Message}",
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

    private static string GetPreparedCue(DiscImage disc, string cacheDirectory, string imagePath)
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

            string cuePath = Path.Combine(cacheDirectory, DiscPreparationCache.PreparedCueName);
            string cueText = CcdConverter.GenerateCueSheet(disc, imagePath);

            // No BOM: the shared CUE parser, like chdman, treats a BOM as part of the first token.
            File.WriteAllText(cuePath, cueText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return cuePath;
        }
    }

    private static string? ResolveImagePath(string descriptorPath)
    {
        string directory = Path.GetDirectoryName(descriptorPath) ?? string.Empty;
        string baseName = Path.GetFileNameWithoutExtension(descriptorPath);
        try
        {
            foreach (string candidate in Directory.EnumerateFiles(
                directory,
                "*",
                SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(
                        Path.GetFileNameWithoutExtension(candidate),
                        baseName,
                        StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        Path.GetExtension(candidate),
                        ".img",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            // A case-insensitive companion is optional; the caller reports the missing file.
        }

        return null;
    }
}
