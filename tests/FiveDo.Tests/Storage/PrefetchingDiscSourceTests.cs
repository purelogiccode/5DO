using System;
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
            SpinWait.SpinUntil(() => source.CachedSectorCount >= 3, TimeSpan.FromSeconds(2)),
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
            SpinWait.SpinUntil(() => source.CachedSectorCount > 0, TimeSpan.FromSeconds(2)),
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
            SpinWait.SpinUntil(() => source.CachedSectorCount >= 4, TimeSpan.FromSeconds(2)),
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
            using var source = DiscImageFactory.Open(path, prefetch: true, prefetchAhead: 2, prefetchCapacity: 4);
            var destination = new byte[2048];
            source.ReadSector(0, destination);
            Assert.Equal(4, source.SectorCount);
        }
        finally
        {
            File.Delete(path);
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
