using System;
using System.Collections.Generic;
using System.Globalization;
using CHDSharp.Encoder.Models;

namespace FiveDo.Storage;

/// <summary>
/// Parses a CUE sheet whose FILE entries live in a virtual archive.
/// </summary>
/// <remarks>
/// This is the archive subset of MAME's cue parser: BINARY files, MODE1/MODE2/AUDIO
/// tracks, and INDEX 01. PREGAP, POSTGAP, WAVE, and INDEX 00 layouts are rejected with an
/// actionable message because the 3DO library does not use them and the existing
/// <see cref="CueDiscImage"/> adapter has no gap-data source. File references are
/// resolved relative to the CUE entry.
/// </remarks>
internal static class VirtualCueParser
{
    /// <summary>Parses CUE text into a CHDSharp track table.</summary>
    /// <param name="cueText">CUE sheet text.</param>
    /// <param name="cueEntry">Normalized archive entry name of the CUE file.</param>
    /// <param name="getLength">Returns the uncompressed length of a referenced entry.</param>
    /// <returns>The parsed table of contents.</returns>
    /// <exception cref="DiscImageException">The sheet uses an unsupported or missing layout.</exception>
    public static CdToc Parse(
        string cueText,
        string cueEntry,
        Func<string, long> getLength)
    {
        ArgumentNullException.ThrowIfNull(cueText);
        ArgumentNullException.ThrowIfNull(getLength);
        ArgumentException.ThrowIfNullOrWhiteSpace(cueEntry);

        var pending = new List<PendingTrack>();
        var currentFile = string.Empty;
        PendingTrack? current = null;
        foreach (var rawLine in cueText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            List<string> tokens = Tokenize(line);
            if (tokens.Count == 0)
            {
                continue;
            }

            switch (tokens[0].ToUpperInvariant())
            {
                case "REM":
                case "CATALOG":
                case "TITLE":
                case "PERFORMER":
                case "SONGWRITER":
                case "FLAGS":
                    break;

                case "FILE":
                    if (tokens.Count < 3)
                    {
                        throw new DiscImageException($"Malformed CUE FILE line: {line}");
                    }

                    if (!string.Equals(tokens[2], "BINARY", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(tokens[2], "MOTOROLA", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new DiscImageException(
                            $"CUE file type '{tokens[2]}' is not supported inside an archive; "
                            + "use a BINARY track set or extract the disc.");
                    }

                    if (current is not null)
                    {
                        pending.Add(current);
                        current = null;
                    }

                    currentFile = ResolveEntry(cueEntry, tokens[1]);
                    break;

                case "TRACK":
                    if (tokens.Count < 3)
                    {
                        throw new DiscImageException($"Malformed CUE TRACK line: {line}");
                    }

                    if (current is not null)
                    {
                        pending.Add(current);
                    }

                    if (string.IsNullOrEmpty(currentFile))
                    {
                        throw new DiscImageException("CUE TRACK appears before FILE.");
                    }

                    if (!int.TryParse(
                            tokens[1],
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var number)
                        || number is < 1 or > 99)
                    {
                        throw new DiscImageException($"Invalid CUE track number '{tokens[1]}'.");
                    }

                    current = new PendingTrack(number, currentFile, ParseTrackType(tokens[2]));
                    break;

                case "INDEX":
                    if (current is null || tokens.Count < 3)
                    {
                        throw new DiscImageException($"Malformed CUE INDEX line: {line}");
                    }

                    if (string.Equals(tokens[1], "01", StringComparison.Ordinal))
                    {
                        current.Index01 = ParseMsf(tokens[2]);
                    }
                    else if (string.Equals(tokens[1], "00", StringComparison.Ordinal))
                    {
                        current.Index00 = ParseMsf(tokens[2]);
                    }

                    break;

                case "PREGAP":
                case "POSTGAP":
                    throw new DiscImageException(
                        $"CUE {tokens[0]} is not supported inside an archive; extract the disc set.");
            }
        }

        if (current is not null)
        {
            pending.Add(current);
        }

        if (pending.Count == 0)
        {
            throw new DiscImageException($"CUE sheet '{cueEntry}' has no tracks.");
        }

        var byFile = new Dictionary<string, List<PendingTrack>>(StringComparer.OrdinalIgnoreCase);
        foreach (PendingTrack track in pending)
        {
            if (track.Index01 < 0)
            {
                throw new DiscImageException(
                    $"CUE track {track.Number} has no INDEX 01; archive CUEs require explicit INDEX 01.");
            }

            if (track.Index00 >= 0)
            {
                throw new DiscImageException(
                    $"CUE track {track.Number} uses INDEX 00; extract the disc set to preserve its pregap.");
            }

            if (!byFile.TryGetValue(track.FileName, out List<PendingTrack>? fileTracks))
            {
                fileTracks = [];
                byFile[track.FileName] = fileTracks;
            }

            fileTracks.Add(track);
        }

        var toc = new CdToc();
        foreach (PendingTrack track in pending)
        {
            List<PendingTrack> fileTracks = byFile[track.FileName];
            int sectorSize = track.Type.DataSize;
            foreach (PendingTrack other in fileTracks)
            {
                if (other.Type.DataSize != sectorSize)
                {
                    throw new DiscImageException(
                        $"CUE file '{track.FileName}' mixes {sectorSize}-byte and "
                        + $"{other.Type.DataSize}-byte tracks; extract the disc set.");
                }
            }

            long fileLength = getLength(track.FileName);
            long offset = (long)track.Index01 * sectorSize;
            if (offset > fileLength)
            {
                throw new DiscImageException(
                    $"CUE track {track.Number} starts at byte {offset}, beyond '{track.FileName}' ({fileLength} bytes).");
            }

            int nextIndex = int.MaxValue;
            foreach (PendingTrack other in fileTracks)
            {
                if (other.Index01 > track.Index01 && other.Index01 < nextIndex)
                {
                    nextIndex = other.Index01;
                }
            }

            long frames = nextIndex == int.MaxValue
                ? (fileLength - offset) / sectorSize
                : nextIndex - track.Index01;
            if (frames <= 0 || frames > int.MaxValue)
            {
                throw new DiscImageException(
                    $"CUE track {track.Number} has an invalid length of {frames} sectors.");
            }

            toc.Tracks.Add(new CdTrack
            {
                Number = track.Number,
                TrackType = track.Type.Kind,
                SubType = 2,
                DataSize = sectorSize,
                SubSize = 0,
                Frames = (int)frames,
                Pregap = 0,
                Postgap = 0,
                PgSub = 2,
                FileName = track.FileName,
                FileOffset = offset,
                Index00 = -1,
                Index01 = track.Index01,
                Swap = track.Type.Kind == CdTrackType.Audio,
                LeadIn = -1,
                LeadOut = -1,
            });
        }

        return toc;
    }

    private static string ResolveEntry(string cueEntry, string fileName)
    {
        var normalized = fileName.Replace('\\', '/').Trim();
        if (normalized.Length == 0)
        {
            throw new DiscImageException($"CUE sheet '{cueEntry}' has an empty FILE name.");
        }

        if (normalized.StartsWith('/'))
        {
            normalized = normalized.TrimStart('/');
        }
        else
        {
            var slash = cueEntry.LastIndexOf('/');
            if (slash >= 0)
            {
                normalized = cueEntry[..(slash + 1)] + normalized;
            }
        }

        var parts = new List<string>();
        foreach (var part in normalized.Split('/'))
        {
            if (part.Length == 0 || string.Equals(part, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(part, "..", StringComparison.Ordinal))
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }

                continue;
            }

            parts.Add(part);
        }

        if (parts.Count == 0)
        {
            throw new DiscImageException($"CUE sheet '{cueEntry}' resolves an empty FILE name.");
        }

        return string.Join('/', parts);
    }

    private static TrackType ParseTrackType(string token)
    {
        return token.ToUpperInvariant() switch
        {
            "AUDIO" => new TrackType(CdTrackType.Audio, 2352),
            "MODE1/2048" => new TrackType(CdTrackType.Mode1, 2048),
            "MODE1/2352" => new TrackType(CdTrackType.Mode1Raw, 2352),
            "MODE2/2048" => new TrackType(CdTrackType.Mode2Form1, 2048),
            "MODE2/2324" => new TrackType(CdTrackType.Mode2Form2, 2324),
            "MODE2/2336" => new TrackType(CdTrackType.Mode2, 2336),
            "MODE2/2352" => new TrackType(CdTrackType.Mode2Raw, 2352),
            _ => throw new DiscImageException($"Unsupported CUE track type '{token}'."),
        };
    }

    private static int ParseMsf(string token)
    {
        string[] pieces = token.Split(':');
        if (pieces.Length != 3
            || !int.TryParse(pieces[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || !int.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || !int.TryParse(pieces[2], NumberStyles.None, CultureInfo.InvariantCulture, out var frames)
            || minutes < 0
            || seconds is < 0 or > 59
            || frames is < 0 or > 74)
        {
            throw new DiscImageException($"Invalid CUE MSF value '{token}'.");
        }

        return (minutes * 60 * 75) + (seconds * 75) + frames;
    }

    private static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var index = 0;
        while (index < line.Length)
        {
            while (index < line.Length && char.IsWhiteSpace(line[index]))
            {
                index++;
            }

            if (index >= line.Length)
            {
                break;
            }

            if (line[index] == '"')
            {
                var start = ++index;
                while (index < line.Length && line[index] != '"')
                {
                    index++;
                }

                tokens.Add(line[start..index]);
                if (index < line.Length)
                {
                    index++;
                }
            }
            else
            {
                var start = index;
                while (index < line.Length && !char.IsWhiteSpace(line[index]))
                {
                    index++;
                }

                tokens.Add(line[start..index]);
            }
        }

        return tokens;
    }

    private sealed record TrackType(int Kind, int DataSize);

    private sealed class PendingTrack
    {
        internal PendingTrack(int number, string fileName, TrackType type)
        {
            Number = number;
            FileName = fileName;
            Type = type;
        }

        internal int Number { get; }

        internal string FileName { get; }

        internal TrackType Type { get; }

        internal int Index00 { get; set; } = -1;

        internal int Index01 { get; set; } = -1;
    }
}
