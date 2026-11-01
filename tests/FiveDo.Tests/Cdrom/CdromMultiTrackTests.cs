using System;
using System.Collections.Generic;
using FiveDo.Cdrom;
using FiveDo.Core;

namespace FiveDo.Tests.Cdrom;

public sealed class CdromMultiTrackTests
{
    [Fact]
    public void DiscInfoReportsEveryTrackAndTheLeadOut()
    {
        var cd = new CdromDevice();
        cd.InsertDisc(new TrackedMemoryDisc(dataSectors: 4, audioSectors: 2));

        Send(cd, 0x8B);
        byte[] info = ReadStatus(cd, 8);
        Assert.Equal(0x8B, info[0]);
        Assert.Equal(1, info[2]);
        Assert.Equal(2, info[3]);
        Assert.Equal(0x00, info[4]);
        Assert.Equal(0x02, info[5]);
        Assert.Equal(0x06, info[6]);

        Send(cd, 0x8C, 0x00, 0x02);
        byte[] track = ReadStatus(cd, 10);
        Assert.Equal(0x8C, track[0]);
        Assert.Equal(0x00, track[2]);
        Assert.Equal(0x02, track[3]);
        Assert.Equal(0x00, track[5]);
        Assert.Equal(0x02, track[6]);
        Assert.Equal(0x04, track[7]);

        Send(cd, 0x8C, 0x00, 0xAA);
        byte[] leadOut = ReadStatus(cd, 10);
        Assert.Equal(0xAA, leadOut[3]);
        Assert.Equal(0x02, leadOut[6]);
        Assert.Equal(0x06, leadOut[7]);
    }

    [Fact]
    public void ModeSetToDigitalAudioReadsRawAudioSectors()
    {
        var disc = new TrackedMemoryDisc(dataSectors: 4, audioSectors: 2);
        var cd = new CdromDevice();
        cd.InsertDisc(disc);

        // MODE SET page 0: density 0x82 (digital audio), block length 2352.
        Send(cd, 0x09, 0x00, 0x82, 0x09, 0x30);
        byte[] modeStatus = ReadStatus(cd, 2);
        Assert.Equal(0x09, modeStatus[0]);
        Assert.Equal(2352, cd.ReadSectorSize);

        // AUDIO track 2 starts at LBA 4 = 00:02:04.
        Send(cd, 0x10, 0x00, 0x02, 0x04, 0x00, 0x00, 0x01);
        byte[] data = ReadData(cd, 2352);
        Assert.Equal(disc.AudioSector(0), data);
    }

    [Fact]
    public void PlayAudioTrackMovesTheSubQPositionAndStopsAtTheEnd()
    {
        var cd = new CdromDevice { ArmClock = 750 };
        cd.InsertDisc(new TrackedMemoryDisc(dataSectors: 4, audioSectors: 2));

        Send(cd, 0x0F, 0x02, 0x00, 0x02);
        Assert.True(cd.IsAudioPlaying);
        Assert.Equal(4, cd.AudioPositionLba);

        Assert.True(cd.TryGetAudioSample(out AudioSample sample));
        Assert.Equal(unchecked((short)0x8180), sample.Left);
        Assert.Equal(unchecked((short)0x8382), sample.Right);

        Send(cd, 0x87);
        byte[] subQ = ReadStatus(cd, 12);
        Assert.Equal(0x87, subQ[0]);
        Assert.Equal(0x00, subQ[1]);
        Assert.Equal(0x00, subQ[2]);
        Assert.Equal(0x02, subQ[3]);
        Assert.Equal(0x01, subQ[4]);
        Assert.Equal(0x00, subQ[5]);
        Assert.Equal(0x02, subQ[6]);
        Assert.Equal(0x04, subQ[7]);

        cd.Tick(750 / 75);
        Assert.Equal(5, cd.AudioPositionLba);
        cd.Tick(750 / 75);
        Assert.False(cd.IsAudioPlaying);
    }

    [Fact]
    public void ModeSetRejectsAnOversizedBlock()
    {
        var cd = new CdromDevice();
        cd.InsertDisc(new TrackedMemoryDisc(dataSectors: 4, audioSectors: 0));

        Send(cd, 0x09, 0x00, 0x00, 0x10, 0x00);
        byte[] status = ReadStatus(cd, 2);
        Assert.Equal(0x09, status[0]);
        Assert.NotEqual(0, status[1] & 0x10);
        Assert.Equal(2048, cd.ReadSectorSize);
    }

    private static void Send(CdromDevice device, params byte[] command)
    {
        foreach (byte value in command)
        {
            device.SetCommand(value);
        }

        for (int i = command.Length; i < 7; i++)
        {
            device.SetCommand(0);
        }
    }

    private static byte[] ReadStatus(CdromDevice device, int count)
    {
        var status = new byte[count];
        for (var i = 0; i < count; i++)
        {
            status[i] = (byte)device.GetStatus();
        }

        return status;
    }

    private static byte[] ReadData(CdromDevice device, int count)
    {
        var data = new byte[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = (byte)device.GetData();
        }

        return data;
    }

    private sealed class TrackedMemoryDisc : ITrackedDiscSource
    {
        private readonly byte[][] _data;
        private readonly byte[][] _audio;

        internal TrackedMemoryDisc(int dataSectors, int audioSectors)
        {
            _data = new byte[dataSectors][];
            for (var lba = 0; lba < dataSectors; lba++)
            {
                var sector = new byte[2048];
                for (var i = 0; i < sector.Length; i++)
                {
                    sector[i] = (byte)((lba * 31) + i);
                }

                _data[lba] = sector;
            }

            _audio = new byte[audioSectors][];
            for (var lba = 0; lba < audioSectors; lba++)
            {
                var sector = new byte[2352];
                for (var i = 0; i < sector.Length; i++)
                {
                    sector[i] = (byte)((lba * 17) + i + 0x80);
                }

                _audio[lba] = sector;
            }

            var tracks = new DiscTrack[audioSectors > 0 ? 2 : 1];
            tracks[0] = new DiscTrack(1, DiscTrackKind.Data, 0, dataSectors, 2048);
            if (audioSectors > 0)
            {
                tracks[1] = new DiscTrack(2, DiscTrackKind.Audio, dataSectors, audioSectors, 2352);
            }

            Tracks = tracks;
        }

        public IReadOnlyList<DiscTrack> Tracks { get; }

        public int TotalSectorCount => _data.Length + _audio.Length;

        public int SectorCount => TotalSectorCount;

        public int SectorSize => 2048;

        public void ReadSector(int lba, Span<byte> destination)
        {
            _data[lba].CopyTo(destination);
        }

        public int ReadTrackSector(int lba, Span<byte> destination)
        {
            if (lba < _data.Length)
            {
                _data[lba].CopyTo(destination);
                return 2048;
            }

            byte[] audio = _audio[lba - _data.Length];
            audio.CopyTo(destination);
            return audio.Length;
        }

        public void Dispose()
        {
        }

        internal byte[] AudioSector(int lba)
        {
            return _audio[lba];
        }
    }
}
