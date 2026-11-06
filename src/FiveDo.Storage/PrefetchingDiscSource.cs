using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// Adds a bounded, host-side read-ahead cache in front of an <see cref="IDiscSource"/>.
/// </summary>
/// <remarks>
/// <para>
/// The emulation core remains synchronous and deterministic: a cache miss performs the same
/// read as the wrapped source, and prefetched sectors never change emulated state. The worker
/// is a host optimization for CHD decompression and slow storage on Tier B hardware.
/// </para>
/// <para>
/// Inner reads are serialized because container readers such as CHDSharp are not documented
/// as thread-safe. Cache hits copy from a private sector buffer and do not touch the inner
/// reader.
/// </para>
/// </remarks>
public sealed class PrefetchingDiscSource : ITrackedDiscSource
{
    private readonly IDiscSource _inner;
    private readonly int _capacity;
    private readonly Lock _gate = new();
    private readonly Lock _innerGate = new();
    private readonly Dictionary<int, byte[]> _cache = new();
    private readonly Queue<int> _cacheOrder = new();
    private readonly Dictionary<int, TrackCacheEntry> _trackCache = new();
    private readonly Queue<int> _trackOrder = new();
    private readonly Stack<byte[]> _bufferPool = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _worker;
    private volatile bool _stop;
    private bool _disposed;
    private int _next;
    private byte[]? _workerSectorBuffer;
    private byte[]? _workerTrackBuffer;

    /// <summary>Initializes a new prefetching disc source.</summary>
    /// <param name="inner">Wrapped source. The wrapper owns and disposes it.</param>
    /// <param name="ahead">Maximum sectors to read ahead after each access.</param>
    /// <param name="capacity">Maximum cached sectors.</param>
    public PrefetchingDiscSource(IDiscSource inner, int ahead = 32, int capacity = 128)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfLessThan(ahead, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _inner = inner;
        Ahead = ahead;
        _capacity = capacity;
        _worker = new Thread(Run)
        {
            IsBackground = true,
            Name = "3DO disc prefetch",
        };
        _worker.Start();
    }

    /// <summary>Gets the number of sectors to read ahead.</summary>
    public int Ahead { get; }

    /// <summary>Gets the current number of cached sectors.</summary>
    public int CachedSectorCount
    {
        get
        {
            lock (_gate)
            {
                return _cache.Count;
            }
        }
    }

    /// <summary>Gets the current number of cached native track sectors.</summary>
    /// <remarks>
    /// Native sectors cover CD-DA and raw reads, which bypass the 2048-byte user-data
    /// cache. The worker fills this cache while audio playback consumes a sector every
    /// 588 stereo frames.
    /// </remarks>
    public int CachedTrackSectorCount
    {
        get
        {
            lock (_gate)
            {
                return _trackCache.Count;
            }
        }
    }

    /// <inheritdoc />
    public int SectorCount
    {
        get
        {
            lock (_innerGate)
            {
                return _inner.SectorCount;
            }
        }
    }

    /// <inheritdoc />
    public int SectorSize
    {
        get
        {
            lock (_innerGate)
            {
                return _inner.SectorSize;
            }
        }
    }

    /// <summary>Gets the wrapped track table, or an empty list for a single-track image.</summary>
    public IReadOnlyList<DiscTrack> Tracks
    {
        get
        {
            lock (_innerGate)
            {
                return _inner is ITrackedDiscSource tracked
                    ? tracked.Tracks
                    : [new DiscTrack(1, DiscTrackKind.Data, 0, _inner.SectorCount, _inner.SectorSize)];
            }
        }
    }

    /// <summary>Gets the wrapped total sector count.</summary>
    public int TotalSectorCount
    {
        get
        {
            lock (_innerGate)
            {
                return _inner is ITrackedDiscSource tracked ? tracked.TotalSectorCount : _inner.SectorCount;
            }
        }
    }

    /// <summary>Reads one native track sector, bypassing the 2048-byte cache.</summary>
    /// <param name="lba">Logical block address.</param>
    /// <param name="destination">Destination span.</param>
    /// <returns>The number of bytes written.</returns>
    public int ReadTrackSector(int lba, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            if (_trackCache.TryGetValue(lba, out TrackCacheEntry cached))
            {
                int copy = Math.Min(cached.Length, destination.Length);
                cached.Buffer.AsSpan(0, copy).CopyTo(destination);
                Advance(lba);
                return copy;
            }
        }

        int read;
        lock (_innerGate)
        {
            if (_inner is ITrackedDiscSource tracked)
            {
                read = tracked.ReadTrackSector(lba, destination);
            }
            else
            {
                _inner.ReadSector(lba, destination);
                read = _inner.SectorSize;
            }
        }

        lock (_gate)
        {
            if (read > 0 && read <= destination.Length)
            {
                StoreTrack(lba, destination[..read]);
            }

            Advance(lba);
        }

