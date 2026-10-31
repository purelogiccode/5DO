using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using FiveDo.Clio;
using FiveDo.Core;
using FiveDo.Cpu;
using FiveDo.Madam;
using FiveDo.Memory;
using FiveDo.Video;
using CdromChip = FiveDo.Cdrom.CdromDevice;
using ClioChip = FiveDo.Clio.Clio;
using DspCore = FiveDo.Dsp.Dsp;
using MadamChip = FiveDo.Madam.Madam;
using SportChip = FiveDo.Sport.Sport;
using XbusChip = FiveDo.Xbus.Xbus;

namespace FiveDo.Machine;

/// <summary>
/// The 3DO machine composition root: CPU, memory, CLIO, MADAM, VDLP, SPORT, XBUS, DSP stub,
/// and the Quartz scheduler.
/// </summary>
/// <remarks>
/// <para>
/// The machine is single-threaded and deterministic. It owns the reset order and the frame
/// loop ported from FreeDOCore's <c>_3do_sys.cpp</c>.
/// </para>
/// <para>
/// The CPU frame loop keeps the reference's 16-cycle batching threshold
/// (<c>if (cnt &gt;&gt; 4)</c>) and its one-event-per-batch scheduling. Do not "smooth" these
/// semantics without golden-frame evidence.
/// </para>
/// </remarks>
public sealed class FiveDoMachine
{
    private readonly VdlFrame _frame = new();

    private readonly List<string> _stateWarnings = [];
    private long _framesExecuted;
    private long _audioSamplesProduced;
    private int _lastFrameCycles;

    /// <summary>Initializes a new instance of the <see cref="FiveDoMachine"/> class.</summary>
    /// <param name="tuning">Machine tuning. Defaults to <see cref="EmulatorTuning.Default"/>.</param>
    /// <param name="compatibility">Optional shared compatibility state.</param>
    public FiveDoMachine(EmulatorTuning? tuning = null, CompatibilityState? compatibility = null)
    {
        Tuning = tuning ?? EmulatorTuning.Default;
        Tuning.Validate();
        Compatibility = compatibility ?? new CompatibilityState
        {
            FixMode = Tuning.FixMode,
            HighResolution = Tuning.HighResolution,
        };
        Compatibility.HighResolution |= Tuning.HighResolution;

        Memory = new FiveDoMemory(Tuning);
        Clio = new ClioChip(Tuning);
        Diag = new DiagPort();
        Sport = new SportChip(Memory.Ram, Compatibility);
        Vdlp = new Vdlp(Memory.Ram, Compatibility);
        Madam = new MadamChip
        {
            Vdlp = Vdlp,
            Interrupts = Clio,
            Fifo = Clio,
            Bus = Memory.Bus,
            Ram = Memory.Ram,
        };
        Madam.Compatibility = Compatibility;
        Xbus = new XbusChip { Interrupts = Clio };
        Cdrom = new CdromChip();
        Xbus.Attach(Cdrom);
        Dsp = new DspCore { Host = Clio };

        Clio.Ram = Memory.Ram;
        Clio.RomSelector = Memory;
        Clio.Xbus = Xbus;
        Clio.Dsp = Dsp;
        Clio.Madam = Madam;

        Memory.Madam = Madam;
        Memory.Clio = Clio;
        Memory.Sport = Sport;
        Memory.DiagPort = Diag;

        Cpu = new Arm60(Memory.Bus, Clio, Tuning, Compatibility);
        Quartz = new Quartz(Tuning, () => Clio.TimerDelay);

        Cdrom.ProgramCounterProvider = () => Cpu.ProgramCounter;
        Cdrom.FrameProvider = () => _framesExecuted;

        Reset();
    }

    /// <summary>Gets the machine tuning.</summary>
    public EmulatorTuning Tuning { get; }

    /// <summary>Gets the shared compatibility state.</summary>
    public CompatibilityState Compatibility { get; }

    /// <summary>Gets the memory subsystem.</summary>
    public FiveDoMemory Memory { get; }

    /// <summary>Gets the ARM60 CPU.</summary>
    public Arm60 Cpu { get; }

