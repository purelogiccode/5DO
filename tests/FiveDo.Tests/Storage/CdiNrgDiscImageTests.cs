using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CDISharp;
using FiveDo.Core;
using FiveDo.Storage;
using NRGSharp;

namespace FiveDo.Tests.Storage;

public sealed class CdiNrgDiscImageTests
{
    [Fact]
    public void CdiImage_ReadsCookedMode1AndMatchesTheIsoGameId()
    {
        using var folder = new ImageFolder();
        const int sectorCount = 8;
        string isoPath = folder.PathFor("disc.iso");
        string cdiPath = folder.PathFor("disc.cdi");
        File.WriteAllBytes(isoPath, BuildIso(sectorCount));
        BuildCdi(cdiPath, sectorCount, CdiTrackMode.Mode1, CdiReadMode.Mode1);

        using IDiscSource iso = DiscImageFactory.Open(isoPath);
        using CdiDiscImage cdi = CdiDiscImage.Open(cdiPath);
        using var parsed = CdiImage.Open(cdiPath);

        Assert.Equal(8, cdi.SectorCount);
        Assert.Equal(2048, cdi.SectorSize);
        Assert.Single(parsed.Sessions);
        Assert.Equal(1, parsed.Sessions[0].Number);
        Assert.Equal(1, parsed.Sessions[0].FirstTrackNumber);
        Assert.Equal(1, parsed.Sessions[0].TrackCount);
        DiscTrack track = Assert.Single(cdi.Tracks);
        Assert.Equal(DiscTrackKind.Data, track.Kind);
        Assert.Equal(0, track.StartLba);
        Assert.Equal(sectorCount, track.SectorCount);
        Assert.Equal(2048, track.SectorSize);
        Assert.Equal(0, track.PayloadOffset);
        Assert.Equal(1, parsed.Tracks[0].Number);
        Assert.Equal(1, parsed.Tracks[0].Session);
        Assert.Equal(CdiTrackMode.Mode1, parsed.Tracks[0].Mode);
        Assert.Equal(CdiReadMode.Mode1, parsed.Tracks[0].ReadMode);
        Assert.Equal(0, parsed.Tracks[0].SourceStartSector);
        Assert.Equal(150, parsed.Tracks[0].PregapSectors);
        Assert.Null(parsed.Tracks[0].Title);
        Assert.Null(parsed.Tracks[0].Isrc);
        Assert.Equal(4, parsed.Tracks[0].Control);
        Assert.Equal(GameId.Compute(iso), GameId.Compute(cdi));

        var sector = new byte[2048];
        cdi.ReadSector(5, sector);
        Assert.Equal(ExpectedSector(5), sector);

        var native = new byte[2048];
        Assert.Equal(2048, cdi.ReadTrackSector(5, native));
        Assert.Equal(ExpectedSector(5), native);
    }

    [Theory]
    [InlineData(CdiTrackMode.Mode1, CdiReadMode.Raw, 2352, 16)]
    [InlineData(CdiTrackMode.Mode1, CdiReadMode.RawPq, 2368, 16)]
    [InlineData(CdiTrackMode.Mode1, CdiReadMode.RawPw, 2448, 16)]
    [InlineData(CdiTrackMode.Mode2, CdiReadMode.Raw, 2352, 24)]
    [InlineData(CdiTrackMode.Mode2, CdiReadMode.RawPq, 2368, 24)]
    [InlineData(CdiTrackMode.Mode2, CdiReadMode.RawPw, 2448, 24)]
    public void CdiImage_ReadsRawSectorVariants(
        CdiTrackMode mode,
        CdiReadMode readMode,
        int bytesPerSector,
        int payloadOffset)
    {
        using var folder = new ImageFolder();
        const int sectorCount = 10;
        string path = folder.PathFor("disc.cdi");
        BuildCdi(path, sectorCount, mode, readMode);

        using var image = CdiImage.Open(path);
        CdiTrack track = Assert.Single(image.Tracks);
        Assert.Equal(2352, track.SectorSize);
        Assert.Equal(bytesPerSector, track.BytesPerSector);
        Assert.Equal(payloadOffset, track.PayloadOffset);
        Assert.Equal(sectorCount, track.SectorCount);

        var sector = new byte[2048];
        image.ReadSector(7, sector);
        Assert.Equal(ExpectedSector(7), sector);

        var native = new byte[2352];
        Assert.Equal(2352, image.ReadTrackSector(7, native));
        Assert.Equal(ExpectedRawSector(7, mode == CdiTrackMode.Mode2), native);
    }

