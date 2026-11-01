using System;
using System.Collections.Generic;
using FiveDo.Cdrom;
using FiveDo.Core;
using FiveDo.Machine;

namespace FiveDo.Tests.Machine;

public sealed class CdAudioMixTests
{
    [Fact]
    public void MixLevelChangesThePackedOutputWhilePlaybackIsActive()
    {
        int[] full = Capture(volume: 1, muted: false);
        int[] silent = Capture(volume: 1, muted: true);

        Assert.NotEmpty(full);
        Assert.Equal(full.Length, silent.Length);
        Assert.NotEqual(full, silent);

        int[] zero = Capture(volume: 0, muted: false);
        Assert.Equal(silent, zero);
    }

    [Fact]
    public void CdAudioMixSettingsRoundtripThroughTheCdState()
    {
        var machine = new FiveDoMachine { CdAudioVolume = 0.25, CdAudioMuted = true };
        using var state = new System.IO.MemoryStream();
        machine.SaveState(state, "disc");

        var loaded = new FiveDoMachine();
        state.Position = 0;
        loaded.LoadState(state);

        Assert.Equal(0.25, loaded.CdAudioVolume);
        Assert.True(loaded.CdAudioMuted);
    }

    private static int[] Capture(double volume, bool muted)
    {
        var machine = new FiveDoMachine
        {
            CdAudioVolume = volume,
            CdAudioMuted = muted,
        };
        var sink = new RecordingSink();
        machine.AudioSink = sink;
        machine.InsertDisc(new TrackedDisc());
        Send(machine.Cdrom, 0x0F, 0x02, 0x00, 0x02);
        machine.RunFrames(2);
        return sink.Samples.ToArray();
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

    private sealed class RecordingSink : IAudioSink
    {
        internal List<int> Samples { get; } = [];

        public void PushSample(uint sample)
        {
            Samples.Add(unchecked((int)sample));
        }

        public void Reset()
        {
            Samples.Clear();
        }
    }

    private sealed class TrackedDisc : ITrackedDiscSource
    {
        private const int DataSectors = 4;
        private const int AudioSectors = 2;

        public IReadOnlyList<DiscTrack> Tracks { get; } =
        [
            new DiscTrack(1, DiscTrackKind.Data, 0, DataSectors, 2048),
            new DiscTrack(2, DiscTrackKind.Audio, DataSectors, AudioSectors, 2352),
        ];

        public int TotalSectorCount => DataSectors + AudioSectors;

        public int SectorCount => TotalSectorCount;

        public int SectorSize => 2048;

        public void ReadSector(int lba, Span<byte> destination)
        {
            destination[..2048].Clear();
        }

        public int ReadTrackSector(int lba, Span<byte> destination)
        {
            if (lba < DataSectors)
            {
                destination[..2048].Clear();
                return 2048;
            }

            Span<byte> audio = destination[..2352];
            for (var i = 0; i < audio.Length; i += 2)
            {
                audio[i] = (byte)(i + 0x40);
                audio[i + 1] = 0x40;
            }

            return 2352;
        }

        public void Dispose()
        {
        }
    }
}