    /// <summary>Gets the CLIO chip.</summary>
    public ClioChip Clio { get; }

    /// <summary>Gets the MADAM chip.</summary>
    public MadamChip Madam { get; }

    /// <summary>Gets the SPORT engine.</summary>
    public SportChip Sport { get; }

    /// <summary>Gets the VDLP processor.</summary>
    public Vdlp Vdlp { get; }

    /// <summary>Gets the XBUS controller.</summary>
    public XbusChip Xbus { get; }

    /// <summary>Gets the CD-ROM XBUS device.</summary>
    public CdromChip Cdrom { get; }

    /// <summary>Gets the DSP backend. M5 replaces the stub with the full core.</summary>
    public DspCore Dsp { get; }

    /// <summary>Gets the diagnostic port.</summary>
    public DiagPort Diag { get; }

    /// <summary>Gets the Quartz scheduler.</summary>
    public Quartz Quartz { get; }

    /// <summary>Gets the reusable frame produced by the VDLP.</summary>
    public VdlFrame Frame => _frame;

    /// <summary>Gets the number of frames executed since the last reset.</summary>
    public long FramesExecuted => _framesExecuted;

    /// <summary>Gets the ARM cycles consumed by the most recent frame.</summary>
    public int LastFrameCycles => _lastFrameCycles;

    /// <summary>Gets the number of DSP samples produced since the last reset.</summary>
    /// <remarks>
    /// The count advances even when no <see cref="AudioSink"/> is attached. The host uses its
    /// delta to pace frames against the 44.1 kHz audio clock.
    /// </remarks>
    public long AudioSamplesProduced => _audioSamplesProduced;

    /// <summary>Gets warnings produced by the most recent state load.</summary>
    public IReadOnlyList<string> StateWarnings => _stateWarnings;

    /// <summary>Gets or sets the frame sink. The frame is reused after the callback returns.</summary>
    public IFrameSink? FrameSink { get; set; }

    /// <summary>Gets or sets the PBUS controller source.</summary>
    public IPbusSource? PbusSource { get; set; }

    /// <summary>Gets or sets the audio sample sink.</summary>
    public IAudioSink? AudioSink { get; set; }

    /// <summary>Loads BIOS1 and optional BIOS2 and applies Anvil detection. FreeDO: <c>_3do_Init</c>.</summary>
    /// <param name="bios1">Exactly 1 MB of BIOS1 data.</param>
    /// <param name="bios2">Optional BIOS2 data, at most 1 MB. Missing bytes are zero-filled.</param>
    /// <exception cref="ArgumentException">Thrown when the BIOS sizes are invalid.</exception>
    public void LoadBios(ReadOnlySpan<byte> bios1, ReadOnlySpan<byte> bios2 = default)
    {
        if (bios1.Length != EmulatorConstants.RomBankSize)
        {
            throw new ArgumentException($"BIOS1 must be exactly {EmulatorConstants.RomBankSize} bytes.", nameof(bios1));
        }

        if (bios2.Length > EmulatorConstants.RomBankSize)
        {
            throw new ArgumentException($"BIOS2 must be at most {EmulatorConstants.RomBankSize} bytes.", nameof(bios2));
        }

        Memory.LoadBios(bios1);
        if (!bios2.IsEmpty)
        {
            var bank = new byte[EmulatorConstants.RomBankSize];
            bios2.CopyTo(bank);
            Memory.LoadRom(1, bank);
        }

        Compatibility.IsAnvil = bios1[28] == 234 ? 30 : 0;
        Compatibility.BiosAnvil = Compatibility.IsAnvil == 30;
    }

    /// <summary>Inserts a disc into the CD-ROM drive. The machine does not take ownership.</summary>
    /// <param name="disc">Disc source.</param>
    public void InsertDisc(IDiscSource disc)
    {
        Cdrom.InsertDisc(disc);
    }

    /// <summary>Ejects the disc from the CD-ROM drive.</summary>
    public void EjectDisc()
    {
        Cdrom.EjectDisc();
    }

