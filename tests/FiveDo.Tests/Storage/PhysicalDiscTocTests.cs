using System;
using System.Collections.Generic;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Tests.Storage;

public sealed class PhysicalDiscTocTests
{
    [Fact]
    public void ParseWindowsTocBuildsDataAndAudioTracks()
    {
        byte[] toc = BuildWindowsToc(
            (1, 0x04, 0, 2, 0),
            (2, 0x00, 0, 4, 0),
            (3, 0x00, 0, 6, 0),
            (0xAA, 0x00, 0, 8, 0));

        IReadOnlyList<DiscTrack> tracks = PhysicalDiscToc.ParseWindowsToc(toc);

        Assert.Equal(3, tracks.Count);
        AssertTrack(tracks[0], number: 1, DiscTrackKind.Data, startLba: 0, sectors: 150, sectorSize: 2048);
        AssertTrack(tracks[1], number: 2, DiscTrackKind.Audio, startLba: 150, sectors: 150, sectorSize: 2352);
        AssertTrack(tracks[2], number: 3, DiscTrackKind.Audio, startLba: 300, sectors: 150, sectorSize: 2352);
    }

    [Fact]
    public void ParseWindowsTocUsesTheFallbackLeadOutWhenTheDriveOmitsIt()
    {
        byte[] toc = BuildWindowsToc((1, 0x04, 0, 2, 0));

        IReadOnlyList<DiscTrack> tracks = PhysicalDiscToc.ParseWindowsToc(toc, fallbackLeadOutLba: 400);

        DiscTrack track = Assert.Single(tracks);
        Assert.Equal(0, track.StartLba);
        Assert.Equal(400, track.SectorCount);
    }

    [Fact]
    public void ParseWindowsTocTreatsATrackZeroDescriptorAsTheLeadOut()
    {
        byte[] toc = BuildWindowsToc(
            (1, 0x04, 0, 2, 0),
            (0, 0x00, 0, 10, 0));

        IReadOnlyList<DiscTrack> tracks = PhysicalDiscToc.ParseWindowsToc(toc);

        DiscTrack track = Assert.Single(tracks);
        Assert.Equal((10 * 75) - 150, track.SectorCount);
    }

    [Fact]
    public void BuildTracksSortsByAddressAndSkipsDuplicateNumbers()
    {
        var entries = new List<DiscTocEntry>
        {
            new(2, IsAudio: true, StartLba: 200),
            new(1, IsAudio: false, StartLba: 0),
            new(1, IsAudio: false, StartLba: 0),
        };

        IReadOnlyList<DiscTrack> tracks = PhysicalDiscToc.BuildTracks(entries, leadOutLba: 500);

        Assert.Equal(2, tracks.Count);
        AssertTrack(tracks[0], number: 1, DiscTrackKind.Data, startLba: 0, sectors: 200, sectorSize: 2048);
        AssertTrack(tracks[1], number: 2, DiscTrackKind.Audio, startLba: 200, sectors: 300, sectorSize: 2352);
    }

    [Theory]
    [InlineData(0, 2, 0, 0)]
    [InlineData(0, 4, 0, 150)]
    [InlineData(1, 0, 0, 4350)]
    public void MsfToLbaSubtractsTheStandardLeadIn(byte minute, byte second, byte frame, int expected)
    {
        Assert.Equal(expected, PhysicalDiscToc.MsfToLba(minute, second, frame));
    }

    [Fact]
    public void EnsureCoverageExtendsATrailingDataTrackToTheCookedLength()
    {
        IReadOnlyList<DiscTrack> tracks = PhysicalDiscToc.BuildTracks(
            [new DiscTocEntry(1, IsAudio: false, StartLba: 0)],
            leadOutLba: 100);

        IReadOnlyList<DiscTrack> extended = PhysicalDiscToc.EnsureCoverage(tracks, cookedSectorCount: 400);

        DiscTrack track = Assert.Single(extended);
        Assert.Equal(400, track.SectorCount);
        Assert.Equal(100, tracks[0].SectorCount);
    }

    [Fact]
    public void EnsureCoverageLeavesAMixedModeDiscAlone()
    {
        IReadOnlyList<DiscTrack> tracks = PhysicalDiscToc.BuildTracks(
            [
                new DiscTocEntry(1, IsAudio: false, StartLba: 0),
                new DiscTocEntry(2, IsAudio: true, StartLba: 100),
            ],
            leadOutLba: 500);

        Assert.Same(tracks, PhysicalDiscToc.EnsureCoverage(tracks, cookedSectorCount: 100));
    }

    [Theory]
    [InlineData(0, 60, 0)]
    [InlineData(0, 0, 75)]
    public void MsfToLbaRejectsInvalidTimes(byte minute, byte second, byte frame)
    {
        Assert.Equal(-1, PhysicalDiscToc.MsfToLba(minute, second, frame));
    }

    private static void AssertTrack(
        DiscTrack track,
        int number,
        DiscTrackKind kind,
        int startLba,
        int sectors,
        int sectorSize)
    {
        Assert.Equal(number, track.Number);
        Assert.Equal(kind, track.Kind);
        Assert.Equal(startLba, track.StartLba);
        Assert.Equal(sectors, track.SectorCount);
        Assert.Equal(sectorSize, track.SectorSize);
    }

    /// <summary>Builds a synthetic <c>CDROM_TOC</c> response with 8-byte descriptors.</summary>
    private static byte[] BuildWindowsToc(params (int Track, int Control, int Minute, int Second, int Frame)[] entries)
    {
        var buffer = new byte[4 + (entries.Length * 8)];
        int length = buffer.Length;
        buffer[0] = (byte)length;
        buffer[1] = (byte)(length >> 8);
        buffer[2] = 1;
        var lastTrack = 1;
        foreach ((int Track, int Control, int Minute, int Second, int Frame) entry in entries)
        {
            if (entry.Track is >= 1 and <= 99)
            {
                lastTrack = Math.Max(lastTrack, entry.Track);
            }
        }

        buffer[3] = (byte)lastTrack;
        for (var i = 0; i < entries.Length; i++)
        {
            int offset = 4 + (i * 8);
            buffer[offset + 1] = (byte)entries[i].Control;
            buffer[offset + 2] = (byte)entries[i].Track;
            buffer[offset + 5] = (byte)entries[i].Minute;
            buffer[offset + 6] = (byte)entries[i].Second;
            buffer[offset + 7] = (byte)entries[i].Frame;
        }

        return buffer;
    }
}
