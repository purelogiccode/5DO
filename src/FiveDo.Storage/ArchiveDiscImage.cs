using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FiveDo.Core;
using SharpCompress.Archives;

namespace FiveDo.Storage;

/// <summary>
/// Opens ISO, BIN, and CUE+BIN disc images stored directly inside ZIP or 7z archives.
/// </summary>
/// <remarks>
/// Archive entries are decompressed once into memory and presented through the same
/// <see cref="IDiscSource"/> API as an extracted image. CHD, CDI, and NRG files inside an
/// archive are rejected with extraction guidance because those containers are already
/// compressed or need their own random-access layout. Encrypted and multi-volume
/// archives are rejected rather than partially read.
/// </remarks>
public static class ArchiveDiscImage
{
    /// <summary>Opens a ZIP or 7z archive and selects its disc image.</summary>
    /// <param name="archivePath">Path to the archive.</param>
    /// <returns>The disc source. The caller owns and must dispose it.</returns>
    /// <exception cref="DiscImageException">The archive has no usable image or cannot be read.</exception>
    public static IDiscSource Open(string archivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        if (!File.Exists(archivePath))
        {
            throw new DiscImageException($"Disc archive '{archivePath}' does not exist.");
        }

        var files = new ArchiveFileSystem(archivePath);
        try
        {
            IDiscSource source = files.OpenDisc();
            return source is ITrackedDiscSource tracked
                ? new ArchiveTrackedDiscSource(source, tracked, files)
                : new ArchiveDiscSource(source, files);
        }
        catch
        {
            files.Dispose();
            throw;
        }
    }

    private sealed class ArchiveFileSystem : IDisposable
    {
        private const long MaxEntryBytes = 1_500_000_000;
        private readonly IArchive _archive;
        private readonly Dictionary<string, IArchiveEntry> _entries =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, byte[]> _cache =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly string _archivePath;

        internal ArchiveFileSystem(string archivePath)
        {
            _archivePath = archivePath;
            try
            {
                _archive = ArchiveFactory.Open(archivePath);
            }
            catch (Exception exception) when (exception is IOException
                or InvalidOperationException
                or NotSupportedException
                or CryptographicException
                or SharpCompress.Common.SharpCompressException)
            {
                throw new DiscImageException(
                    $"Archive '{archivePath}' could not be opened: {exception.Message}",
                    exception);
            }

            SharpCompress.Common.ArchiveType type = _archive.Type;
            if (type is not (SharpCompress.Common.ArchiveType.Zip or SharpCompress.Common.ArchiveType.SevenZip))
            {
                _archive.Dispose();
                throw new DiscImageException(
                    $"Archive '{archivePath}' is {type}; only ZIP and 7z are supported.");
            }

            foreach (IArchiveEntry entry in _archive.Entries)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }

                var key = NormalizeEntry(entry.Key);
                if (key.Length == 0)
                {
                    continue;
                }