    /// <summary>Loads a 32 KB NVRAM image.</summary>
    /// <param name="nvram">Exactly 32 KB of NVRAM data.</param>
    public void LoadNvram(ReadOnlySpan<byte> nvram)
    {
        Memory.LoadNvram(nvram);
    }

    /// <summary>Copies the current NVRAM image.</summary>
    /// <param name="destination">Destination span of at least 32 KB.</param>
    public void CopyNvram(Span<byte> destination)
    {
        Memory.CopyNvram(destination);
    }

    /// <summary>Resets every chip in the FreeDOCore initialization order.</summary>
    public void Reset()
    {
        Cpu.Reset();
        Memory.Reset();
        Memory.SelectRomBank(0);
        Vdlp.Reset();
        Sport.Reset();
        Madam.Reset();
        Xbus.Reset();
        Clio.Reset();
        Dsp.Reset();
        Diag.Reset(-1);
        Quartz.Reset();
        _frame.Clear();
        _framesExecuted = 0;
        _audioSamplesProduced = 0;
        AudioSink?.Reset();

        if (Compatibility.IsAnvil == 30)
        {
            Compatibility.BiosAnvil = true;
        }
    }

    /// <summary>Saves the complete machine state to a stream. FreeDO: <c>_3do_Save</c>.</summary>
    /// <param name="destination">Destination stream.</param>
    /// <param name="discHash">Optional game/disc hash stored in the state header.</param>
    /// <param name="compress">When true, large section payloads are Brotli-compressed.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA5351:Do Not Use Broken Cryptographic Algorithms",
        Justification = "MD5 is the 3DO BIOS/game identifier used by 4DO.")]
    public void SaveState(Stream destination, string? discHash = null, bool compress = true)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var writer = new StateWriter { CompressionEnabled = compress };
        writer.WriteUInt32(StateFormat.Magic);
        writer.WriteUInt16(StateFormat.Version);
        writer.WriteUInt16(0);
        writer.WriteString(FiveDoVersion.Version);
        writer.WriteInt32(Tuning.ArmClock);
        writer.WriteUInt32((uint)Compatibility.FixMode);
        writer.WriteString(ComputeBiosHash());
        writer.WriteString(discHash ?? string.Empty);
        writer.WriteInt64(_framesExecuted);

        writer.WriteSection(Cpu);
        writer.WriteSection(Memory);
        writer.WriteSection(Madam);
        writer.WriteSection(Clio);
        writer.WriteSection(Dsp);
        writer.WriteSection(Sport);
        writer.WriteSection(Vdlp);
        writer.WriteSection(Xbus);
        writer.WriteSection(Cdrom);
        writer.WriteSection(Quartz);
        writer.WriteSection(new MachineFrameSection(_frame, Compatibility));

        var bytes = writer.ToArray();
        destination.Write(bytes, 0, bytes.Length);
    }

    /// <summary>Saves the machine state atomically to a file.</summary>
    /// <param name="path">Destination path.</param>
    /// <param name="discHash">Optional game/disc hash.</param>
    /// <param name="compress">When true, large section payloads are Brotli-compressed.</param>
    public void SaveStateToFile(string path, string? discHash = null, bool compress = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var temporary = path + ".tmp";
        using (FileStream stream = File.Create(temporary))
        {
            SaveState(stream, discHash, compress);
        }

        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Loads a complete machine state from a stream.</summary>
    /// <param name="source">Source stream.</param>
    /// <param name="expectedDiscHash">
    /// Optional game/disc hash of the disc currently inserted. When both this value and the
    /// state header's disc hash are present and differ, a warning is added to
    /// <see cref="StateWarnings"/> instead of rejecting the state.
    /// </param>
    /// <exception cref="InvalidStateException">Thrown when the state is invalid or incomplete.</exception>
    public void LoadState(Stream source, string? expectedDiscHash = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var reader = new StateReader(bytes);
        _stateWarnings.Clear();

        if (reader.Remaining < 8)
        {
            throw new InvalidStateException("The save state is truncated.");
        }

        if (reader.ReadUInt32() != StateFormat.Magic)
        {
            throw new InvalidStateException("The save state has an invalid magic value.");
        }

        var version = reader.ReadUInt16();
        if (!StateFormat.IsSupportedVersion(version))
        {
            throw new InvalidStateException($"Unsupported save-state version {version}.");
        }

        _ = reader.ReadUInt16();
        var emulatorVersion = reader.ReadString();
        var armClock = reader.ReadInt32();
        var fixMode = reader.ReadUInt32();
        var biosHash = reader.ReadString();
        var discHash = reader.ReadString();
        var frames = reader.ReadInt64();

        if (!string.Equals(emulatorVersion, FiveDoVersion.Version, StringComparison.Ordinal))
        {
            _stateWarnings.Add($"State was written by version {emulatorVersion}.");
        }

        if (armClock != Tuning.ArmClock)
        {
            _stateWarnings.Add($"State ARM clock {armClock} differs from the machine clock {Tuning.ArmClock}.");
        }

        Compatibility.FixMode = (FixMode)fixMode;
        var currentBios = ComputeBiosHash();
        if (!string.Equals(biosHash, currentBios, StringComparison.OrdinalIgnoreCase))
        {
            _stateWarnings.Add("State BIOS hash differs from the loaded BIOS.");
        }

        if (!string.IsNullOrEmpty(discHash)
            && !string.IsNullOrEmpty(expectedDiscHash)
            && !string.Equals(discHash, expectedDiscHash, StringComparison.OrdinalIgnoreCase))
        {
            _stateWarnings.Add("State disc hash differs from the loaded disc.");
        }

        var required = new HashSet<uint>
        {
            StateSectionIds.Cpu,
            StateSectionIds.Memory,
            StateSectionIds.Madam,
            StateSectionIds.Clio,
            StateSectionIds.Dsp,
            StateSectionIds.Sport,
            StateSectionIds.Vdlp,
            StateSectionIds.Xbus,
            StateSectionIds.Cdrom,
            StateSectionIds.Quartz,
            StateSectionIds.Machine,
        };

        while (reader.TryReadSection(out StateSectionReader? section))
        {
            var supportedVersion = section.Version == 1
                || (section.Id == StateSectionIds.Quartz && section.Version == 2);
            if (!supportedVersion)
            {
                throw new InvalidStateException($"Unsupported section {section.Id} version {section.Version}.");
            }

            switch (section.Id)
            {
                case StateSectionIds.Cpu:
                    Cpu.Load(section);
                    break;
                case StateSectionIds.Memory:
                    Memory.Load(section);
                    break;
                case StateSectionIds.Madam:
                    Madam.Load(section);
                    break;
                case StateSectionIds.Clio:
                    Clio.Load(section);
                    break;
                case StateSectionIds.Dsp:
                    Dsp.Load(section);
                    break;
                case StateSectionIds.Sport:
                    Sport.Load(section);
                    break;
                case StateSectionIds.Vdlp:
                    Vdlp.Load(section);
                    break;
                case StateSectionIds.Xbus:
                    Xbus.Load(section);
                    break;
                case StateSectionIds.Cdrom:
                    Cdrom.Load(section);
                    break;
                case StateSectionIds.Quartz:
                    Quartz.Load(section);
                    break;
                case StateSectionIds.Machine:
                    new MachineFrameSection(_frame, Compatibility).Load(section);
                    break;
                default:
                    _stateWarnings.Add($"Ignored unknown state section {section.Id}.");
                    break;
            }

            required.Remove(section.Id);
        }

        if (required.Count > 0)
        {
            throw new InvalidStateException($"The save state is missing sections: {string.Join(", ", required)}.");
        }

        // A command-line/front-end override survives loads of states written before the
        // high-resolution flag was recorded as enabled.
        Compatibility.HighResolution |= Tuning.HighResolution;

        _framesExecuted = frames;
        _lastFrameCycles = 0;
        AudioSink?.Reset();
    }

    /// <summary>Loads a machine state atomically from a file.</summary>
    /// <param name="path">Source path.</param>
    /// <param name="expectedDiscHash">
    /// Optional game/disc hash of the disc currently inserted. A mismatch is reported through
    /// <see cref="StateWarnings"/> rather than rejected.
    /// </param>
    public void LoadStateFromFile(string path, string? expectedDiscHash = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        LoadState(stream, expectedDiscHash);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA5351:Do Not Use Broken Cryptographic Algorithms",
        Justification = "MD5 is the 3DO BIOS identifier used by 4DO.")]
    private string ComputeBiosHash()
    {
        var bios = new byte[EmulatorConstants.RomBankSize];
        Memory.CopyRomBank(0, bios);
        return Convert.ToHexStringLower(MD5.HashData(bios));
    }

    /// <summary>Runs one emulated frame in single-threaded mode. FreeDO: <c>_3do_Frame</c>.</summary>
    public void RunFrame()
    {
        RunFrame(false, null);
    }

    /// <summary>Runs one emulated frame.</summary>
    /// <param name="skipFrame">When true, VDLP rendering and frame presentation are skipped (multitask mode).</param>
    public void RunFrame(bool skipFrame)
    {
        RunFrame(skipFrame, null);
    }

    /// <summary>Runs one emulated frame with an optional per-instruction diagnostic observer.</summary>
    /// <param name="skipFrame">When true, VDLP rendering and frame presentation are skipped (multitask mode).</param>
    /// <param name="instructionObserver">
    /// Invoked before each CPU instruction with the program counter, CPSR, and raw instruction word.
    /// Intended for host diagnostics only; pass <see langword="null"/> in the steady-state loop.
    /// </param>
    public void RunFrame(bool skipFrame, Action<uint, uint, uint>? instructionObserver)
    {
        Clio.OnFrame();

        var total = 0;
        var batch = 0;
        var frameCycles = Tuning.ArmClock / 60;

        while (total < frameCycles)
        {
            if (Madam.Fsm == CelFsmState.InProcess)
            {
                Madam.HandleCel();
                Madam.Fsm = CelFsmState.Idle;
                continue;
            }

            if (instructionObserver is not null)
            {
                var pc = Cpu.ProgramCounter;
                instructionObserver(pc, Cpu.Cpsr, Memory.Bus.Read32(pc));
            }

            batch += Cpu.Step();
            if (batch < 16)
            {
                continue;
            }

            InternalFrame(batch, skipFrame);
            total += batch;
            batch = 0;
        }

        _framesExecuted++;
        _lastFrameCycles = total;
    }

    /// <summary>Runs a number of frames.</summary>
    /// <param name="count">Frame count.</param>
    public void RunFrames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            RunFrame();
        }
    }

    /// <summary>Renders 256 lines from the current VDLP state. FreeDO: <c>FDP_DO_FRAME_MT</c>.</summary>
    public void RenderFrameMultitask()
    {
        for (var line = 0; line < 256; line++)
        {
            Vdlp.DoLine(line, _frame);
        }
    }

    private void InternalFrame(int cycles, bool skipFrame)
    {
        Quartz.PushArmCycles((uint)cycles);

        if (Quartz.QueueDsp())
        {
            // The DSP must run even when the host has no audio sink. The guest
            // uses the DSP for resource allocation and semaphore handshakes, not
            // just for sample output.
            var sample = Dsp.Loop();
            _audioSamplesProduced++;
            AudioSink?.PushSample(sample);
        }

        if (Quartz.QueueTimer())
        {
            Clio.DoTimers();
        }

        if (!Quartz.QueueVdl())
        {
            return;
        }

        var line = Quartz.VdCurrLine;
        Clio.UpdateVcnt(line, Quartz.VdHalfFrame);

        if (!skipFrame)
        {
            Vdlp.DoLine(line, _frame);
        }

        if (line == 16 && skipFrame)
        {
            FrameSink?.OnFrameTrigger();
        }

        if (line == Clio.V0Line)
        {
            Clio.GenerateFiq(1u << 0, 0);
        }

        if (line != Clio.V1Line)
        {
            return;
        }

        Clio.GenerateFiq(1u << 1, 0);

        Span<byte> pbus = stackalloc byte[16];
        var length = PbusSource?.GetPbusData(pbus) ?? 0;
        if (length < 0)
        {
            length = 0;
        }
        else if (length > pbus.Length)
        {
            length = pbus.Length;
        }

        Madam.KeyPressed(pbus[..length]);

        var visibleHeight = Compatibility.HighResolution
            ? EmulatorConstants.HighResolutionHeight
            : EmulatorConstants.VideoHeight;
        _frame.SourceWidth = Compatibility.HighResolution
            ? EmulatorConstants.HighResolutionWidth
            : EmulatorConstants.VideoWidth;
        for (var lineIndex = 0; lineIndex < visibleHeight; lineIndex++)
        {
            var width = _frame.Lines[lineIndex].Width;
            if (width > _frame.SourceWidth)
            {
                _frame.SourceWidth = width;
            }
        }

        _frame.SourceHeight = visibleHeight;

        if (!skipFrame)
        {
            FrameSink?.OnFrame(_frame);
        }
    }

    /// <summary>Serializes the derived VDL frame so a load continues pixel-identically.</summary>
    private sealed class MachineFrameSection : IStateSection
    {
        private readonly VdlFrame _frame;
        private readonly CompatibilityState _compatibility;

        internal MachineFrameSection(VdlFrame frame, CompatibilityState compatibility)
        {
            _frame = frame;
            _compatibility = compatibility;
        }

        public uint Id => StateSectionIds.Machine;

        public ushort Version => 1;

        public void Save(IStateWriter writer)
        {
            writer.WriteUInt32((uint)_compatibility.FixMode);
            writer.WriteInt32(_compatibility.IsAnvil);
            writer.WriteBoolean(_compatibility.BiosAnvil);
            writer.WriteInt32(_compatibility.SpeedFixes);
            writer.WriteInt32(_compatibility.Sf);
            writer.WriteInt32(_compatibility.Sdf);
            writer.WriteInt32(_compatibility.UnknownFlag11);
            writer.WriteInt32(_compatibility.Jw);
            writer.WriteBoolean(_compatibility.Cnbfix);
            writer.WriteBoolean(_compatibility.HighResolution);
            writer.WriteInt32(_frame.SourceWidth);
            writer.WriteInt32(_frame.SourceHeight);
            foreach (VdlLine line in _frame.Lines)
            {
                writer.WriteInt32(line.Width);
                writer.WriteUInt32(line.OutControl);
                writer.WriteUInt32(line.ClutDma);
                writer.WriteUInt32(line.Background);
                writer.WriteBytes(line.ClutB);
                writer.WriteBytes(line.ClutG);
                writer.WriteBytes(line.ClutR);
                foreach (var pixel in line.PixelBuffer)
                {
                    writer.WriteUInt16(pixel);
                }
            }
        }

        public void Load(IStateReader reader)
        {
            _compatibility.FixMode = (FixMode)reader.ReadUInt32();
            _compatibility.IsAnvil = reader.ReadInt32();
            _compatibility.BiosAnvil = reader.ReadBoolean();
            _compatibility.SpeedFixes = reader.ReadInt32();
            _compatibility.Sf = reader.ReadInt32();
            _compatibility.Sdf = reader.ReadInt32();
            _compatibility.UnknownFlag11 = reader.ReadInt32();
            _compatibility.Jw = reader.ReadInt32();
            _compatibility.Cnbfix = reader.ReadBoolean();
            _compatibility.HighResolution = reader.ReadBoolean();
            _frame.SourceWidth = reader.ReadInt32();
            _frame.SourceHeight = reader.ReadInt32();
            foreach (VdlLine line in _frame.Lines)
            {
                var width = reader.ReadInt32();
                if (width < 0 || width > VdlLine.MaxPixels)
                {
                    throw new InvalidStateException($"Invalid VDL line width {width}.");
                }

                line.Width = width;
                line.OutControl = reader.ReadUInt32();
                line.ClutDma = reader.ReadUInt32();
                line.Background = reader.ReadUInt32();
                reader.ReadBytes(line.ClutB);
                reader.ReadBytes(line.ClutG);
                reader.ReadBytes(line.ClutR);
                for (var i = 0; i < line.PixelBuffer.Length; i++)
                {
                    line.PixelBuffer[i] = reader.ReadUInt16();
                }
            }
        }
    }
}