    [Fact]
    public void CdiImage_ReadsCookedMode2Body()
    {
        using var folder = new ImageFolder();
        string path = folder.PathFor("disc.cdi");
        BuildCdi(path, 6, CdiTrackMode.Mode2, CdiReadMode.Mode2);

        using var image = CdiImage.Open(path);
        CdiTrack track = Assert.Single(image.Tracks);
        Assert.Equal(2336, track.SectorSize);
        Assert.Equal(2336, track.BytesPerSector);
        Assert.Equal(8, track.PayloadOffset);

        var sector = new byte[2048];
        image.ReadSector(4, sector);
        Assert.Equal(ExpectedSector(4), sector);
    }

    [Fact]
    public void CdiImage_RejectsAudioOnlyAndMalformedImages()
    {
        using var folder = new ImageFolder();
        string audioPath = folder.PathFor("audio.cdi");
        BuildCdi(audioPath, 4, CdiTrackMode.Audio, CdiReadMode.Raw);
        DiscImageException audio = Assert.Throws<DiscImageException>(() => CdiDiscImage.Open(audioPath));
        Assert.Contains("no data track", audio.Message, StringComparison.OrdinalIgnoreCase);

        string brokenPath = folder.PathFor("broken.cdi");
        File.WriteAllBytes(brokenPath, [1, 2, 3, 0x40, 0, 0, 0]);
        Assert.Throws<DiscImageException>(() => CdiDiscImage.Open(brokenPath));
    }

    [Fact]
    public void NrgImage_ReadsDaoV1AndV2CookedMode1AndMatchesTheIsoGameId()
    {
        using var folder = new ImageFolder();
        foreach (bool version2 in new[] { false, true })
        {
            string isoPath = folder.PathFor(version2 ? "v2.iso" : "v1.iso");
            string nrgPath = folder.PathFor(version2 ? "v2.nrg" : "v1.nrg");
            File.WriteAllBytes(isoPath, BuildIso(8));
            BuildNrgDao(nrgPath, version2, 8, NrgTrackMode.Data, 2048, leadInSectors: 0);

            using IDiscSource iso = DiscImageFactory.Open(isoPath);
            using NrgDiscImage nrg = NrgDiscImage.Open(nrgPath);
            using var parsed = NrgImage.Open(nrgPath);
            Assert.Equal(version2 ? NrgVersion.V2 : NrgVersion.V1, parsed.Version);
            Assert.True(parsed.FirstChunkOffset > 0);
            Assert.Equal(8, nrg.SectorCount);
            DiscTrack track = Assert.Single(nrg.Tracks);
            Assert.Equal(DiscTrackKind.Data, track.Kind);
            Assert.Equal(0, track.StartLba);
            Assert.Equal(8, track.SectorCount);
            Assert.Equal(2048, track.SectorSize);
            Assert.Equal(1, parsed.Tracks[0].Session);
            Assert.Null(parsed.Tracks[0].Isrc);
            Assert.Equal(GameId.Compute(iso), GameId.Compute(nrg));

            var sector = new byte[2048];
            nrg.ReadSector(6, sector);
            Assert.Equal(ExpectedSector(6), sector);
        }
    }

    [Theory]
    [InlineData(false, 2352, NrgTrackMode.RawData, 16)]
    [InlineData(true, 2352, NrgTrackMode.RawData, 16)]
    [InlineData(true, 2353, NrgTrackMode.RawData, 16)]
    [InlineData(true, 2368, NrgTrackMode.RawData, 16)]
    [InlineData(true, 2448, NrgTrackMode.RawMode2, 24)]
    [InlineData(true, 2449, NrgTrackMode.RawMode2, 24)]
    public void NrgImage_ReadsRawSectorSizeVariants(
        bool version2,
        int bytesPerSector,
        NrgTrackMode mode,
        int payloadOffset)
    {
        using var folder = new ImageFolder();
        string path = folder.PathFor("disc.nrg");
        BuildNrgDao(path, version2, 9, mode, bytesPerSector, leadInSectors: 4);

        using var image = NrgImage.Open(path);
        NrgTrack track = Assert.Single(image.Tracks);
        Assert.Equal(2352, track.SectorSize);
        Assert.Equal(bytesPerSector, track.BytesPerSector);
        Assert.Equal(payloadOffset, track.PayloadOffset);
        Assert.Equal(9, track.SectorCount);

        var sector = new byte[2048];
        image.ReadSector(5, sector);
        Assert.Equal(ExpectedSector(5), sector);

        var native = new byte[2352];
        Assert.Equal(2352, image.ReadTrackSector(5, native));
        Assert.Equal(ExpectedRawSector(5, mode == NrgTrackMode.RawMode2), native);
    }