                _entries[key] = entry;
            }
        }

        internal IDiscSource OpenDisc()
        {
            List<string> cues = _entries.Keys
                .Where(static key => key.EndsWith(".cue", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (cues.Count > 1)
            {
                throw new DiscImageException(
                    $"Archive '{_archivePath}' contains {cues.Count} CUE sheets "
                    + $"({string.Join(", ", cues.Take(5))}); open a single-disc archive or extract the set.");
            }

            if (cues.Count == 1)
            {
                return OpenCue(cues[0]);
            }

            List<string> images = _entries.Keys
                .Where(static key => key.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)
                    || key.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (images.Count > 1)
            {
                throw new DiscImageException(
                    $"Archive '{_archivePath}' contains multiple images "
                    + $"({string.Join(", ", images.Take(5))}); add a .cue or extract the set.");
            }

            if (images.Count == 1)
            {
                return OpenImage(images[0]);
            }

            List<string> compressed = _entries.Keys
                .Where(static key => key.EndsWith(".chd", StringComparison.OrdinalIgnoreCase)
                    || key.EndsWith(".cdi", StringComparison.OrdinalIgnoreCase)
                    || key.EndsWith(".nrg", StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();
            if (compressed.Count > 0)
            {
                throw new DiscImageException(
                    $"Archive '{_archivePath}' contains {string.Join(", ", compressed)}. "
                    + "CHD, CDI, and NRG must be extracted before opening; archive support "
                    + "covers ISO, BIN, and CUE+BIN.");
            }

            throw new DiscImageException(
                $"Archive '{_archivePath}' contains no .cue, .iso, or .bin disc image. "
                + $"Entries: {string.Join(", ", _entries.Keys.Take(8))}.");
        }

        private CueDiscImage OpenCue(string cueEntry)
        {
            string text = Encoding.UTF8.GetString(ReadEntryBytes(cueEntry));
            var toc = VirtualCueParser.Parse(
                text,
                cueEntry,
                name => ReadEntryBytes(name).Length);
            return CueDiscImage.OpenVirtual(
                toc,
                name => new MemoryStream(ReadEntryBytes(name), writable: false),
                $"{_archivePath}:{cueEntry}");
        }

        private IDiscSource OpenImage(string entry)
        {
            byte[] data = ReadEntryBytes(entry);
            if (entry.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
            {
                return OpenIso(data, entry);
            }

            if (HasCdSyncHeader(data))
            {
                int mode = data.Length >= 16 && data[15] == 2 ? 2 : 1;
                return new BinImage(
                    new MemoryStream(data, writable: false),
                    entry,
                    payloadOffset: mode == 2 ? 24 : 16);
            }

            if (data.Length % IsoImage.SectorBytes == 0)
            {
                return new IsoImage(new MemoryStream(data, writable: false), entry);
            }

            throw new DiscImageException(
                $"Archive entry '{entry}' has neither a CD sync header nor 2048-byte sectors.");
        }

        private static IDiscSource OpenIso(byte[] data, string entry)
        {
            if (data.Length % BinImage.RawSectorBytes == 0 && HasCdSyncHeader(data))
            {
                int mode = data.Length >= 16 && data[15] == 2 ? 2 : 1;
                return new BinImage(
                    new MemoryStream(data, writable: false),
                    entry,
                    payloadOffset: mode == 2 ? 24 : 16);
            }

            if (data.Length % IsoImage.SectorBytes == 0)
            {
                return new IsoImage(new MemoryStream(data, writable: false), entry);
            }

            throw new DiscImageException(
                $"Archive entry '{entry}' is {data.Length:N0} bytes, which is not a whole "
                + $"number of 2048-byte sectors (remainder {data.Length % IsoImage.SectorBytes:N0}).");
        }

        private IArchiveEntry GetEntry(string name)
        {
            if (!_entries.TryGetValue(name, out IArchiveEntry? entry))
            {
                throw new DiscImageException(
                    $"Archive '{_archivePath}' has no entry '{name}'.");
            }

            return entry;
        }

        private byte[] ReadEntryBytes(string name)
        {
            if (_cache.TryGetValue(name, out byte[]? cached))
            {
                return cached;
            }

            IArchiveEntry entry = GetEntry(name);
            if (entry.IsEncrypted)
            {
                throw new DiscImageException(
                    $"Archive entry '{name}' is encrypted; decrypt or extract the archive.");
            }

            if (entry.Size > MaxEntryBytes)
            {
                throw new DiscImageException(
                    $"Archive entry '{name}' is {entry.Size:N0} bytes; the in-memory limit is "
                    + $"{MaxEntryBytes:N0}. Extract this image before opening it.");
            }

            try
            {
                using Stream stream = entry.OpenEntryStream();
                using var buffer = entry.Size is > 0 and <= int.MaxValue
                    ? new MemoryStream((int)entry.Size)
                    : new MemoryStream();
                stream.CopyTo(buffer);
                byte[] bytes = buffer.ToArray();
                _cache[name] = bytes;
                return bytes;
            }
            catch (Exception exception) when (exception is IOException
                or InvalidOperationException
                or NotSupportedException
                or CryptographicException
                or SharpCompress.Common.SharpCompressException)
            {
                throw new DiscImageException(
                    $"Archive entry '{name}' could not be read: {exception.Message}",
                    exception);
            }
        }

        private static string NormalizeEntry(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            var normalized = key.Replace('\\', '/').Trim();
            while (normalized.StartsWith('/'))
            {
                normalized = normalized[1..];
            }

            return normalized;
        }

        private static bool HasCdSyncHeader(byte[] data)
        {
            if (data.Length < 12)
            {
                return false;
            }

            return data[0] == 0x00
                && data[1] == 0xFF
                && data[2] == 0xFF
                && data[3] == 0xFF
                && data[4] == 0xFF
                && data[5] == 0xFF
                && data[6] == 0xFF
                && data[7] == 0xFF
                && data[8] == 0xFF
                && data[9] == 0xFF
                && data[10] == 0xFF
                && data[11] == 0x00;
        }

        public void Dispose()
        {
            _archive.Dispose();
            _cache.Clear();
            _entries.Clear();
        }
    }

    private sealed class ArchiveDiscSource : IDiscSource
    {
        private readonly IDiscSource _inner;
        private readonly ArchiveFileSystem _files;

        internal ArchiveDiscSource(IDiscSource inner, ArchiveFileSystem files)
        {
            _inner = inner;
            _files = files;
        }

        public int SectorCount => _inner.SectorCount;

        public int SectorSize => _inner.SectorSize;

        public void ReadSector(int lba, Span<byte> destination)
        {
            _inner.ReadSector(lba, destination);
        }

        public void Dispose()
        {
            _inner.Dispose();
            _files.Dispose();
        }
    }

    private sealed class ArchiveTrackedDiscSource : ITrackedDiscSource
    {
        private readonly IDiscSource _inner;
        private readonly ITrackedDiscSource _tracked;
        private readonly ArchiveFileSystem _files;

        internal ArchiveTrackedDiscSource(
            IDiscSource inner,
            ITrackedDiscSource tracked,
            ArchiveFileSystem files)
        {
            _inner = inner;
            _tracked = tracked;
            _files = files;
        }

        public int SectorCount => _inner.SectorCount;

        public int SectorSize => _inner.SectorSize;

        public IReadOnlyList<DiscTrack> Tracks => _tracked.Tracks;

        public int TotalSectorCount => _tracked.TotalSectorCount;

        public void ReadSector(int lba, Span<byte> destination)
        {
            _inner.ReadSector(lba, destination);
        }

        public int ReadTrackSector(int lba, Span<byte> destination)
        {
            return _tracked.ReadTrackSector(lba, destination);
        }

        public void Dispose()
        {
            _inner.Dispose();
            _files.Dispose();
        }
    }
}
