using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Host;

/// <summary>
/// Loads and persists the 32 KB NVRAM image with debounced atomic writes.
/// </summary>
/// <remarks>
/// The emulation core never touches the filesystem. The host calls <see cref="MarkChanged"/>
/// with a snapshot of the machine's NVRAM and <see cref="FlushIfDue"/> from its frame loop;
/// writes happen at most once per <see cref="FlushInterval"/> and use a temporary file plus
/// an atomic replace. <see cref="Flush"/> forces a write on pause/stop.
/// </remarks>
public sealed class NvramStore : IDisposable
{
    /// <summary>Default debounce interval.</summary>
    public static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(250);

    private readonly string _path;
    private readonly IReadOnlyList<string> _fallbackPaths;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private readonly byte[] _pending = new byte[EmulatorConstants.NvramSize];

    private bool _dirty;
    private DateTimeOffset _lastChange;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="NvramStore"/> class.</summary>
    /// <param name="path">NVRAM file path.</param>
    /// <param name="timeProvider">Optional clock, for tests.</param>
    public NvramStore(string path, TimeProvider? timeProvider = null)
        : this(path, [], timeProvider)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NvramStore"/> class.</summary>
    /// <param name="path">Primary NVRAM file path.</param>
    /// <param name="fallbackPaths">
    /// Read-only fallback images used when the primary file does not exist yet. This
    /// supports migration from a shared image to a per-game image without copying data.
    /// </param>
    /// <param name="timeProvider">Optional clock, for tests.</param>
    public NvramStore(string path, IEnumerable<string> fallbackPaths, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _fallbackPaths = fallbackPaths
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Where(candidate => !string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets the NVRAM file path.</summary>
    public string Path => _path;

    /// <summary>Gets the read-only fallback paths used for migration.</summary>
    public IReadOnlyList<string> FallbackPaths => _fallbackPaths;

    /// <summary>Gets a value indicating whether the primary NVRAM file exists.</summary>
    public bool Exists => File.Exists(_path);

    /// <summary>Loads the NVRAM image, or returns a fresh image when no file exists.</summary>
    /// <returns>The loaded or fresh image.</returns>
    public NvramImage LoadOrCreate()
    {
        if (File.Exists(_path))
        {
            try
            {
                return NvramImage.Load(_path);
            }
            catch (InvalidDataException exception)
            {
                // Preserve the bad image for diagnosis and start from a fresh volume.
                // Do not silently fall back to an older image after primary corruption.
                Serilog.Log.Warning(exception, "NVRAM image {Path} is invalid; starting a fresh volume.", _path);
                File.Move(_path, _path + ".bad", overwrite: true);
                return NvramImage.CreateFresh();
            }
        }

        foreach (var fallback in _fallbackPaths)
        {
            if (!File.Exists(fallback))
            {
                continue;
            }

            try
            {
                NvramImage image = NvramImage.Load(fallback);
                Serilog.Log.Information(
                    "NVRAM image {Path} is missing; using {Fallback} until the next save.",
                    _path,
                    fallback);
                return image;
            }
            catch (InvalidDataException exception)
            {
                Serilog.Log.Warning(
                    exception,
                    "Fallback NVRAM image {Path} is invalid; continuing with the next candidate.",
                    fallback);
            }
        }

        return NvramImage.CreateFresh();
    }

    /// <summary>Records a changed NVRAM snapshot for a later debounced write.</summary>
    /// <param name="nvram">Exactly 32 KB of NVRAM data.</param>
    public void MarkChanged(ReadOnlySpan<byte> nvram)
    {
        if (nvram.Length != EmulatorConstants.NvramSize)
        {
            throw new ArgumentException($"NVRAM must be exactly {EmulatorConstants.NvramSize} bytes.", nameof(nvram));
        }

        lock (_gate)
        {
            nvram.CopyTo(_pending);
            _dirty = true;
            _lastChange = _timeProvider.GetUtcNow();
        }
    }

    /// <summary>Writes a complete NVRAM image immediately.</summary>
    /// <param name="nvram">Exactly 32 KB of NVRAM data.</param>
    public void Save(ReadOnlySpan<byte> nvram)
    {
        MarkChanged(nvram);
        Flush();
    }

    /// <summary>Writes a fresh NVRAM image immediately, clearing the current save data.</summary>
    public void Reset()
    {
        Save(NvramImage.CreateFresh().Data.Span);
    }

    /// <summary>Writes the pending snapshot when the debounce interval has elapsed.</summary>
    /// <returns><see langword="true"/> when a write happened.</returns>
    public bool FlushIfDue()
    {
        lock (_gate)
        {
            if (!_dirty || _timeProvider.GetUtcNow() - _lastChange < FlushInterval)
            {
                return false;
            }

            WritePending();
            return true;
        }
    }

    /// <summary>Writes the pending snapshot immediately, if any.</summary>
    /// <returns><see langword="true"/> when a write happened.</returns>
    public bool Flush()
    {
        lock (_gate)
        {
            if (!_dirty)
            {
                return false;
            }

            WritePending();
            return true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Flush();
    }

    private void WritePending()
    {
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(_path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = _path + ".tmp";
        File.WriteAllBytes(temporary, _pending);
        File.Move(temporary, _path, overwrite: true);
        _dirty = false;
    }
}