    [Fact]
    public void NrgImage_ReadsTaoTrackAtOnceMetadata()
    {
        using var folder = new ImageFolder();
        string path = folder.PathFor("tao.nrg");
        BuildNrgTao(path, 6, NrgTrackMode.Data, 2048);

        using var image = NrgImage.Open(path);
        Assert.Equal(NrgVersion.V1, image.Version);
        NrgTrack track = Assert.Single(image.Tracks);
        Assert.Equal(6, track.SectorCount);
        Assert.Equal(2048, track.SectorSize);

        var sector = new byte[2048];
        image.ReadSector(3, sector);
        Assert.Equal(ExpectedSector(3), sector);
    }

    [Fact]
    public void NrgImage_RejectsInvalidFooterAndTruncatedTrack()
    {
        using var folder = new ImageFolder();
        string badFooter = folder.PathFor("bad-footer.nrg");
        File.WriteAllBytes(badFooter, [0, 1, 2, 3, 0x4E, 0x45, 0x52, 0x4F, 0xFF, 0xFF, 0xFF, 0xFF]);
        Assert.Throws<DiscImageException>(() => NrgDiscImage.Open(badFooter));

        string badDao = folder.PathFor("bad-dao.nrg");
        var metadata = new List<byte>();
        metadata.AddRange("DAOX"u8.ToArray());
        metadata.AddRange(BigEndian(100));
        metadata.AddRange(new byte[10]);
        metadata.AddRange("END!"u8.ToArray());
        metadata.AddRange(BigEndian(0));
        var bytes = new List<byte>();
        bytes.AddRange(new byte[16]);
        bytes.AddRange(metadata);
        bytes.AddRange("NER5"u8.ToArray());
        bytes.AddRange(BigEndian((ulong)16));
        File.WriteAllBytes(badDao, [.. bytes]);

        Assert.Throws<DiscImageException>(() => NrgDiscImage.Open(badDao));
    }

    [Fact]
    public void Factory_DetectsCdiAndNrgByExtensionAndHeader()
    {
        using var folder = new ImageFolder();
        string cdiPath = folder.PathFor("disc.cdi");
        string nrgPath = folder.PathFor("disc.nrg");
        BuildCdi(cdiPath, 6, CdiTrackMode.Mode1, CdiReadMode.Mode1);
        BuildNrgDao(nrgPath, version2: true, 6, NrgTrackMode.Data, 2048, leadInSectors: 0);

        using (IDiscSource cdi = DiscImageFactory.Open(cdiPath))
        {
            Assert.IsType<CdiDiscImage>(cdi);
        }

        using (IDiscSource nrg = DiscImageFactory.Open(nrgPath))
        {
            Assert.IsType<NrgDiscImage>(nrg);
        }

        string renamedCdi = folder.PathFor("renamed.dat");
        string renamedNrg = folder.PathFor("renamed.bin");
        File.Copy(cdiPath, renamedCdi);
        File.Copy(nrgPath, renamedNrg);

        using (IDiscSource cdi = DiscImageFactory.Open(renamedCdi))
        {
            Assert.IsType<CdiDiscImage>(cdi);
        }

        using (IDiscSource nrg = DiscImageFactory.Open(renamedNrg))
        {
            Assert.IsType<NrgDiscImage>(nrg);
        }
    }

    [Fact]
    public void CdiImage_ExposesMultiTrackTableOfContentsAndAudio()
    {
        using var folder = new ImageFolder();
        string path = folder.PathFor("mixed.cdi");
        BuildCdiWithAudio(path, dataSectors: 4, audioSectors: 3);

        using var image = CdiImage.Open(path);
        Assert.Equal(2, image.Tracks.Count);
        Assert.Equal(7, image.TotalSectorCount);

        CdiTrack data = image.Tracks[0];
        Assert.False(data.IsAudio);
        Assert.Equal(0, data.StartSector);
        Assert.Equal(4, data.SectorCount);
        Assert.Equal(2048, data.SectorSize);

        CdiTrack audio = image.Tracks[1];
        Assert.True(audio.IsAudio);
        Assert.Equal(4, audio.StartSector);
        Assert.Equal(3, audio.SectorCount);
        Assert.Equal(2352, audio.SectorSize);

        var sector = new byte[2048];
        image.ReadSector(2, sector);
        Assert.Equal(ExpectedSector(2), sector);
        Assert.Throws<InvalidOperationException>(() => image.ReadSector(4, sector));

        var audioSector = new byte[2352];
        Assert.Equal(2352, image.ReadTrackSector(4, audioSector));
        Assert.Equal(ExpectedAudio(0), audioSector);
    }

