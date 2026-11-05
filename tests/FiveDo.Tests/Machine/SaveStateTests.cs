using System;
using System.IO;
using System.Linq;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Machine;
using FiveDo.Tests.TestUtilities;
using FiveDo.Video;

namespace FiveDo.Tests.Machine;

public sealed class SaveStateTests
{
    [Fact]
    public void SaveLoad_RunAheadMatchesControlRun()
    {
        FiveDoMachine first = CreateMachine();
        first.RunFrames(5);

        using var state = new MemoryStream();
        first.SaveState(state, "disc");
        first.RunFrames(60);
        (uint Hash, uint ProgramCounter, long FramesExecuted) expected = Snapshot(first);

        FiveDoMachine loaded = CreateMachine();
        state.Position = 0;
        loaded.LoadState(state);
        loaded.RunFrames(60);
        (uint Hash, uint ProgramCounter, long FramesExecuted) actual = Snapshot(loaded);

        Assert.Equal(expected.Hash, actual.Hash);
        Assert.Equal(expected.ProgramCounter, actual.ProgramCounter);
        Assert.Equal(expected.FramesExecuted, actual.FramesExecuted);
        Assert.Empty(loaded.StateWarnings);
    }

    [Fact]
    public void LoadState_FlushesQueuedAudio()
    {
        FiveDoMachine source = CreateMachine();
        source.RunFrames(2);
        using var state = new MemoryStream();
        source.SaveState(state, "disc");

        FiveDoMachine loaded = CreateMachine();
        var sink = new RecordingAudioSink();
        loaded.AudioSink = sink;
        state.Position = 0;

        loaded.LoadState(state);

        Assert.Equal(1, sink.ResetCount);
    }

    [Fact]
    public void LoadState_RestoresTheParsedFrameImmediately()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(2);

        VdlLine source = machine.Frame[3];
        source.Width = 64;
        source.OutControl = 0x12345678;
        source.ClutDma = 0x9ABCDEF0;
        source.Background = 0x0BADF00D;
        for (var i = 0; i < VdlLine.MaxPixels; i++)
        {
            source.PixelBuffer[i] = (ushort)((i * 7) + 3);
        }

        source.ClutB[0] = 1;
        source.ClutG[1] = 2;
        source.ClutR[2] = 3;

        using var state = new MemoryStream();
        machine.SaveState(state);

        FiveDoMachine loaded = CreateMachine();
        state.Position = 0;
        loaded.LoadState(state);

