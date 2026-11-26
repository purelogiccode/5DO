using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Storage;

namespace FiveDo.Tests.Storage;

public sealed class MdsCcdDiscImageTests
{
    [Fact]
    public void MdsImage_ReadsSingleDataTrackAndMatchesBinGameId()
    {
        using var fixture = new MdsCcdFixture(dataSectors: 12);
        fixture.WriteMds(singleTrack: true);

        using var bin = new BinImage(fixture.BinPath);
        using MdsDiscImage mds = MdsDiscImage.Open(fixture.MdsPath);

        Assert.Single(mds.Tracks);
        DiscTrack track = mds.Tracks[0];
        Assert.Equal(DiscTrackKind.Data, track.Kind);
        Assert.Equal(0, track.StartLba);
        Assert.Equal(12, track.SectorCount);
        Assert.Equal(2352, track.SectorSize);
        Assert.Equal(16, track.PayloadOffset);
        Assert.Equal(GameId.Compute(bin), GameId.Compute(mds));

        var sector = new byte[2048];
        mds.ReadSector(7, sector);
        Assert.Equal(ExpectedData(7), sector);

        var native = new byte[2352];
        Assert.Equal(2352, mds.ReadTrackSector(7, native));
        Assert.Equal(ExpectedRawData(7), native);
    }

    [Fact]
    public void MdsImage_ReadsMixedModeAudioTrack()
    {
        using var fixture = new MdsCcdFixture(dataSectors: 10, audioSectors: 4);
        fixture.WriteMds(singleTrack: false);

        using MdsDiscImage mds = MdsDiscImage.Open(fixture.MdsPath);

        Assert.Equal(2, mds.Tracks.Count);
        string trackSummary = string.Join(
            "; ",
            System.Linq.Enumerable.Select(
                mds.Tracks,
                static t => $"{t.Number}:{t.Kind}:{t.StartLba}:{t.SectorCount}"));
        Assert.True(mds.Tracks[0].Kind == DiscTrackKind.Data, trackSummary);
        Assert.Equal(10, mds.Tracks[0].SectorCount);
        Assert.Equal(DiscTrackKind.Audio, mds.Tracks[1].Kind);
        Assert.Equal(10, mds.Tracks[1].StartLba);
        Assert.Equal(4, mds.Tracks[1].SectorCount);

        var audio = new byte[2352];
        Assert.Equal(2352, mds.ReadTrackSector(12, audio));
        Assert.Equal(ExpectedAudio(2), audio);
        Assert.Throws<DiscImageException>(() => mds.ReadSector(12, new byte[2048]));
    }

    [Fact]
    public void CcdImage_ReadsSingleDataTrackAndMatchesBinGameId()
    {
        using var fixture = new MdsCcdFixture(dataSectors: 12);
        fixture.WriteCcd(singleTrack: true);

        using var bin = new BinImage(fixture.BinPath);
        using CcdDiscImage ccd = CcdDiscImage.Open(fixture.CcdPath);

        Assert.Single(ccd.Tracks);
        DiscTrack track = ccd.Tracks[0];
        Assert.Equal(DiscTrackKind.Data, track.Kind);
        Assert.Equal(0, track.StartLba);
        Assert.Equal(12, track.SectorCount);
        Assert.Equal(2352, track.SectorSize);
        Assert.Equal(16, track.PayloadOffset);
        Assert.Equal(GameId.Compute(bin), GameId.Compute(ccd));

        var sector = new byte[2048];
        ccd.ReadSector(9, sector);
        Assert.Equal(ExpectedData(9), sector);
    }

    [Fact]
    public void CcdImage_ReadsMixedModeAudioTrack()
    {
        using var fixture = new MdsCcdFixture(dataSectors: 10, audioSectors: 4);
        fixture.WriteCcd(singleTrack: false);

        using CcdDiscImage ccd = CcdDiscImage.Open(fixture.CcdPath);

        Assert.Equal(2, ccd.Tracks.Count);
        Assert.Equal(DiscTrackKind.Audio, ccd.Tracks[1].Kind);
        Assert.Equal(10, ccd.Tracks[1].StartLba);
        Assert.Equal(4, ccd.Tracks[1].SectorCount);

        var audio = new byte[2352];
        Assert.Equal(2352, ccd.ReadTrackSector(11, audio));
        Assert.Equal(ExpectedAudio(1), audio);
        Assert.Throws<DiscImageException>(() => ccd.ReadSector(11, new byte[2048]));
    }

    [Fact]
    public void Factory_OpensMdsAndCcdExtensionsAndHeaders()
    {
        using var fixture = new MdsCcdFixture(dataSectors: 8);
        fixture.WriteMds(singleTrack: true);
        fixture.WriteCcd(singleTrack: true);

        using (IDiscSource mds = DiscImageFactory.Open(fixture.MdsPath))
        {
            Assert.IsType<MdsDiscImage>(mds);
        }

        using (IDiscSource ccd = DiscImageFactory.Open(fixture.CcdPath))
        {
            Assert.IsType<CcdDiscImage>(ccd);
        }

        string renamedMds = Path.Combine(fixture.Directory, "renamed.img");
        File.Copy(fixture.MdsPath, renamedMds);
        using (IDiscSource byHeader = DiscImageFactory.Open(renamedMds))
        {
            Assert.IsType<MdsDiscImage>(byHeader);
        }
    }

