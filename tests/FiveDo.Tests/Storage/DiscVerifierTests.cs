using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Tests.Storage;

public sealed class DiscVerifierTests
{
    [Fact]
    public void Verify_ReadsEverySectorAndProducesAStableHash()
    {
        using var disc = new FakeDisc(8, failAt: -1);

        DiscVerificationResult first = DiscVerifier.Verify(disc);
        DiscVerificationResult second = DiscVerifier.Verify(disc);

        Assert.True(first.Ok);
        Assert.Equal(8, first.Sectors);
        Assert.Equal(8, first.SectorsVerified);
        Assert.Equal(8 * 2048, first.BytesVerified);
        Assert.Empty(first.Errors);
        Assert.Equal(first.Hash, second.Hash);
    }

    [Fact]
    public void Verify_ReportsReadErrorsAndContinues()
    {
        using var disc = new FakeDisc(8, failAt: 3);

        DiscVerificationResult result = DiscVerifier.Verify(disc, maxErrors: 4);

        Assert.False(result.Ok);
        Assert.Equal(8, result.Sectors);
        Assert.Equal(7, result.SectorsVerified);
        Assert.Single(result.Errors);
        Assert.Contains("LBA 3", result.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_UsesNativeTrackSectorsWhenAvailable()
    {
        using var disc = new FakeTrackedDisc(3, 2);

        DiscVerificationResult result = DiscVerifier.Verify(disc);

        Assert.True(result.Ok);
        Assert.Equal(5, result.Sectors);
        Assert.Equal(5, result.SectorsVerified);
        Assert.Equal(2, result.Tracks);
        Assert.Equal((3 * 2048) + (2 * 2352), result.BytesVerified);
    }

    private sealed class FakeDisc : IDiscSource
    {
        private readonly int _failAt;

        internal FakeDisc(int sectorCount, int failAt)
        {
            SectorCount = sectorCount;
            _failAt = failAt;
        }

        public int SectorCount { get; }

        public int SectorSize => 2048;

        public void ReadSector(int lba, Span<byte> destination)
        {
            if (lba == _failAt)
            {
                throw new InvalidDataException("simulated read failure");
            }

            destination[..2048].Fill((byte)lba);
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeTrackedDisc : ITrackedDiscSource
    {
        private readonly int _dataSectors;
        private readonly int _audioSectors;

        internal FakeTrackedDisc(int dataSectors, int audioSectors)
        {
            _dataSectors = dataSectors;
            _audioSectors = audioSectors;
            Tracks =
            [
                new DiscTrack(1, DiscTrackKind.Data, 0, dataSectors, 2048),
                new DiscTrack(2, DiscTrackKind.Audio, dataSectors, audioSectors, 2352),
            ];
        }

        public int SectorCount => _dataSectors + _audioSectors;

        public int SectorSize => 2048;

        public IReadOnlyList<DiscTrack> Tracks { get; }

        public int TotalSectorCount => SectorCount;

        public int ReadTrackSector(int lba, Span<byte> destination)
        {
            DiscTrack track = lba < _dataSectors ? Tracks[0] : Tracks[1];
            int offset = lba - track.StartLba;
            destination[..track.SectorSize].Fill((byte)offset);
            return track.SectorSize;
        }

        public void ReadSector(int lba, Span<byte> destination)
        {
            destination[..2048].Fill((byte)lba);
        }

        public void Dispose()
        {
        }
    }
}
