using System;
using System.Linq;
using FiveDo.Cdrom;
using FiveDo.Core;

namespace FiveDo.Tests.Cdrom;

public sealed class CdromTranscriptTests
{
    [Fact]
    public void Transcript_CapturesCommandsStatusesAndPollBits()
    {
        var cd = new CdromDevice { FrameProvider = () => 100 };
        cd.InsertDisc(new MemoryDisc(8));
        var transcript = new CdromCommandTranscript();
        transcript.Attach(cd);

        Send(cd, 0x83);
        DrainStatus(cd, 12);
        Send(cd, 0x85);
        DrainStatus(cd, 8);
        Send(cd, 0x8B);
        DrainStatus(cd, 8);
        Send(cd, 0x8C, 0x00, 0x01);
        DrainStatus(cd, 10);
        Send(cd, 0x10, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01);
        DrainStatus(cd, 2);

        Assert.Equal(5, transcript.Count);
        Assert.Equal(
            new byte[] { 0x83, 0x85, 0x8B, 0x8C, 0x10 },
            transcript.Entries.Select(entry => entry.Opcode).ToArray());
        Assert.All(transcript.Entries, entry => Assert.Equal(100, entry.Frame));
        Assert.All(transcript.Entries, entry => Assert.Equal(7, entry.Command.Length));
        Assert.Equal(0x83, transcript.Entries[0].Status[0]);
        Assert.Equal(0x85, transcript.Entries[1].Status[0]);
        Assert.Equal(0x8B, transcript.Entries[2].Status[0]);
        Assert.Equal(0x8C, transcript.Entries[3].Status[0]);
        Assert.Equal(0x10, transcript.Entries[4].Status[0]);

        var text = transcript.ToText();
        Assert.Contains("f=100 cmd=83", text, StringComparison.Ordinal);
        Assert.Contains("status=83", text, StringComparison.Ordinal);
        Assert.Contains("poll=", text, StringComparison.Ordinal);
        Assert.Contains("drive=", text, StringComparison.Ordinal);
        Assert.Equal(5, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void Transcript_RecordsEjectAndInjectWithoutLosingTheTrayState()
    {
        var cd = new CdromDevice { FrameProvider = () => 7 };
        cd.InsertDisc(new MemoryDisc(4));
        var transcript = new CdromCommandTranscript();
        transcript.Attach(cd);

        Send(cd, 0x06);
        DrainStatus(cd, 2);
        Send(cd, 0x07);
        DrainStatus(cd, 2);

        Assert.Equal(2, transcript.Count);
        Assert.Equal(0x06, transcript.Entries[0].Opcode);
        Assert.Equal(0x07, transcript.Entries[1].Opcode);
        Assert.Equal(0, transcript.Entries[0].DriveStatus & 0xE0);
        Assert.Equal(0xE1, transcript.Entries[1].DriveStatus);
        Assert.True(cd.DiscPresent);
        Assert.False(cd.TrayOpen);
    }

    [Fact]
    public void Transcript_RestartsAfterReset()
    {
        var cd = new CdromDevice { FrameProvider = () => 3 };
        cd.InsertDisc(new MemoryDisc(4));
        var transcript = new CdromCommandTranscript();
        transcript.Attach(cd);

        Send(cd, 0x83);
        DrainStatus(cd, 12);
        string first = transcript.Entries[0].ToText();

        cd.Reset();
        Send(cd, 0x83);
        DrainStatus(cd, 12);

        Assert.Equal(2, transcript.Count);
        Assert.Equal(first, transcript.Entries[1].ToText());
    }

    [Fact]
    public void Detach_StopsCapturing()
    {
        var cd = new CdromDevice { FrameProvider = () => 1 };
        cd.InsertDisc(new MemoryDisc(4));
        var transcript = new CdromCommandTranscript();
        transcript.Attach(cd);
        Send(cd, 0x83);
        DrainStatus(cd, 12);
        transcript.Detach();

        Send(cd, 0x85);
        DrainStatus(cd, 8);

        Assert.Equal(1, transcript.Count);
        transcript.Clear();
        Assert.Equal(0, transcript.Count);
        Assert.Empty(transcript.ToText());
    }

    private static void Send(CdromDevice device, params byte[] command)
    {
        for (var i = 0; i < command.Length; i++)
        {
            device.SetCommand(command[i]);
        }

        // Pad the remaining command bytes so the device executes immediately.
        for (int i = command.Length; i < 7; i++)
        {
            device.SetCommand(0);
        }
    }

    private static void DrainStatus(CdromDevice device, int length)
    {
        for (var i = 0; i < length; i++)
        {
            device.GetStatus();
        }
    }

    private sealed class MemoryDisc : IDiscSource
    {
        private readonly byte[][] _sectors;

        internal MemoryDisc(int count)
        {
            _sectors = new byte[count][];
            for (var lba = 0; lba < count; lba++)
            {
                var sector = new byte[2048];
                for (var i = 0; i < sector.Length; i++)
                {
                    sector[i] = (byte)((lba * 31) + i);
                }

                _sectors[lba] = sector;
            }
        }

        public int SectorCount => _sectors.Length;

        public int SectorSize => 2048;

        public void ReadSector(int lba, Span<byte> destination)
        {
            _sectors[lba].CopyTo(destination);
        }

        public void Dispose()
        {
        }
    }
}