    [Fact]
    public void Factory_OpensRelativeMdsAndCcdPaths()
    {
        using var fixture = new MdsCcdFixture(dataSectors: 8);
        fixture.WriteMds(singleTrack: true);
        fixture.WriteCcd(singleTrack: true);

        string local = Path.Combine(
            Environment.CurrentDirectory,
            "artifacts",
            "relative-mdsccd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(local);
        try
        {
            File.Copy(fixture.MdsPath, Path.Combine(local, "disc.mds"));
            File.Copy(Path.Combine(fixture.Directory, "disc.mdf"), Path.Combine(local, "disc.mdf"));
            File.Copy(fixture.CcdPath, Path.Combine(local, "disc.ccd"));
            File.Copy(Path.Combine(fixture.Directory, "disc.img"), Path.Combine(local, "disc.img"));

            string relativeMds = Path.Combine("artifacts", Path.GetFileName(local), "disc.mds");
            string relativeCcd = Path.Combine("artifacts", Path.GetFileName(local), "disc.ccd");
            Assert.False(Path.IsPathRooted(relativeMds));
            Assert.False(Path.IsPathRooted(relativeCcd));

            using (IDiscSource mds = DiscImageFactory.Open(relativeMds))
            {
                Assert.IsType<MdsDiscImage>(mds);
                var sector = new byte[2048];
                mds.ReadSector(0, sector);
                Assert.Equal(ExpectedData(0), sector);
            }

            using (IDiscSource ccd = DiscImageFactory.Open(relativeCcd))
            {
                Assert.IsType<CcdDiscImage>(ccd);
                var sector = new byte[2048];
                ccd.ReadSector(0, sector);
                Assert.Equal(ExpectedData(0), sector);
            }
        }
        finally
        {
            Directory.Delete(local, recursive: true);
        }
    }

    [Fact]
    public void LibraryScanner_RecognizesMdsAndCcdExtensions()
    {
        using var fixture = new MdsCcdFixture(dataSectors: 8);
        fixture.WriteMds(singleTrack: true);
        fixture.WriteCcd(singleTrack: true);

        IReadOnlyList<GameLibraryEntry> entries = GameLibraryScanner.Scan(
            [fixture.Directory],
            deduplicate: false);

        Assert.Contains(
            entries,
            entry => entry.Path.EndsWith("disc.mds", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            entries,
            entry => entry.Path.EndsWith("disc.ccd", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MdsAndCcdImages_RejectMalformedInput()
    {
        using var fixture = new MdsCcdFixture(dataSectors: 4);

        string badMds = Path.Combine(fixture.Directory, "bad.mds");
        File.WriteAllBytes(badMds, [1, 2, 3, 4]);
        Assert.Throws<DiscImageException>(() => MdsDiscImage.Open(badMds));

        string missingDataMds = Path.Combine(fixture.Directory, "missing.mds");
        fixture.WriteMds(singleTrack: true, dataFileName: "missing.mdf");
        File.Move(fixture.MdsPath, missingDataMds, overwrite: true);
        File.Delete(Path.Combine(fixture.Directory, "missing.mdf"));
        Assert.Throws<DiscImageException>(() => MdsDiscImage.Open(missingDataMds));

        string missingImgCcd = Path.Combine(fixture.Directory, "missing.ccd");
        fixture.WriteCcd(singleTrack: true, imageFileName: "missing.img");
        File.Move(fixture.CcdPath, missingImgCcd, overwrite: true);
        File.Delete(Path.Combine(fixture.Directory, "missing.img"));
        Assert.Throws<DiscImageException>(() => CcdDiscImage.Open(missingImgCcd));
    }

    private static byte[] ExpectedData(int lba)
    {
        var sector = new byte[2048];
        for (var i = 0; i < sector.Length; i += 4)
        {
            BinaryPrimitives.WriteUInt32BigEndian(sector.AsSpan(i), (uint)((lba * 0x01000193) ^ i));
        }

        return sector;
    }

    private static byte[] ExpectedRawData(int lba)
    {
        var raw = new byte[2352];
        raw[0] = 0x00;
        for (var i = 1; i <= 10; i++)
        {
            raw[i] = 0xFF;
        }

        raw[11] = 0x00;
        raw[12] = (byte)((lba / (60 * 75)) % 100);
        raw[13] = (byte)((lba / 75) % 60);
        raw[14] = (byte)(lba % 75);
        raw[15] = 1;
        ExpectedData(lba).CopyTo(raw.AsSpan(16));
        return raw;
    }

    private static byte[] ExpectedAudio(int index)
    {
        var audio = new byte[2352];
        for (var i = 0; i < audio.Length; i++)
        {
            audio[i] = (byte)((index * 31) + i + 0x40);
        }

        return audio;
    }

    private sealed class MdsCcdFixture : IDisposable
    {
        internal MdsCcdFixture(int dataSectors, int audioSectors = 0)
        {
            this.dataSectors = dataSectors;
            this.audioSectors = audioSectors;
            Directory = Path.Combine(Path.GetTempPath(), $"fivedo-mdsccd-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(Directory);
            BinPath = Path.Combine(Directory, "disc.bin");
            MdsPath = Path.Combine(Directory, "disc.mds");
            CcdPath = Path.Combine(Directory, "disc.ccd");
            WriteRawImage(BinPath);
        }

        private readonly int dataSectors;
        private readonly int audioSectors;

        internal string Directory { get; }

        internal string BinPath { get; }

        internal string MdsPath { get; }

        internal string CcdPath { get; }

        internal void WriteMds(bool singleTrack, string dataFileName = "disc.mdf")
        {
            string mdfPath = Path.Combine(Directory, dataFileName);
            File.Copy(BinPath, mdfPath, overwrite: true);

            var descriptor = new byte[0x200];
            Encoding.ASCII.GetBytes("MEDIA DESCRIPTOR").CopyTo(descriptor, 0);
            BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(0x10), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(0x12), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(0x14), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(0x50), 0x60);

            const int sessionOffset = 0x60;
            descriptor[sessionOffset + 0x0A] = (byte)(singleTrack ? 1 : 2);
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(sessionOffset + 0x14), 0x80);

            const int trackOffset = 0x80;
            const int extraOffset = 0x120;
            const int footerOffset = 0x140;
            const int nameOffset = 0x180;

            WriteTrack(descriptor, trackOffset, 1, mode: 2, startLba: 0, extraOffset, footerOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(extraOffset), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(
                descriptor.AsSpan(extraOffset + 4),
                (uint)dataSectors);
            WriteFooter(descriptor, footerOffset, nameOffset);

            if (!singleTrack)
            {
                const int track2Offset = trackOffset + 0x50;
                const int extra2Offset = extraOffset + 8;
                const int footer2Offset = footerOffset + 16;
                WriteTrack(
                    descriptor,
                    track2Offset,
                    2,
                    mode: 1,
                    startLba: dataSectors,
                    extra2Offset,
                    footer2Offset);
                BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(extra2Offset), 0);
                BinaryPrimitives.WriteUInt32LittleEndian(
                    descriptor.AsSpan(extra2Offset + 4),
                    (uint)audioSectors);
                WriteFooter(descriptor, footer2Offset, nameOffset);
            }

            Encoding.ASCII.GetBytes(dataFileName).CopyTo(descriptor, nameOffset);
            File.WriteAllBytes(MdsPath, descriptor);
        }

        internal void WriteCcd(bool singleTrack, string imageFileName = "disc.img")
        {
            string imgPath = Path.Combine(Directory, imageFileName);
            File.Copy(BinPath, imgPath, overwrite: true);

            var ccd = new StringBuilder();
            ccd.AppendLine("[CloneCD]");
            ccd.AppendLine("Version=3");
            ccd.AppendLine();
            ccd.AppendLine("[Disc]");
            ccd.Append("TocEntries=").Append(singleTrack ? "1" : "2").AppendLine();
            ccd.AppendLine("Sessions=1");
            ccd.AppendLine("DataTracksScrambled=0");
            ccd.AppendLine("CDTextLength=0");
            ccd.AppendLine();
            ccd.AppendLine("[TRACK 1]");
            ccd.AppendLine("MODE=1");
            ccd.AppendLine("INDEX 1=0");
            if (!singleTrack)
            {
                ccd.AppendLine();
                ccd.AppendLine("[TRACK 2]");
                ccd.AppendLine("MODE=0");
                ccd.Append("INDEX 1=").Append(dataSectors.ToString(System.Globalization.CultureInfo.InvariantCulture)).AppendLine();
            }

            File.WriteAllText(CcdPath, ccd.ToString(), new UTF8Encoding(false));
        }

        private static void WriteTrack(
            byte[] descriptor,
            int offset,
            byte number,
            byte mode,
            int startLba,
            int extraOffset,
            int footerOffset)
        {
            descriptor[offset] = mode;
            descriptor[offset + 0x04] = number;
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(offset + 0x0C), (uint)extraOffset);
            BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(offset + 0x10), 2352);
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(offset + 0x24), (uint)startLba);
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(offset + 0x30), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(offset + 0x34), (uint)footerOffset);
        }

        private static void WriteFooter(byte[] descriptor, int offset, int nameOffset)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(offset), (uint)nameOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(offset + 4), 0);
        }

        private void WriteRawImage(string path)
        {
            using FileStream stream = File.Create(path);
            for (var lba = 0; lba < dataSectors; lba++)
            {
                stream.Write(ExpectedRawData(lba));
            }

            for (var lba = 0; lba < audioSectors; lba++)
            {
                stream.Write(ExpectedAudio(lba));
            }
        }

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
