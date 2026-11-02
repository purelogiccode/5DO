using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Tests.Storage;

public sealed class DiscHealthTests
{
    [Fact]
    public void Parse_ReadsGamesAndNormalizesHashes()
    {
        const string xml = """
            <?xml version="1.0"?>
            <datafile>
              <game name="Game One">
                <description>First</description>
                <rom name="one.bin" size="2352" crc="cbf43926" md5="25f9e794323b453885f5181f1b624d0b" sha1="f7c3bc1d808e04732adf679965ccc34ca7ae3441"/>
              </game>
              <game name="Game Two">
                <rom name="two.bin" size="4096" crc="00000000"/>
                <rom name="bad.bin" size="not-a-number" crc="00000000"/>
              </game>
            </datafile>
            """;

        RedumpDat dat = RedumpDat.Parse(xml);

        Assert.Equal(2, dat.Games.Count);
        Assert.Equal(2, dat.RomCount);
        Assert.Equal("Game One", dat.Games[0].Name);
        Assert.Equal("First", dat.Games[0].Description);
        Assert.Equal("CBF43926", dat.Games[0].Roms[0].Crc32);
        Assert.Equal("25F9E794323B453885F5181F1B624D0B", dat.Games[0].Roms[0].Md5);
        Assert.Equal("Game Two", dat.Games[1].Name);
        Assert.Single(dat.Games[1].Roms);
    }

    [Fact]
    public void Parse_RejectsMalformedOrEmptyData()
    {
        Assert.Throws<InvalidDataException>(() => RedumpDat.Parse("<not-a-dat/>"));
        Assert.Throws<InvalidDataException>(() => RedumpDat.Parse("<datafile><game name=\"x\"/></datafile>"));
        Assert.Throws<InvalidDataException>(() => RedumpDat.Parse("<datafile>"));
    }

    [Fact]
    public void Verify_MatchesASingleTrackByEveryHash()
    {
        using var disc = new FakeTrackedDisc([2]);
        byte[] data = disc.TrackBytes(0);
        RedumpDat dat = CreateDat("Fake Game", "Fake Track.bin", data);

        DiscDatResult result = DiscHealth.Verify(disc, dat);

        Assert.True(result.KnownDump);
        Assert.Equal("Fake Game", result.GameName);
        Assert.Equal(1, result.MatchedTracks);
        DiscDatTrackResult track = Assert.Single(result.Tracks);
        Assert.True(track.Matched);
        Assert.Equal(2 * 2352, track.Size);
        Assert.Equal("Fake Game", track.Game);
        Assert.Equal("Fake Track.bin", track.Rom);
    }

    [Fact]
    public void Verify_ReportsAMismatchedTrack()
    {
        using var disc = new FakeTrackedDisc([2]);
        byte[] data = disc.TrackBytes(0);
        RedumpDat dat = CreateDat("Fake Game", "Fake Track.bin", data);
        disc.TrackBytes(0)[0] ^= 0xFF;

        DiscDatResult result = DiscHealth.Verify(disc, dat);

        Assert.False(result.KnownDump);
        Assert.Equal(0, result.MatchedTracks);
        Assert.False(Assert.Single(result.Tracks).Matched);
    }

    [Fact]
    public void Verify_MatchesEveryTrackInAMultiTrackDisc()
    {
        using var disc = new FakeTrackedDisc([2, 1]);
        RedumpDat dat = CreateMultiTrackDat(
            "Multi Game",
            ("Track 1.bin", disc.TrackBytes(0)),
            ("Track 2.bin", disc.TrackBytes(1)));

        DiscDatResult result = DiscHealth.Verify(disc, dat);

        Assert.True(result.KnownDump);
        Assert.Equal("Multi Game", result.GameName);
        Assert.Equal(2, result.MatchedTracks);
        Assert.Equal(2, result.Tracks.Count);
        Assert.All(result.Tracks, track => Assert.True(track.Matched));
    }

    private static RedumpDat CreateDat(string game, string romName, byte[] data)
    {
        return RedumpDat.Parse(
            string.Create(
                CultureInfo.InvariantCulture,
                $"""
                <datafile>
                  <game name="{game}">
                    <rom name="{romName}" size="{data.Length}" md5="{Hash(data, HashAlgorithmName.MD5)}"/>
                  </game>
                </datafile>
                """));
    }

    private static RedumpDat CreateMultiTrackDat(
        string game,
        params (string Name, byte[] Data)[] roms)
    {
        var builder = new StringBuilder();
        builder.Append("<datafile><game name=\"");
        builder.Append(game);
        builder.Append("\">");
        foreach ((var name, byte[] data) in roms)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<rom name=\"{name}\" size=\"{data.Length}\" ");
            builder.Append(CultureInfo.InvariantCulture, $"md5=\"{Hash(data, HashAlgorithmName.MD5)}\" ");
            builder.Append(CultureInfo.InvariantCulture, $"sha1=\"{Hash(data, HashAlgorithmName.SHA1)}\"/>");
        }

        builder.Append("</game></datafile>");
        return RedumpDat.Parse(builder.ToString());
    }

    private static string Hash(byte[] data, HashAlgorithmName algorithm)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(algorithm);
        hash.AppendData(data);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private sealed class FakeTrackedDisc : ITrackedDiscSource
    {
        private readonly byte[][] _data;

        internal FakeTrackedDisc(int[] sectorsPerTrack)
        {
            _data = new byte[sectorsPerTrack.Length][];
            var tracks = new List<DiscTrack>(sectorsPerTrack.Length);
            var start = 0;
            for (var track = 0; track < sectorsPerTrack.Length; track++)
            {
                int sectors = sectorsPerTrack[track];
                var bytes = new byte[sectors * 2352];
                for (var i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = (byte)((track * 37) + (i * 11) + (i / 2352));
                }

                _data[track] = bytes;
                tracks.Add(new DiscTrack(track + 1, DiscTrackKind.Data, start, sectors, 2352, 16));
                start += sectors;
            }

            Tracks = tracks;
            TotalSectorCount = start;
        }

        public int SectorCount => TotalSectorCount;

        public int SectorSize => 2048;

        public IReadOnlyList<DiscTrack> Tracks { get; }

        public int TotalSectorCount { get; }

        internal byte[] TrackBytes(int track) => _data[track];

        public int ReadTrackSector(int lba, Span<byte> destination)
        {
            DiscTrack track = FindTrack(Tracks, lba);
            int offset = (lba - track.StartLba) * 2352;
            _data[track.Number - 1].AsSpan(offset, 2352).CopyTo(destination);
            return 2352;
        }

        public void ReadSector(int lba, Span<byte> destination)
        {
            DiscTrack track = FindTrack(Tracks, lba);
            int offset = ((lba - track.StartLba) * 2352) + 16;
            _data[track.Number - 1].AsSpan(offset, 2048).CopyTo(destination);
        }

        public void Dispose()
        {
        }
    }

    private static DiscTrack FindTrack(IReadOnlyList<DiscTrack> tracks, int lba)
    {
        foreach (DiscTrack track in tracks)
        {
            if (track.Contains(lba))
            {
                return track;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(lba));
    }
}