    [Fact]
    public void NrgImage_ExposesMultiTrackTableOfContentsAndAudio()
    {
        using var folder = new ImageFolder();
        string path = folder.PathFor("mixed.nrg");
        BuildNrgWithAudio(path, dataSectors: 4, audioSectors: 3);

        using var image = NrgImage.Open(path);
        Assert.Equal(2, image.Tracks.Count);
        Assert.Equal(7, image.TotalSectorCount);

        NrgTrack data = image.Tracks[0];
        Assert.False(data.IsAudio);
        Assert.Equal(0, data.StartSector);
        Assert.Equal(4, data.SectorCount);

        NrgTrack audio = image.Tracks[1];
        Assert.True(audio.IsAudio);
        Assert.Equal(4, audio.StartSector);
        Assert.Equal(3, audio.SectorCount);

        var sector = new byte[2048];
        image.ReadSector(2, sector);
        Assert.Equal(ExpectedSector(2), sector);
        Assert.Throws<InvalidOperationException>(() => image.ReadSector(4, sector));

        var audioSector = new byte[2352];
        Assert.Equal(2352, image.ReadTrackSector(4, audioSector));
        Assert.Equal(ExpectedAudio(0), audioSector);
    }

    private static void BuildCdiWithAudio(string path, int dataSectors, int audioSectors)
    {
        var bytes = new List<byte>();
        for (var lba = 0; lba < 150; lba++)
        {
            bytes.AddRange(new byte[2048]);
        }

        for (var lba = 0; lba < dataSectors; lba++)
        {
            bytes.AddRange(ExpectedSector(lba));
        }

        for (var lba = 0; lba < audioSectors; lba++)
        {
            bytes.AddRange(ExpectedAudio(lba));
        }

        var descriptor = new List<byte> { 1 };
        descriptor.AddRange([0, 2, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0xFF, 0xFF]);
        descriptor.AddRange(BuildCdiTrackBlock(1, 0, 0, dataSectors + 150, 150, 4, 1));
        descriptor.AddRange(BuildCdiTrackBlock(0, 2, dataSectors + 150, audioSectors, 0, 0, 0));
        descriptor.AddRange(new byte[15]);

        var discInfo = new byte[95];
        discInfo[46] = 152;
        BinaryPrimitives.WriteUInt32LittleEndian(discInfo.AsSpan(48), (uint)(dataSectors + audioSectors));
        descriptor.AddRange(discInfo);
        descriptor.AddRange(LittleEndian((uint)(descriptor.Count + 4)));
        bytes.AddRange(descriptor);
        File.WriteAllBytes(path, [.. bytes]);
    }

    private static byte[] BuildCdiTrackBlock(
        int mode,
        int readMode,
        int startSector,
        int length,
        int pregap,
        int control,
        int sessionType)
    {
        var block = new List<byte>();
        block.AddRange(new byte[16]);
        block.Add(0);
        block.AddRange(new byte[29]);
        block.AddRange(LittleEndian(152));
        block.AddRange(LittleEndian(2));
        block.AddRange(LittleEndian((uint)pregap));
        block.AddRange(LittleEndian((uint)length));
        block.AddRange(LittleEndian(0u));
        block.AddRange(new byte[2]);
        block.AddRange(LittleEndian((uint)mode));
        block.AddRange(new byte[4]);
        block.AddRange(LittleEndian(0u));
        block.AddRange(LittleEndian(0u));
        block.AddRange(LittleEndian((uint)startSector));
        block.AddRange(LittleEndian((uint)length));
        block.AddRange(new byte[16]);
        block.AddRange(LittleEndian((uint)readMode));
        block.AddRange(LittleEndian((uint)control));
        block.AddRange(new byte[9]);
        block.AddRange(new byte[12]);
        block.AddRange(LittleEndian(0u));
        block.AddRange(new byte[87]);
        block.Add((byte)sessionType);
        block.AddRange(new byte[5]);
        block.Add(0);
        block.AddRange(new byte[2]);
        block.AddRange(LittleEndian(0u));
        return [.. block];
    }

