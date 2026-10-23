using System;
using System.IO;
using ThreeDo.Core;
using ThreeDo.Storage;

namespace ThreeDo.Host;

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
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private readonly byte[] _pending = new byte[EmulatorConstants.NvramSize];

    private bool _dirty;
    private DateTimeOffset _lastChange;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="NvramStore"/> class.</summary>
    /// <param name="path">NVRAM file path.</param>
    /// <param name="timeProvider">Optional clock, for tests.</param>
    public NvramStore(string path, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets the NVRAM file path.</summary>
    public string Path => _path;

    /// <summary>Loads the NVRAM image, or returns a fresh image when no file exists.</summary>
    /// <returns>The loaded or fresh image.</returns>
    public NvramImage LoadOrCreate()
    {
        if (!File.Exists(_path))
        {
            return NvramImage.CreateFresh();
        }

        try
        {
            return NvramImage.Load(_path);
        }
        catch (InvalidDataException)
        {
            // Preserve the bad image for diagnosis and start from a fresh volume.
            File.Move(_path, _path + ".bad", overwrite: true);
            return NvramImage.CreateFresh();
        }
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
