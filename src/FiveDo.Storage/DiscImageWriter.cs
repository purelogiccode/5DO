using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CHDSharp.Encoder;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>Output container for <see cref="DiscImageWriter"/>.</summary>
public enum DiscImageFormat
{
    /// <summary>A cooked 2048-byte-sector ISO image.</summary>
    Iso,

    /// <summary>A cooked 2048-byte-sector BIN image.</summary>
    Bin,

    /// <summary>A CUE sheet plus its cooked BIN track.</summary>
    Cue,

    /// <summary>A flat CHD with 2048-byte units.</summary>
    Chd,
}

/// <summary>
/// Writes a data-only disc source to ISO, BIN, or CUE. CHD output remains an external
/// <c>chdman</c> step because the managed reader does not include an encoder.
/// </summary>
public static class DiscImageWriter
{
    /// <summary>Converts a data-only disc source to the requested container.</summary>
    /// <param name="source">Open source image.</param>
    /// <param name="outputPath">Destination path. For CUE, the companion BIN uses the same stem.</param>
    /// <param name="format">Output container.</param>
    /// <returns>The number of 2048-byte sectors written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a parameter is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when the source contains audio tracks.</exception>
    public static int Write(IDiscSource source, string outputPath, DiscImageFormat format)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (source is ITrackedDiscSource tracked && tracked.Tracks.Any(track => track.IsAudio))
        {
            throw new NotSupportedException(
                "Disc conversion currently supports data-only images; audio tracks require a raw BIN/CUE writer.");
        }

        if (format == DiscImageFormat.Chd)
        {
            // The encoder needs a BIN input, but it must not use the natural
            // <name>.bin path: converting game.bin -> game.chd would otherwise
            // overwrite (and then delete) the source image.
            string fullOutput = Path.GetFullPath(outputPath);
            string directory = Path.GetDirectoryName(fullOutput) ?? ".";
            string tempBin = Path.Combine(
                directory,
                $".{Path.GetFileNameWithoutExtension(fullOutput)}.{Guid.NewGuid():N}.bin");
            WriteSectors(source, tempBin);
            try
            {
                ChdEncoder.EncodeRaw(
                    tempBin,
                    outputPath,
                    hunkBytes: 16384,
                    unitBytes: 2048,
                    codecTags: new[] { CodecTags.Zlib },
                    options: null,
                    CancellationToken.None);
            }
            finally
            {
                File.Delete(tempBin);
            }

            return source.SectorCount;
        }

        if (format == DiscImageFormat.Cue)
        {
            string binPath = Path.ChangeExtension(outputPath, ".bin");
            if (string.Equals(
                    Path.GetFullPath(binPath),
                    Path.GetFullPath(outputPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "A CUE output path cannot use the .bin extension; the companion BIN would overwrite the CUE sheet.",
                    nameof(outputPath));
            }

            WriteSectors(source, binPath);
            WriteCue(outputPath, Path.GetFileName(binPath));
            return source.SectorCount;
        }

        WriteSectors(source, outputPath);
        return source.SectorCount;
    }

    private static void WriteSectors(IDiscSource source, string path)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        Directory.CreateDirectory(directory);
        string tempPath = path + ".tmp";
        var sector = new byte[IsoImage.SectorBytes];
        try
        {
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 1 << 20))
            {
                for (var lba = 0; lba < source.SectorCount; lba++)
                {
                    source.ReadSector(lba, sector);
                    stream.Write(sector);
                }
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void WriteCue(string cuePath, string binName)
    {
        var text = new StringBuilder();
        text.AppendLine($"FILE \"{binName}\" BINARY");
        text.AppendLine("  TRACK 01 MODE1/2048");
        text.AppendLine("    INDEX 01 00:00:00");
        File.WriteAllText(cuePath, text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Parses a container name.</summary>
    /// <param name="value">One of <c>iso</c>, <c>bin</c>, <c>cue</c>, or <c>chd</c>.</param>
    /// <param name="format">The parsed format.</param>
    /// <returns><see langword="true"/> when the name is supported.</returns>
    public static bool TryParseFormat(string? value, out DiscImageFormat format)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "iso":
                format = DiscImageFormat.Iso;
                return true;
            case "bin":
                format = DiscImageFormat.Bin;
                return true;
            case "cue":
                format = DiscImageFormat.Cue;
                return true;
            case "chd":
                format = DiscImageFormat.Chd;
                return true;
            default:
                format = DiscImageFormat.Iso;
                return false;
        }
    }

    /// <summary>Infers a format from a path extension.</summary>
    /// <param name="path">Output path.</param>
    /// <param name="format">The inferred format.</param>
    /// <returns><see langword="true"/> when the extension is supported.</returns>
    public static bool TryInferFormat(string path, out DiscImageFormat format)
    {
        return TryParseFormat(Path.GetExtension(path).TrimStart('.'), out format);
    }
}