    private static void BuildNrgWithAudio(string path, int dataSectors, int audioSectors)
    {
        var body = new List<byte>();
        for (var lba = 0; lba < dataSectors; lba++)
        {
            body.AddRange(ExpectedSector(lba));
        }

        for (var lba = 0; lba < audioSectors; lba++)
        {
            body.AddRange(ExpectedAudio(lba));
        }

        ulong dataBytes = (ulong)(dataSectors * 2048);
        ulong audioBytes = (ulong)(audioSectors * 2352);
        var payload = new List<byte>();
        payload.AddRange(LittleEndian(0u));
        payload.AddRange(new byte[14]);
        payload.AddRange(BigEndian(1));
        payload.Add(1);
        payload.Add(2);
        AddNrgDaoEntry(payload, (ushort)NrgTrackMode.Data, 2048, 0, 0, dataBytes);
        AddNrgDaoEntry(payload, (ushort)NrgTrackMode.Audio, 2352, dataBytes, dataBytes, dataBytes + audioBytes);

        byte[] payloadLength = LittleEndian((uint)payload.Count);
        for (var index = 0; index < payloadLength.Length; index++)
        {
            payload[index] = payloadLength[index];
        }

        var metadata = new List<byte>();
        metadata.AddRange("DAOX"u8.ToArray());
        metadata.AddRange(BigEndian((uint)payload.Count));
        metadata.AddRange(payload);
        metadata.AddRange("END!"u8.ToArray());
        metadata.AddRange(BigEndian(0u));

        int metadataOffset = body.Count;
        body.AddRange(metadata);
        body.AddRange("NER5"u8.ToArray());
        body.AddRange(BigEndian((ulong)metadataOffset));
        File.WriteAllBytes(path, [.. body]);
    }

    private static void AddNrgDaoEntry(
        List<byte> payload,
        ushort mode,
        ushort sectorSize,
        ulong index0,
        ulong index1,
        ulong end)
    {
        payload.AddRange(new byte[12]);
        payload.AddRange(BigEndian(sectorSize));
        payload.AddRange(LittleEndian(mode));
        payload.AddRange(BigEndian(1));
        payload.AddRange(BigEndian(index0));
        payload.AddRange(BigEndian(index1));
        payload.AddRange(BigEndian(end));
    }

    private static void BuildCdi(
        string path,
        int sectorCount,
        CdiTrackMode mode,
        CdiReadMode readMode,
        int leadInSectors = 150)
    {
        int stride = readMode switch
        {
            CdiReadMode.Mode1 => 2048,
            CdiReadMode.Mode2 => 2336,
            CdiReadMode.Raw => 2352,
            CdiReadMode.RawPq => 2368,
            _ => 2448,
        };

        var bytes = new List<byte>();
        for (var lba = 0; lba < leadInSectors; lba++)
        {
            bytes.AddRange(new byte[stride]);
        }

        for (var lba = 0; lba < sectorCount; lba++)
        {
            bytes.AddRange(BuildImageSector(lba, mode, readMode, stride));
        }

        var descriptor = new List<byte> { 1 };
        descriptor.AddRange([0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0xFF, 0xFF]);
        descriptor.AddRange(new byte[16]);
        descriptor.Add(0);
        descriptor.AddRange(new byte[29]);
        descriptor.AddRange(LittleEndian(152));
        descriptor.AddRange(LittleEndian(2));
        descriptor.AddRange(LittleEndian((uint)leadInSectors));
        descriptor.AddRange(LittleEndian((uint)sectorCount));
        descriptor.AddRange(LittleEndian(0u));
        descriptor.AddRange(new byte[2]);
        descriptor.AddRange(LittleEndian((uint)mode));
        descriptor.AddRange(new byte[4]);
        descriptor.AddRange(LittleEndian(0u));
        descriptor.AddRange(LittleEndian(0u));
        descriptor.AddRange(LittleEndian(0u));
        descriptor.AddRange(LittleEndian((uint)(sectorCount + leadInSectors)));
        descriptor.AddRange(new byte[16]);
        descriptor.AddRange(LittleEndian((uint)readMode));
        descriptor.AddRange(LittleEndian(mode == CdiTrackMode.Audio ? 0u : 4u));
        descriptor.AddRange(new byte[9]);
        descriptor.AddRange(new byte[12]);
        descriptor.AddRange(LittleEndian(0u));
        descriptor.AddRange(new byte[87]);
        descriptor.Add((byte)(mode == CdiTrackMode.Audio ? 0 : 1));
        descriptor.AddRange(new byte[5]);
        descriptor.Add(0);
        descriptor.AddRange(new byte[2]);
        descriptor.AddRange(LittleEndian(0u));
        descriptor.AddRange(new byte[15]);

        var discInfo = new byte[95];
        discInfo[46] = 152;
        BinaryPrimitives.WriteUInt32LittleEndian(discInfo.AsSpan(48), (uint)sectorCount);
        descriptor.AddRange(discInfo);
        descriptor.AddRange(LittleEndian((uint)(descriptor.Count + 4)));
        bytes.AddRange(descriptor);
        File.WriteAllBytes(path, [.. bytes]);
    }

