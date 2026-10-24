using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ThreeDo.Core;

namespace ThreeDo.Storage;

/// <summary>
/// A minimal CUE sheet parser for single-file, single-data-track 3DO images.
/// </summary>
/// <remarks>
/// Multi-file and multi-track CUE images are common for audio CDs; 3DO games use a single
/// Mode 1 data track followed by optional CD audio, so this parser deliberately supports the
/// first data track only and rejects a CUE that has no data track.
/// </remarks>
public static class CueSheet
{
    /// <summary>Opens the first data track described by a CUE sheet.</summary>
    /// <param name="cuePath">Path to the CUE file.</param>
    /// <returns>A disc source over the referenced BIN or ISO file.</returns>
    /// <exception cref="DiscImageException">Thrown when the sheet is malformed or has no data track.</exception>
    public static IDiscSource Open(string cuePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cuePath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(cuePath)) ?? ".";
        var lines = File.ReadAllLines(cuePath);
        string? currentFile = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("REM", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tokens = Tokenize(line);
            if (tokens.Count == 0)
            {
                continue;
            }

            switch (tokens[0].ToUpperInvariant())
            {
                case "FILE":
                    if (tokens.Count < 2)
                    {
                        throw new DiscImageException($"Malformed FILE line in '{cuePath}'.");
                    }

                    currentFile = Path.Combine(directory, tokens[1]);
                    break;
                case "TRACK":
                    if (tokens.Count < 3)
                    {
                        throw new DiscImageException($"Malformed TRACK line in '{cuePath}'.");
                    }

                    var mode = tokens[2].ToUpperInvariant();
                    if (!mode.StartsWith("MODE", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (currentFile is null)
                    {
                        throw new DiscImageException($"TRACK appears before FILE in '{cuePath}'.");
                    }

                    var (payloadOffset, sectorBytes) = mode switch
                    {
                        "MODE1/2048" => (0, 2048),
                        "MODE1/2352" => (16, 2352),
                        "MODE2/2352" or "MODE2/2336" => (24, 2352),
                        _ => throw new DiscImageException($"Unsupported CUE track mode '{mode}'."),
                    };

                    var trackStartFrame = FindIndex01(lines, cuePath);
                    var startOffset = trackStartFrame * sectorBytes;
                    return sectorBytes == 2048
                        ? new IsoSegment(currentFile, startOffset)
                        : new BinImage(currentFile, startOffset, payloadOffset: payloadOffset);
            }
        }

        throw new DiscImageException($"CUE sheet '{cuePath}' has no data track.");
    }

    private static long FindIndex01(IReadOnlyList<string> lines, string cuePath)
    {
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("INDEX 01", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var time = parts[^1].Split(':');
            if (time.Length != 3)
            {
                throw new DiscImageException($"Malformed INDEX 01 time in '{cuePath}'.");
            }

            return (long.Parse(time[0], CultureInfo.InvariantCulture) * 60 * 75)
                + (long.Parse(time[1], CultureInfo.InvariantCulture) * 75)
                + long.Parse(time[2], CultureInfo.InvariantCulture);
        }

        throw new DiscImageException($"CUE sheet '{cuePath}' has no INDEX 01.");
    }

    private static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var builder = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (!quoted && char.IsWhiteSpace(c))
            {
                if (builder.Length > 0)
                {
                    tokens.Add(builder.ToString());
                    builder.Clear();
                }

                continue;
            }

            builder.Append(c);
        }

        if (builder.Length > 0)
        {
            tokens.Add(builder.ToString());
        }

        return tokens;
    }

    /// <summary>An ISO image starting at a byte offset (CUE MODE1/2048).</summary>
    private sealed class IsoSegment : IDiscSource
    {
        private readonly FileStream _stream;
        private readonly long _startOffset;
        private readonly byte[] _sector = new byte[2048];

        internal IsoSegment(string path, long startOffset)
        {
            _stream = File.OpenRead(path);
            _startOffset = startOffset;
            var remaining = _stream.Length - startOffset;
            if (remaining < 0 || remaining % 2048 != 0)
            {
                _stream.Dispose();
                throw new DiscImageException($"ISO segment in '{path}' is not a whole number of 2048-byte sectors.");
            }

            SectorCount = (int)(remaining / 2048);
        }

        public int SectorCount { get; }

        public int SectorSize => 2048;

        public void ReadSector(int lba, Span<byte> destination)
        {
            if (lba < 0 || lba >= SectorCount)
            {
                throw new DiscImageException($"Sector {lba} is outside the image ({SectorCount} sectors).");
            }

            _stream.Position = _startOffset + ((long)lba * 2048);
            _stream.ReadExactly(_sector);
            _sector.AsSpan(0, Math.Min(2048, destination.Length)).CopyTo(destination);
        }

        public void Dispose() => _stream.Dispose();
    }
}
