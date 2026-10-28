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
public sealed class PrefetchingDiscSource : IDiscSource
{
    private readonly IDiscSource _inner;
    private readonly int _capacity;
    private readonly Lock _gate = new();
    private readonly Lock _innerGate = new();
    private readonly Dictionary<int, byte[]> _cache = new();
    private readonly Queue<int> _cacheOrder = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _worker;
    private volatile bool _stop;
    private bool _disposed;
    private int _next;

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
    }

    private void Advance(int lba)
    {
        if (lba >= _next)
        {
            _next = lba + 1;
        }

        _wake.Set();
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
                    if (_cache.ContainsKey(lba))
                    {
                        continue;
                    }
                }

                var buffer = new byte[SectorSize];
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
                    if (_cache.Count >= _capacity)
                    {
                        var oldest = _cacheOrder.Dequeue();
                        _cache.Remove(oldest);
                    }

                    _cache[lba] = buffer;
                    _cacheOrder.Enqueue(lba);
                }
            }
        }
    }
}