        return read;
    }

    /// <inheritdoc />
    public void ReadSector(int lba, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(lba);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(lba, SectorCount);
        if (destination.Length < SectorSize)
        {
            throw new ArgumentException("Destination is smaller than one sector.", nameof(destination));
        }

        lock (_gate)
        {
            if (_cache.TryGetValue(lba, out var cached))
            {
                cached.AsSpan(0, SectorSize).CopyTo(destination);
                Advance(lba);
                return;
            }
        }

        lock (_innerGate)
        {
            _inner.ReadSector(lba, destination);
        }

        lock (_gate)
        {
            Advance(lba);
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
        _stop = true;
        _wake.Set();
        _ = _worker.Join(TimeSpan.FromSeconds(2));
        _wake.Dispose();
        _inner.Dispose();
        lock (_gate)
        {
            _cache.Clear();
            _trackCache.Clear();
            _bufferPool.Clear();
        }
    }

    private void Advance(int lba)
    {
        if (lba >= _next)
        {
            _next = lba + 1;
        }

        _wake.Set();
    }

    private void StoreTrack(int lba, ReadOnlySpan<byte> data)
    {
        if (_trackCache.ContainsKey(lba))
        {
            return;
        }

        if (_trackCache.Count >= _capacity)
        {
            var oldest = _trackOrder.Dequeue();
            ReturnBuffer(_trackCache[oldest].Buffer);
            _trackCache.Remove(oldest);
        }

        byte[] buffer = RentBuffer(data.Length);
        data.CopyTo(buffer);
        _trackCache[lba] = new TrackCacheEntry(buffer, data.Length);
        _trackOrder.Enqueue(lba);
    }

    private void StoreData(int lba, ReadOnlySpan<byte> data)
    {
        if (_cache.ContainsKey(lba))
        {
            return;
        }

        if (_cache.Count >= _capacity)
        {
            var oldest = _cacheOrder.Dequeue();
            ReturnBuffer(_cache[oldest]);
            _cache.Remove(oldest);
        }

        byte[] buffer = RentBuffer(data.Length);
        data.CopyTo(buffer);
        _cache[lba] = buffer;
        _cacheOrder.Enqueue(lba);
    }

    private readonly record struct TrackCacheEntry(byte[] Buffer, int Length);

    private byte[] RentBuffer(int length)
    {
        while (_bufferPool.Count > 0)
        {
            byte[] buffer = _bufferPool.Pop();
            if (buffer.Length >= length)
            {
                return buffer;
            }
        }

        return new byte[length];
    }

    private void ReturnBuffer(byte[] buffer)
    {
        if (buffer.Length <= 2352)
        {
            _bufferPool.Push(buffer);
        }
    }

    private DiscTrack? TryFindTrack(int lba)
    {
        lock (_innerGate)
        {
            return _inner is ITrackedDiscSource tracked ? tracked.FindTrack(lba) : null;
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A prefetch failure is non-fatal; the foreground read reports the error if the sector is needed.")]
    private void Run()
    {
        while (!_stop)
        {
            _wake.WaitOne();
            if (_stop)
            {
                break;
            }

            for (var i = 0; i < Ahead && !_stop; i++)
            {
                int lba;
                lock (_gate)
                {
                    if (_next >= SectorCount)
                    {
                        break;
                    }

                    lba = _next++;
                    if (_cache.ContainsKey(lba) || _trackCache.ContainsKey(lba))
                    {
                        continue;
                    }
                }

                if (TryFindTrack(lba) is { IsAudio: true } audioTrack)
                {
                    int trackSize = audioTrack.SectorSize;
                    byte[] trackBuffer = _workerTrackBuffer ??= new byte[trackSize];
                    if (trackBuffer.Length < trackSize)
                    {
                        trackBuffer = new byte[trackSize];
                        _workerTrackBuffer = trackBuffer;
                    }

                    try
                    {
                        int read;
                        lock (_innerGate)
                        {
                            read = ((ITrackedDiscSource)_inner).ReadTrackSector(lba, trackBuffer);
                        }

                        if (read <= 0)
                        {
                            continue;
                        }

                        lock (_gate)
                        {
                            StoreTrack(lba, trackBuffer.AsSpan(0, Math.Min(read, trackBuffer.Length)));
                        }
                    }
                    catch (Exception exception)
                    {
                        Serilog.Log.Debug(exception, "Audio prefetch read failed for LBA {Lba}.", lba);
                    }

                    continue;
                }

                int sectorSize = SectorSize;
                byte[] buffer = _workerSectorBuffer ??= new byte[sectorSize];
                if (buffer.Length < sectorSize)
                {
                    buffer = new byte[sectorSize];
                    _workerSectorBuffer = buffer;
                }

                try
                {
                    lock (_innerGate)
                    {
                        _inner.ReadSector(lba, buffer);
                    }
                }
                catch (Exception exception)
                {
                    Serilog.Log.Debug(exception, "Prefetch read failed for LBA {Lba}.", lba);
                    continue;
                }

                lock (_gate)
                {
                    StoreData(lba, buffer);
                }
            }
        }
    }
}