        VdlLine restored = loaded.Frame[3];
        Assert.Equal(64, restored.Width);
        Assert.Equal(0x12345678u, restored.OutControl);
        Assert.Equal(0x9ABCDEF0u, restored.ClutDma);
        Assert.Equal(0x0BADF00Du, restored.Background);
        Assert.Equal(1, restored.ClutB[0]);
        Assert.Equal(2, restored.ClutG[1]);
        Assert.Equal(3, restored.ClutR[2]);
        Assert.Equal(source.PixelBuffer.ToArray(), restored.PixelBuffer.ToArray());
    }

    [Fact]
    public void LoadState_RejectsTruncatedData()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(2);
        using var state = new MemoryStream();
        machine.SaveState(state);
        var bytes = state.ToArray();

        Assert.Throws<InvalidStateException>(() => machine.LoadState(new MemoryStream(bytes[..20])));
        Assert.Throws<InvalidStateException>(() => machine.LoadState(new MemoryStream([])));
    }

    [Fact]
    public void LoadState_RejectsInvalidMagic()
    {
        FiveDoMachine machine = CreateMachine();
        using var stream = new MemoryStream(new byte[64]);
        Assert.Throws<InvalidStateException>(() => machine.LoadState(stream));
    }

    [Fact]
    public void LoadState_WarnsOnBiosHashMismatch()
    {
        FiveDoMachine writer = CreateMachine();
        writer.RunFrames(2);
        using var state = new MemoryStream();
        writer.SaveState(state);

        var otherBios = new byte[EmulatorConstants.RomBankSize];
        otherBios[0] = 1;
        FiveDoMachine reader = CreateMachine(otherBios);
        state.Position = 0;
        reader.LoadState(state);

        Assert.Contains(reader.StateWarnings, warning => warning.Contains("BIOS", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadState_WarnsOnDiscHashMismatch()
    {
        FiveDoMachine writer = CreateMachine();
        writer.RunFrames(2);
        using var state = new MemoryStream();
        writer.SaveState(state, "disc-a");

        FiveDoMachine reader = CreateMachine();
        state.Position = 0;
        reader.LoadState(state, "disc-b");

        Assert.Contains(reader.StateWarnings, warning => warning.Contains("disc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadState_DoesNotWarnOnMatchingDiscHash()
    {
        FiveDoMachine writer = CreateMachine();
        writer.RunFrames(2);
        using var state = new MemoryStream();
        writer.SaveState(state, "disc-a");

        FiveDoMachine reader = CreateMachine();
        state.Position = 0;
        reader.LoadState(state, "disc-a");

        Assert.Empty(reader.StateWarnings);
    }

    [Fact]
    public void LoadState_DoesNotWarnWhenEitherDiscHashIsEmpty()
    {
        FiveDoMachine writer = CreateMachine();
        writer.RunFrames(2);
        using var stateWithoutDisc = new MemoryStream();
        writer.SaveState(stateWithoutDisc);
        using var stateWithDisc = new MemoryStream();
        writer.SaveState(stateWithDisc, "disc-a");

        FiveDoMachine reader = CreateMachine();
        stateWithoutDisc.Position = 0;
        reader.LoadState(stateWithoutDisc, "disc-a");
        Assert.Empty(reader.StateWarnings);

        stateWithDisc.Position = 0;
        reader.LoadState(stateWithDisc);
        Assert.Empty(reader.StateWarnings);
    }

    [Fact]
    public void SaveStateToFile_IsAtomicAndRoundtrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-state-{Guid.NewGuid():N}.tdos");
        try
        {
            FiveDoMachine machine = CreateMachine();
            machine.RunFrames(3);
            machine.SaveStateToFile(path);

            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));
            FiveDoMachine loaded = CreateMachine();
            loaded.LoadStateFromFile(path);
            Assert.Equal(machine.Cpu.ProgramCounter, loaded.Cpu.ProgramCounter);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SaveState_CompressesLargeSectionsAndRoundtrips()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(3);

        using var compressed = new MemoryStream();
        machine.SaveState(compressed);
        using var uncompressed = new MemoryStream();
        machine.SaveState(uncompressed, compress: false);

        Assert.True(compressed.Length < uncompressed.Length / 2);

        var bytes = compressed.ToArray();
        StateInfo info = StateInspector.Read(new MemoryStream(bytes));
        Assert.Equal(StateFormat.Version, info.Version);
        Assert.True(info.Compressed);
        Assert.Equal(bytes.Length, info.StoredLength);
        Assert.Contains(info.Sections, section => section.Compressed && section.StoredLength < section.PayloadLength);

        FiveDoMachine loaded = CreateMachine();
        loaded.LoadState(new MemoryStream(bytes));
        Assert.Equal(machine.Cpu.ProgramCounter, loaded.Cpu.ProgramCounter);
    }

    [Fact]
    public void SaveState_UncompressedSectionsRoundtrip()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(3);
        using var state = new MemoryStream();
        machine.SaveState(state, compress: false);

        FiveDoMachine loaded = CreateMachine();
        state.Position = 0;
        loaded.LoadState(state);

        Assert.Equal(machine.Cpu.ProgramCounter, loaded.Cpu.ProgramCounter);
        Assert.Empty(loaded.StateWarnings);
    }

    [Fact]
    public void Machine_AppliesConfiguredRandomSeed()
    {
        FiveDoMachine machine = CreateMachine(tuning: new EmulatorTuning { RandomSeed = 0x1234 });

        Assert.Equal(0x1234u, machine.Dsp.Seed);
    }

    [Fact]
    public void SaveState_RecordsArmTimingAndWarnsOnMismatch()
    {
        var arm60 = new EmulatorTuning { ArmTiming = ArmTimingMode.Arm60Datasheet };
        FiveDoMachine machine = CreateMachine(tuning: arm60);
        machine.RunFrames(2);
        using var state = new MemoryStream();
        machine.SaveState(state);

        StateInfo info = StateInspector.Read(new MemoryStream(state.ToArray()));
        Assert.Contains(info.Sections, section => section.Id == StateSectionIds.Options);

        FiveDoMachine matching = CreateMachine(tuning: arm60);
        state.Position = 0;
        matching.LoadState(state);
        Assert.DoesNotContain(matching.StateWarnings, warning => warning.Contains("ARM timing", StringComparison.Ordinal));

        FiveDoMachine freeDo = CreateMachine();
        state.Position = 0;
        freeDo.LoadState(state);
        Assert.Contains(
            freeDo.StateWarnings,
            warning => warning.Contains("ARM timing", StringComparison.Ordinal)
                && warning.Contains("Arm60Datasheet", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveState_RecordsClioTimersAndWarnsOnMismatch()
    {
        var operaTimers = new EmulatorTuning { ClioTimers = ClioTimerMode.OperaScheduler };
        FiveDoMachine machine = CreateMachine(tuning: operaTimers);
        machine.RunFrames(2);
        using var state = new MemoryStream();
        machine.SaveState(state);

        FiveDoMachine matching = CreateMachine(tuning: operaTimers);
        state.Position = 0;
        matching.LoadState(state);
        Assert.DoesNotContain(
            matching.StateWarnings,
            warning => warning.Contains("CLIO timer", StringComparison.Ordinal));

        FiveDoMachine freeDo = CreateMachine();
        state.Position = 0;
        freeDo.LoadState(state);
        Assert.Contains(
            freeDo.StateWarnings,
            warning => warning.Contains("CLIO timer", StringComparison.Ordinal)
                && warning.Contains("OperaScheduler", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveState_RecordsCelTimingAndWarnsOnMismatch()
    {
        var operaCel = new EmulatorTuning { CelTiming = CelTimingMode.OperaBusy };
        FiveDoMachine machine = CreateMachine(tuning: operaCel);
        machine.RunFrames(2);
        using var state = new MemoryStream();
        machine.SaveState(state);

        FiveDoMachine matching = CreateMachine(tuning: operaCel);
        state.Position = 0;
        matching.LoadState(state);
        Assert.DoesNotContain(
            matching.StateWarnings,
            warning => warning.Contains("CEL timing", StringComparison.Ordinal));

        FiveDoMachine freeDo = CreateMachine();
        state.Position = 0;
        freeDo.LoadState(state);
        Assert.Contains(
            freeDo.StateWarnings,
            warning => warning.Contains("CEL timing", StringComparison.Ordinal)
                && warning.Contains("OperaBusy", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveState_RecordsCdromTimingAndWarnsOnMismatch()
    {
        var operaCd = new EmulatorTuning { CdromTiming = CdromTimingMode.OperaCycleBased };
        FiveDoMachine machine = CreateMachine(tuning: operaCd);
        machine.RunFrames(2);
        using var state = new MemoryStream();
        machine.SaveState(state);

        FiveDoMachine matching = CreateMachine(tuning: operaCd);
        state.Position = 0;
        matching.LoadState(state);
        Assert.DoesNotContain(
            matching.StateWarnings,
            warning => warning.Contains("CD-ROM timing", StringComparison.Ordinal));

        FiveDoMachine freeDo = CreateMachine();
        state.Position = 0;
        freeDo.LoadState(state);
        Assert.Contains(
            freeDo.StateWarnings,
            warning => warning.Contains("CD-ROM timing", StringComparison.Ordinal)
                && warning.Contains("OperaCycleBased", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveState_RecordsCdSpeedAndWarnsOnMismatch()
    {
        var fastCd = new EmulatorTuning
        {
            CdromTiming = CdromTimingMode.OperaCycleBased,
            CdSpeed = CdSpeed.X8,
        };
        FiveDoMachine machine = CreateMachine(tuning: fastCd);
        machine.RunFrames(2);
        using var state = new MemoryStream();
        machine.SaveState(state);

        FiveDoMachine matching = CreateMachine(tuning: fastCd);
        state.Position = 0;
        matching.LoadState(state);
        Assert.DoesNotContain(
            matching.StateWarnings,
            warning => warning.Contains("CD-ROM speed", StringComparison.Ordinal));

        FiveDoMachine stock = CreateMachine();
        state.Position = 0;
        stock.LoadState(state);
        Assert.Contains(
            stock.StateWarnings,
            warning => warning.Contains("CD-ROM speed", StringComparison.Ordinal)
                && warning.Contains("X8", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveState_RoundTripsEveryRegisteredMachineOption()
    {
        var tuning = new EmulatorTuning
        {
            ArmClock = 25_000_000,
            Region = VideoRegion.Pal1,
            ArmTiming = ArmTimingMode.Arm60Datasheet,
            ClioTimers = ClioTimerMode.OperaScheduler,
            CelTiming = CelTimingMode.OperaBusy,
            CdromTiming = CdromTimingMode.OperaCycleBased,
            CdSpeed = CdSpeed.X8,
            MemoryConfiguration = MemoryConfiguration.Dram8MbVram2Mb,
            RandomSeed = 0x1234_5678,
            FixMode = FixMode.BitTiming5,
            HighResolution = true,
            OperaDipir = true,
            OperaSoftReset = true,
            SwiHle = true,
        };
        FiveDoMachine machine = CreateMachine(tuning: tuning);
        machine.RunFrames(2);
        using var state = new MemoryStream();
        machine.SaveState(state);

        FiveDoMachine matching = CreateMachine(tuning: tuning);
        state.Position = 0;
        matching.LoadState(state);
        Assert.DoesNotContain(
            matching.StateWarnings,
            warning => warning.Contains("differs from the machine", StringComparison.Ordinal));

        // The default machine differs in every non-default option. Every registry entry
        // must therefore produce exactly one options warning or region-switch warning.
        FiveDoMachine defaults = CreateMachine();
        state.Position = 0;
        defaults.LoadState(state);
        foreach (MachineOptionDefinition definition in MachineOptionRegistry.Options)
        {
            // The interpreter is the only supported backend, so that value cannot differ.
            // The fix mask is already stored in the state header and the Machine section;
            // loading it makes the options-section value match by construction.
            if (string.Equals(definition.Key, "cpuBackend", StringComparison.Ordinal)
                || string.Equals(definition.Key, "fixMode", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.True(
                defaults.StateWarnings.Any(warning =>
                    warning.Contains(definition.DisplayName, StringComparison.Ordinal)),
                $"No warning for {definition.Key} ({definition.DisplayName}): "
                    + string.Join(" | ", defaults.StateWarnings));
        }
    }

    [Fact]
    public void LoadState_AcceptsVersionTwoStates()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(2);
        using var state = new MemoryStream();
        machine.SaveState(state);
        byte[] legacy = StateTestHelper.DowngradeToVersion(state.ToArray(), version: 2);

        FiveDoMachine loaded = CreateMachine();
        loaded.LoadState(new MemoryStream(legacy));

        Assert.Equal(machine.Cpu.ProgramCounter, loaded.Cpu.ProgramCounter);
    }

    [Fact]
    public void LoadState_AcceptsVersionOneStates()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(2);
        using var state = new MemoryStream();
        machine.SaveState(state, compress: false);
        byte[] legacy = StateTestHelper.DowngradeToVersion(state.ToArray(), version: 1);

        FiveDoMachine loaded = CreateMachine();
        loaded.LoadState(new MemoryStream(legacy));

        Assert.Equal(machine.Cpu.ProgramCounter, loaded.Cpu.ProgramCounter);
    }

    [Fact]
    public void LoadState_RejectsUnsupportedVersion()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(2);
        using var state = new MemoryStream();
        machine.SaveState(state);
        var bytes = state.ToArray();
        bytes[4] = 4;
        bytes[5] = 0;

        Assert.Throws<InvalidStateException>(() => machine.LoadState(new MemoryStream(bytes)));
    }

    [Fact]
    public void SaveState_WritesVersionThreeFeatureFlagsAndSectionTable()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(2);
        using var state = new MemoryStream();
        machine.SaveState(state);
        state.Position = 0;

        StateInfo info = StateInspector.ReadHeader(state);

        Assert.Equal(StateFormat.Version, info.Version);
        Assert.Equal(12, info.Sections.Count);
        Assert.Contains(info.Sections, section => section.Id == StateSectionIds.Options);
        Assert.Contains(info.Sections, section => section.Id == StateSectionIds.Machine);
    }

    [Fact]
    public void LoadState_UnknownSectionPolicyWarnsOrRejects()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(2);
        using var state = new MemoryStream();
        machine.SaveState(state);
        byte[] combined = StateTestHelper.AppendDeclaredSection(state.ToArray(), new UnknownSection());

        FiveDoMachine rejecting = CreateMachine();
        rejecting.UnknownSectionPolicy = StateUnknownSectionPolicy.Reject;
        Assert.Throws<InvalidStateException>(() => rejecting.LoadState(new MemoryStream(combined)));

        FiveDoMachine warning = CreateMachine();
        warning.LoadState(new MemoryStream(combined));

        Assert.Contains(
            warning.StateWarnings,
            message => message.Contains("unknown state section", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SaveLoad_AfterDiscSwapRestoresTheNewDriveState()
    {
        FiveDoMachine machine = CreateMachine();
        using var first = new FakeDisc(64);
        using var second = new FakeDisc(96);
        machine.InsertDisc(first);
        machine.RunFrames(3);
        machine.InsertDisc(second);
        machine.RunFrames(2);

        using var state = new MemoryStream();
        machine.SaveState(state, "disc-b");
        state.Position = 0;
        machine.LoadState(state, "disc-b");

        Assert.Empty(machine.StateWarnings);
        Assert.Same(second, machine.Cdrom.Disc);
        Assert.True(machine.Cdrom.DiscPresent);
        Assert.False(machine.Cdrom.TrayOpen);
        Assert.Equal(96, machine.Cdrom.Disc?.SectorCount);
    }

    private static FiveDoMachine CreateMachine(byte[]? rom = null, EmulatorTuning? tuning = null)
    {
        var machine = new FiveDoMachine(tuning);
        machine.LoadBios(rom ?? BuildSyntheticRom());
        machine.Reset();
        return machine;
    }

    private static (uint Hash, uint ProgramCounter, long FramesExecuted) Snapshot(FiveDoMachine machine)
    {
        var buffer = new FrameBuffer(320, 240);
        FrameAssembler.Assemble(machine.Frame, buffer, 320, 240);
        var hash = 2166136261u;
        foreach (var value in buffer.PixelSpan)
        {
            hash = unchecked((hash ^ value) * 16777619);
        }

        return (hash, machine.Cpu.ProgramCounter, machine.FramesExecuted);
    }

    private sealed class UnknownSection : IStateSection
    {
        public uint Id => 0xDEADBEEF;

        public ushort Version => 1;

        public void Save(IStateWriter writer) => writer.WriteByte(0x5A);

        public void Load(IStateReader reader) => _ = reader.ReadByte();
    }

    private sealed class FakeDisc : IDiscSource
    {
        private readonly int _sectorCount;

        internal FakeDisc(int sectorCount)
        {
            _sectorCount = sectorCount;
        }

        public int SectorCount => _sectorCount;

        public int SectorSize => 2048;

        public void ReadSector(int lba, Span<byte> destination)
        {
            destination.Clear();
        }

        public void Dispose()
        {
        }
    }

    private static byte[] BuildSyntheticRom()
    {
        var program = new[]
        {
            ArmTestAssembler.Mov(0, 0x03300000),
            ArmTestAssembler.Mov(1, 0xb0000),
            ArmTestAssembler.StrImmediate(1, 0, 0x580),
            ArmTestAssembler.Mov(2, 0x03200000),
            ArmTestAssembler.AddImmediate(2, 2, 0x2000),
            ArmTestAssembler.Mvn(3, 0),
            ArmTestAssembler.StrImmediate(3, 2),
            ArmTestAssembler.Branch(0x03000018, 0x03000018),
        };

        var rom = new byte[EmulatorConstants.RomBankSize];
        for (var i = 0; i < program.Length; i++)
        {
            var offset = i * 4;
            var word = program[i];
            rom[offset] = (byte)(word >> 24);
            rom[offset + 1] = (byte)(word >> 16);
            rom[offset + 2] = (byte)(word >> 8);
            rom[offset + 3] = (byte)word;
        }

        return rom;
    }
}
