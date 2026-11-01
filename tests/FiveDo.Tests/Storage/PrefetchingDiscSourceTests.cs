using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Tests.Storage;

public sealed class PrefetchingDiscSourceTests
{
    [Fact]
    public void ReadSector_ReturnsDataAndCachesSequentialSectors()
    {
        using var inner = new FakeDiscSource(64);
        using var source = new PrefetchingDiscSource(inner, ahead: 4, capacity: 8);

        var destination = new byte[2048];
        source.ReadSector(5, destination);
        Assert.Equal(5, destination[0]);

        Assert.True(
            WaitForCache(source, minimum: 3, TimeSpan.FromSeconds(2)),
            "The prefetch worker did not cache the next sectors.");
    }

    [Fact]
    public void ReadSector_FromCacheReturnsTheSameData()
    {
        using var inner = new FakeDiscSource(64);
        using var source = new PrefetchingDiscSource(inner, ahead: 4, capacity: 8);

        var destination = new byte[2048];
        source.ReadSector(5, destination);
        Assert.True(
            WaitForCache(source, minimum: 1, TimeSpan.FromSeconds(2)),
            "The prefetch worker did not cache a sector.");

        source.ReadSector(5, destination);
        Assert.Equal(5, destination[0]);
    }

    [Fact]
    public void Cache_IsBoundedByCapacity()
    {
        using var inner = new FakeDiscSource(256);
        using var source = new PrefetchingDiscSource(inner, ahead: 8, capacity: 4);

        var destination = new byte[2048];
        source.ReadSector(0, destination);

        Assert.True(
            WaitForCache(source, minimum: 4, TimeSpan.FromSeconds(2)),
            "The prefetch worker did not fill the cache.");

        Thread.Sleep(100);
        Assert.InRange(source.CachedSectorCount, 1, 4);
    }

    [Fact]
    public void Dispose_StopsTheWorkerAndDisposesTheInnerSource()
    {
        var inner = new FakeDiscSource(8);
        var source = new PrefetchingDiscSource(inner);
        source.Dispose();

        Assert.True(inner.Disposed);
        Assert.Throws<ObjectDisposedException>(() => source.ReadSector(0, new byte[2048]));
    }

    [Fact]
    public void Factory_CanWrapAnIsoInThePrefetchCache()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-prefetch-{Guid.NewGuid():N}.iso");
        try
        {
            File.WriteAllBytes(path, new byte[2048 * 4]);
            using IDiscSource source = DiscImageFactory.Open(path, prefetch: true, prefetchAhead: 2, prefetchCapacity: 4);
            var destination = new byte[2048];
            source.ReadSector(0, destination);
            Assert.Equal(4, source.SectorCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadTrackSector_CachesAndPrefetchesAudioSectors()
    {
        using var inner = new FakeTrackedDiscSource(64);
        using var source = new PrefetchingDiscSource(inner, ahead: 4, capacity: 8);

        var destination = new byte[2352];
        Assert.Equal(2352, source.ReadTrackSector(5, destination));
        Assert.Equal(5, destination[0]);
        Assert.Equal((byte)(5 ^ 0xff), destination[1]);

        Assert.True(
            WaitForTrackCache(source, minimum: 2, TimeSpan.FromSeconds(2)),
            "The prefetch worker did not cache the following audio sectors.");

        int readsBefore = inner.ReadCount(6);
        int secondRead = source.ReadTrackSector(6, destination);

        Assert.Equal(2352, secondRead);
        Assert.Equal(6, destination[0]);
        Assert.Equal(readsBefore, inner.ReadCount(6));
    }

    [Fact]
    public void ReadTrackSector_StillReturnsCorrectDataWhenTheCacheIsFull()
    {
        using var inner = new FakeTrackedDiscSource(256);
        using var source = new PrefetchingDiscSource(inner, ahead: 16, capacity: 2);

        var destination = new byte[2352];
        Assert.Equal(2352, source.ReadTrackSector(0, destination));
        Assert.True(
            WaitForTrackCache(source, minimum: 2, TimeSpan.FromSeconds(2)),
            "The prefetch worker did not fill the track cache.");

        Assert.Equal(2352, source.ReadTrackSector(40, destination));
        Assert.Equal(40, destination[0]);
        Assert.InRange(source.CachedTrackSectorCount, 1, 2);
    }

    private static bool WaitForTrackCache(PrefetchingDiscSource source, int minimum, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (source.CachedTrackSectorCount >= minimum)
            {
                return true;
            }

            Thread.Sleep(5);
        }

        return source.CachedTrackSectorCount >= minimum;
    }

    private static bool WaitForCache(PrefetchingDiscSource source, int minimum, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (source.CachedSectorCount >= minimum)
            {
                return true;
            }

            Thread.Sleep(5);
        }

        return source.CachedSectorCount >= minimum;
    }

    private sealed class FakeTrackedDiscSource : ITrackedDiscSource
    {
        private readonly int[] _reads;

        public FakeTrackedDiscSource(int sectorCount)
        {
            SectorCount = sectorCount;
            TotalSectorCount = sectorCount;
            _reads = new int[sectorCount];
            Tracks = [new DiscTrack(1, DiscTrackKind.Audio, 0, sectorCount, 2352)];
        }

        public int SectorCount { get; }

        public int SectorSize => 2048;

        public IReadOnlyList<DiscTrack> Tracks { get; }

        public int TotalSectorCount { get; }

        public int ReadCount(int lba) => Volatile.Read(ref _reads[lba]);

        public void ReadSector(int lba, Span<byte> destination)
        {
            destination[..SectorSize].Fill((byte)lba);
        }

        public int ReadTrackSector(int lba, Span<byte> destination)
        {
            Interlocked.Increment(ref _reads[lba]);
            destination[0] = (byte)lba;
            destination[1] = (byte)(lba ^ 0xff);
            destination[..2352].Slice(2).Fill((byte)(lba + 1));
            return 2352;
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeDiscSource : IDiscSource
    {
        public FakeDiscSource(int sectorCount)
        {
            SectorCount = sectorCount;
        }

        public int SectorCount { get; }

        public int SectorSize => 2048;

        public bool Disposed { get; private set; }

        public void ReadSector(int lba, Span<byte> destination)
        {
            destination[..SectorSize].Fill((byte)lba);
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