    private static void BuildNrgDao(
        string path,
        bool version2,
        int sectorCount,
        NrgTrackMode mode,
        int bytesPerSector,
        int leadInSectors)
    {
        int native = mode switch
        {
            NrgTrackMode.Data or NrgTrackMode.DataMode2Form1 => 2048,
            NrgTrackMode.DataMode2Form2 => 2336,
            _ => 2352,
        };

        var body = new List<byte>();
        for (var lba = 0; lba < leadInSectors; lba++)
        {
            body.AddRange(new byte[bytesPerSector]);
        }

        for (var lba = 0; lba < sectorCount; lba++)
        {
            body.AddRange(BuildImageSector(lba, mode, bytesPerSector, native));
        }

        var payload = new List<byte>();
        payload.AddRange(LittleEndian(0u));
        payload.AddRange(new byte[14]);
        payload.AddRange(BigEndian(1));
        payload.Add(1);
        payload.Add(1);
        payload.AddRange(new byte[12]);
        payload.AddRange(BigEndian((ushort)bytesPerSector));
        payload.AddRange(LittleEndian((ushort)mode));
        payload.AddRange(BigEndian(1));
        ulong index0 = 0;
        ulong index1 = (ulong)leadInSectors * (ulong)bytesPerSector;
        ulong end = (ulong)(leadInSectors + sectorCount) * (ulong)bytesPerSector;
        if (version2)
        {
            payload.AddRange(BigEndian(index0));
            payload.AddRange(BigEndian(index1));
            payload.AddRange(BigEndian(end));
        }
        else
        {
            payload.AddRange(BigEndian((uint)index0));
            payload.AddRange(BigEndian((uint)index1));
            payload.AddRange(BigEndian((uint)end));
        }

        byte[] payloadLength = LittleEndian((uint)payload.Count);
        for (var index = 0; index < payloadLength.Length; index++)
        {
            payload[index] = payloadLength[index];
        }
        var metadata = new List<byte>();
        metadata.AddRange(Encoding.ASCII.GetBytes(version2 ? "DAOX" : "DAOI"));
        metadata.AddRange(BigEndian((uint)payload.Count));
        metadata.AddRange(payload);
        metadata.AddRange("END!"u8.ToArray());
        metadata.AddRange(BigEndian(0u));

        body.AddRange(metadata);
        body.AddRange(Encoding.ASCII.GetBytes(version2 ? "NER5" : "NERO"));
        if (version2)
        {
            body.AddRange(BigEndian((ulong)0));
        }
        else
        {
            body.AddRange(BigEndian(0u));
        }

        int metadataOffset = body.Count - metadata.Count - (version2 ? 12 : 8);
        int footerOffset = body.Count - (version2 ? 12 : 8);
        byte[] offsetBytes = version2
            ? BigEndian((ulong)metadataOffset)
            : BigEndian((uint)metadataOffset);
        for (var index = 0; index < offsetBytes.Length; index++)
        {
            body[footerOffset + 4 + index] = offsetBytes[index];
        }

        File.WriteAllBytes(path, [.. body]);
    }

    private static void BuildNrgTao(string path, int sectorCount, NrgTrackMode mode, int bytesPerSector)
    {
        var body = new List<byte>();
        for (var lba = 0; lba < sectorCount; lba++)
        {
            body.AddRange(BuildImageSector(lba, mode, bytesPerSector, bytesPerSector));
        }

        var payload = new List<byte>();
        payload.AddRange(BigEndian(0u));
        payload.AddRange(BigEndian((uint)(sectorCount * bytesPerSector)));
        payload.AddRange(BigEndian((uint)mode));
        payload.AddRange(BigEndian(0u));
        payload.AddRange(BigEndian(0u));

        var metadata = new List<byte>();
        metadata.AddRange("ETNF"u8.ToArray());
        metadata.AddRange(BigEndian((uint)payload.Count));
        metadata.AddRange(payload);
        metadata.AddRange("END!"u8.ToArray());
        metadata.AddRange(BigEndian(0u));

        int metadataOffset = body.Count;
        body.AddRange(metadata);
        body.AddRange("NERO"u8.ToArray());
        body.AddRange(BigEndian((uint)metadataOffset));
        File.WriteAllBytes(path, [.. body]);
    }

    private static byte[] BuildIso(int sectorCount)
    {
        var bytes = new byte[sectorCount * 2048];
        for (var lba = 0; lba < sectorCount; lba++)
        {
            ExpectedSector(lba).CopyTo(bytes.AsSpan(lba * 2048));
        }

        return bytes;
    }

    private static byte[] BuildImageSector(int lba, CdiTrackMode mode, CdiReadMode readMode, int stride)
    {
        int native = readMode switch
        {
            CdiReadMode.Mode1 => 2048,
            CdiReadMode.Mode2 => 2336,
            _ => 2352,
        };

        return BuildImageSector(lba, mode == CdiTrackMode.Mode2, native, stride);
    }

    private static byte[] BuildImageSector(int lba, NrgTrackMode mode, int stride, int native) =>
        BuildImageSector(lba, mode == NrgTrackMode.RawMode2 || mode == NrgTrackMode.RawMode2Subchannel, native, stride);

    private static byte[] BuildImageSector(
        int lba,
        bool mode2,
        int native,
        int stride)
    {
        var sector = new byte[stride];
        if (native == 2048)
        {
            ExpectedSector(lba).CopyTo(sector);
            return sector;
        }

        if (native == 2336)
        {
            ExpectedSector(lba).CopyTo(sector.AsSpan(8));
            return sector;
        }

        var raw = ExpectedRawSector(lba, mode2);
        raw.CopyTo(sector.AsSpan(0, 2352));
        return sector;
    }

    private static byte[] ExpectedRawSector(int lba, bool mode2)
    {
        var sector = new byte[2352];
        sector[0] = 0x00;
        for (var index = 1; index <= 10; index++)
        {
            sector[index] = 0xFF;
        }

        sector[11] = 0x00;
        int absoluteFrame = lba + 150;
        sector[12] = ToBcd(absoluteFrame / (60 * 75));
        sector[13] = ToBcd((absoluteFrame / 75) % 60);
        sector[14] = ToBcd(absoluteFrame % 75);
        sector[15] = (byte)(mode2 ? 2 : 1);
        if (mode2)
        {
            sector[16] = 0x00;
            sector[17] = 0x00;
            sector[18] = 0x08;
            sector[19] = 0x00;
            ExpectedSector(lba).CopyTo(sector.AsSpan(24));
        }
        else
        {
            ExpectedSector(lba).CopyTo(sector.AsSpan(16));
        }

        return sector;
    }

    private static byte[] ExpectedSector(int lba)
    {
        var sector = new byte[2048];
        for (var index = 0; index < sector.Length; index += 4)
        {
            BinaryPrimitives.WriteUInt32BigEndian(
                sector.AsSpan(index),
                (uint)((lba * 0x01000193) ^ index));
        }

        return sector;
    }

    private static byte[] ExpectedAudio(int lba)
    {
        var sector = new byte[2352];
        for (var index = 0; index < sector.Length; index++)
        {
            sector[index] = (byte)((lba * 17) + index + 0x80);
        }

        return sector;
    }

    private static byte ToBcd(int value) => (byte)(((value / 10) << 4) | (value % 10));

    private static byte[] LittleEndian(ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] LittleEndian(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] BigEndian(ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] BigEndian(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] BigEndian(ulong value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private sealed class ImageFolder : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(),
            $"fivedo-cdi-nrg-{Guid.NewGuid():N}");

        public ImageFolder() => Directory.CreateDirectory(_directory);

        public string PathFor(string name) => Path.Combine(_directory, name);

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
