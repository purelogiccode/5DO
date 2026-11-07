using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
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
    private readonly InterlaceProcessor _interlace = new();
    private VdlFrame _presentedFrame;

    private readonly List<string> _stateWarnings = [];
    private long _framesExecuted;
    private string? _biosHash;
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
        RegionProfile = VideoRegionProfile.For(Tuning.Region);
        Vdlp = new Vdlp(Memory.Ram, Compatibility) { Region = RegionProfile };
        Madam = new MadamChip
        {
            Vdlp = Vdlp,
            Interrupts = Clio,
            Fifo = Clio,
            Bus = Memory.Bus,
            Ram = Memory.Ram,
            CelTiming = Tuning.CelTiming,
            ArmClock = Tuning.ArmClock,
        };
        Madam.Compatibility = Compatibility;
        Vdlp.ClutDmaEnabled = () => Madam.ClutDmaEnabled;
        Vdlp.VideoDmaEnabled = () => Madam.VideoDmaEnabled;
        Clio.OperaDipirEnabled = Tuning.OperaDipir;
        Clio.OperaSoftResetEnabled = Tuning.OperaSoftReset;
        Clio.SoftResetRequested = HandleSoftReset;
        Xbus = new XbusChip
        {
            Interrupts = Clio,
            OperaSemantics = Tuning.ClioTimers == ClioTimerMode.OperaScheduler,
            MediaAccess = Tuning.OperaDipir ? Clio.XbusMediaAccess : null,
        };
        Cdrom = new CdromChip
        {
            TimingMode = Tuning.CdromTiming,
            ConfiguredSpeed = Tuning.CdSpeed,
            ArmClock = Tuning.ArmClock,
            InterruptRequested = () => Clio.GenerateFiq(4, 0),
        };
        Xbus.Attach(Cdrom);
        Dsp = new DspCore { Host = Clio };
        if (Tuning.RandomSeed is { } randomSeed)
        {
            Dsp.SetSeed(randomSeed);
            Clio.SetRandomSeed(randomSeed);
        }

        Clio.Ram = Memory.Ram;
        Clio.RomSelector = Memory;
        Clio.Xbus = Xbus;
        Clio.Dsp = Dsp;
        Clio.Madam = Madam;

        Memory.Madam = Madam;
        Memory.Clio = Clio;
        Memory.Sport = Sport;
        Memory.DiagPort = Diag;

        Cpu = new Arm60(Memory.Bus, Clio, Tuning, Compatibility, Memory.Ram);
        Memory.RamWriteHook = Cpu.NotifyRamWrite;
        Memory.RomChangedHook = Cpu.NotifyMemoryMapChanged;
        Quartz = new Quartz(Tuning, () => Clio.TimerDelay);

        Clio.CycleProvider = () => Cpu.CyclesExecuted;
        Clio.FiqRequested = HandleFiqRequest;
        Cpu.FiqEntered = HandleFiqEntry;

        Cdrom.ProgramCounterProvider = () => Cpu.ProgramCounter;
        Cdrom.FrameProvider = () => _framesExecuted;

        _presentedFrame = _frame;
        _interlace.Mode = DeinterlaceMode.Bob;
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

    /// <summary>Gets the active video-region geometry and field timing.</summary>
    public VideoRegionProfile RegionProfile { get; private set; }

    /// <summary>Gets the emulated field rate in hertz.</summary>
    /// <remarks>
    /// NTSC is 60000/1001 (59.94 Hz), not 60 Hz. Host pacing must use this exact
    /// rate; the nominal <see cref="VideoRegionProfile.FieldRate"/> is for display.
    /// </remarks>
    public double FramesPerSecond => RegionProfile.ExactFieldRate;

    /// <summary>Switches the video region without reloading the machine.</summary>
    /// <param name="region">New video region.</param>
    /// <remarks>
    /// Frontends call this between frames when a newly resolved BIOS changes the video
    /// standard. The frame store is cleared so stale lines from the old height cannot be
    /// presented.
    /// </remarks>
    public void SetVideoRegion(VideoRegion region)
    {
        VideoRegionProfile profile = VideoRegionProfile.For(region);
        if (profile == RegionProfile)
        {
            return;
        }

        RegionProfile = profile;
        Vdlp.Region = profile;
        Quartz.SetRegion(region);
        _frame.Clear();
        _interlace.Reset();
        _presentedFrame = _frame;
    }

    /// <summary>Gets the diagnostic port.</summary>
    public DiagPort Diag { get; }

    /// <summary>Gets the Quartz scheduler.</summary>
    public Quartz Quartz { get; }

    /// <summary>
    /// Gets the reusable frame produced by the VDLP after the configured field handling.
    /// </summary>
    /// <remarks>
    /// With <see cref="DeinterlaceMode.Bob"/> this is the raw 240-line field. With weave it
    /// is a 480-line composite, and with blend it is a 240-line averaged frame. The buffer
    /// is reused, so callers must copy it before handing it to another thread
    /// (<see cref="FiveDo.Video.VdlFrameMailbox"/> does this).
    /// </remarks>
    public VdlFrame Frame => _presentedFrame;

    /// <summary>Gets the raw VDLP field before field handling.</summary>
    public VdlFrame FieldFrame => _frame;

    /// <summary>Gets or sets the presentation-side interlace handling mode.</summary>
    /// <remarks>
    /// This is a presentation option: it does not change guest state, input, audio, or save
    /// states. The default is <see cref="DeinterlaceMode.Bob"/> so headless runs and
    /// differential captures keep the historical field output. Desktop profiles select
    /// <see cref="DeinterlaceMode.Auto"/> by default. Weave and blend change the presented
    /// frame (and therefore the frame hash), but not the emulated machine.
    /// </remarks>
    public DeinterlaceMode Deinterlace
    {
        get => _interlace.Mode;
        set => _interlace.Mode = value;
    }

    /// <summary>Gets a value indicating whether the detector currently sees interlaced content.</summary>
    public bool IsInterlacedFieldContent => _interlace.IsInterlaced;

    /// <summary>Gets a value indicating whether the presented frame combines two fields.</summary>
    /// <remarks>
    /// This is true for a detected weave and for an explicit <see cref="DeinterlaceMode.Weave"/>.
    /// Blend produces a normal-height frame even though it consumes two fields.
    /// </remarks>
    public bool IsWovenFrame => !ReferenceEquals(_presentedFrame, _frame)
        && _presentedFrame.SourceHeight == _frame.SourceHeight * 2;

    /// <summary>Gets the share of information samples that looked interlaced in the last detection pass.</summary>
    public double InterlaceDetectionFraction => _interlace.DetectionFraction;

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

    /// <summary>Gets the FIQ request-to-vector latency telemetry for this machine.</summary>
    /// <remarks>
    /// Counters are diagnostics only and are reset by <see cref="Reset"/>. They are deliberately
    /// not part of the save-state payload.
    /// </remarks>
    public FiqLatencyTracker FiqLatency { get; } = new();

    /// <summary>Gets or sets the observer for a FIQ becoming pending.</summary>
    public Action<FiqRequest>? FiqRequested { get; set; }

    /// <summary>Gets or sets the observer for a FIQ vector entry.</summary>
    public Action<FiqEntry>? FiqEntered { get; set; }

    private void HandleFiqRequest(FiqRequest request)
    {
        FiqLatency.OnRequested(request);
        FiqRequested?.Invoke(request);
    }

    private void HandleFiqEntry(long cycle, uint returnAddress)
    {
        FiqEntry entry = FiqLatency.OnEntered(
            cycle,
            returnAddress,
            Clio.ActiveFiqReason1,
            Clio.ActiveFiqReason2);
        FiqEntered?.Invoke(entry);
    }

    /// <summary>
    /// Gets or sets how unknown state sections are handled. The default skips them with a
    /// warning; <see cref="StateUnknownSectionPolicy.Reject"/> refuses the state.
    /// </summary>
    public StateUnknownSectionPolicy UnknownSectionPolicy { get; set; } = StateUnknownSectionPolicy.Warn;

    /// <summary>Gets or sets the frame sink. The frame is reused after the callback returns.</summary>
    public IFrameSink? FrameSink { get; set; }

    /// <summary>Gets or sets the PBUS controller source.</summary>
    public IPbusSource? PbusSource { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked once before each emulated frame. The host uses it
    /// for per-frame cheat writes so rewind replay and fast-forward observe the same
    /// modifications as normal execution.
    /// </summary>
    public Action? BeforeFrame { get; set; }

    /// <summary>Gets or sets the audio sample sink.</summary>
    public IAudioSink? AudioSink { get; set; }

    /// <summary>
    /// Gets or sets the CD-DA mix level, from 0 (muted) to 1 (full scale).
    /// </summary>
    /// <remarks>
    /// CD-DA is mixed into the DSP output only while PLAY_AUDIO is active. Most 3DO
    /// software reads audio tracks through READ_DATA and feeds the DSP itself, so this
    /// path is primarily for CD-player titles and diagnostics. The value is stored in
    /// the CD save-state section.
    /// </remarks>
    public double CdAudioVolume
    {
        get => Cdrom.AudioVolume;
        set => Cdrom.AudioVolume = value;
    }

    /// <summary>Gets or sets a value indicating whether CD-DA mixing is muted.</summary>
    public bool CdAudioMuted
    {
        get => Cdrom.AudioMuted;
        set => Cdrom.AudioMuted = value;
    }

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
        _biosHash = null;
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

    /// <summary>Handles a CLIO CSTAT soft reset. Opera: <c>opera_3do_soft_reset</c>.</summary>
    /// <param name="clearDipir">Whether the reset also clears the DIPIR latches and XBUS controller.</param>
    private void HandleSoftReset(bool clearDipir)
    {
        Cpu.SoftReset();
        Memory.Reset();
        Madam.SoftReset();
        Dsp.Reset();
        Dsp.SetRunning(false);
        if (clearDipir)
        {
            Xbus.ResetController();
            Clio.ClearDipirOnSoftReset();
        }
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
        _interlace.Reset();
        _presentedFrame = _frame;
        _framesExecuted = 0;
        _audioSamplesProduced = 0;
        FiqLatency.Reset();
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
        SaveState(
            destination,
            new StateSaveOptions { CompressionEnabled = compress },
            discHash);
    }

    /// <summary>Saves the complete machine state with explicit serialization options.</summary>
    /// <param name="destination">Destination stream.</param>
    /// <param name="options">Serialization options.</param>
    /// <param name="discHash">Optional game/disc hash stored in the state header.</param>
    /// <param name="reusableWriter">
    /// Optional writer whose buffers are kept between calls. The rewind history passes a
    /// long-lived writer so a multi-megabyte snapshot does not allocate large buffers every
    /// time.
    /// </param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA5351:Do Not Use Broken Cryptographic Algorithms",
        Justification = "MD5 is the 3DO BIOS/game identifier used by 4DO.")]
    public void SaveState(
        Stream destination,
        StateSaveOptions options,
        string? discHash = null,
        StateWriter? reusableWriter = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        var compress = options.CompressionEnabled;
        StateWriter writer = reusableWriter ?? new StateWriter(options.InitialCapacity);
        writer.Reset();
        writer.CompressionEnabled = compress;
        writer.CompressionLevel = options.CompressionLevel;
        writer.ChecksumsEnabled = options.ChecksumsEnabled;
        IStateSection[] sections =
        [
            Cpu,
            Memory,
            Madam,
            Clio,
            Dsp,
            Sport,
            Vdlp,
            Xbus,
            Cdrom,
            Quartz,
            new MachineOptionsSection(
                Tuning,
                RegionProfile.Region,
                Compatibility.FixMode,
                Memory.Layout.Configuration),
            new MachineFrameSection(_frame, Compatibility),
        ];
        uint features = StateFormat.FeatureOptionsSnapshot
            | StateFormat.FeatureSectionTable
            | StateFormat.FeatureVideoRegion
            | (compress ? StateFormat.FeatureCompressedSections : 0u)
            | (options.ChecksumsEnabled ? 0u : StateFormat.FeatureUncheckedSections);
        writer.WriteUInt32(StateFormat.Magic);
        writer.WriteUInt16(StateFormat.Version);
        writer.WriteUInt16(StateFormat.HeaderFlagFeatureFlags | StateFormat.HeaderFlagSectionTable);
        writer.WriteString(FiveDoVersion.Version);
        writer.WriteInt32(Tuning.ArmClock);
        writer.WriteUInt32((uint)Compatibility.FixMode);
        writer.WriteString(ComputeBiosHash());
        writer.WriteString(discHash ?? string.Empty);
        writer.WriteInt64(_framesExecuted);
        writer.WriteUInt32(features);
        writer.WriteUInt32((uint)sections.Length);
        foreach (IStateSection section in sections)
        {
            writer.WriteUInt32(section.Id);
            writer.WriteUInt16(section.Version);
            writer.WriteUInt16(0);
        }

        foreach (IStateSection section in sections)
        {
            writer.WriteSection(section);
        }

        destination.Write(writer.WrittenSpan);
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
        LoadState(buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length)), expectedDiscHash);
    }

    /// <summary>Loads a complete machine state from memory.</summary>
    /// <param name="data">Complete state bytes, including the header and section table.</param>
    /// <param name="expectedDiscHash">
    /// Optional game/disc hash of the disc currently inserted. When both this value and the
    /// state header's disc hash are present and differ, a warning is added to
    /// <see cref="StateWarnings"/> instead of rejecting the state.
    /// </param>
    /// <exception cref="InvalidStateException">Thrown when the state is invalid or incomplete.</exception>
    /// <remarks>
    /// This overload avoids copying the state again when the caller already owns an
    /// in-memory snapshot (for example the latency run-ahead cache).
    /// </remarks>
    public void LoadState(ReadOnlyMemory<byte> data, string? expectedDiscHash = null)
    {
        var reader = new StateReader(data);
        _stateWarnings.Clear();
        FiqLatency.Reset();

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

        var headerFlags = reader.ReadUInt16();
        var emulatorVersion = reader.ReadString();
        var armClock = reader.ReadInt32();
        var fixMode = reader.ReadUInt32();
        var biosHash = reader.ReadString();
        var discHash = reader.ReadString();
        var frames = reader.ReadInt64();

        Dictionary<uint, (ushort Version, ushort Flags)>? declaredSections = null;
        if (version >= 3)
        {
            if ((headerFlags & ~StateFormat.HeaderFlagAll) != 0)
            {
                _stateWarnings.Add($"State header has unknown flags 0x{headerFlags:X4}.");
            }

            if ((headerFlags & StateFormat.HeaderFlagFeatureFlags) != 0)
            {
                var features = reader.ReadUInt32();
                if ((features & ~StateFormat.FeatureAll) != 0)
                {
                    _stateWarnings.Add($"State declares unknown features 0x{features & ~StateFormat.FeatureAll:X8}.");
                }
            }

            if ((headerFlags & StateFormat.HeaderFlagSectionTable) != 0)
            {
                var count = reader.ReadUInt32();
                if (count > StateFormat.MaxSectionTableEntries)
                {
                    throw new InvalidStateException($"The state declares {count} sections, which is too many.");
                }

                declaredSections = new Dictionary<uint, (ushort Version, ushort Flags)>((int)count);
                for (var i = 0; i < count; i++)
                {
                    var id = reader.ReadUInt32();
                    var sectionVersion = reader.ReadUInt16();
                    var sectionFlags = reader.ReadUInt16();
                    if (!declaredSections.TryAdd(id, (sectionVersion, sectionFlags)))
                    {
                        throw new InvalidStateException($"Section 0x{id:X8} appears twice in the state table.");
                    }

                    if (sectionFlags != 0)
                    {
                        throw new InvalidStateException(
                            $"Section 0x{id:X8} has unsupported table flags 0x{sectionFlags:X4}.");
                    }
                }
            }
        }
        else if (headerFlags != 0)
        {
            _stateWarnings.Add($"Legacy state header has non-zero flags 0x{headerFlags:X4}.");
        }

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

        MachineOptionSnapshot? stateOptions = null;
        var machineFixMode = Compatibility.FixMode;
        MemoryConfiguration machineMemory = Memory.Layout.Configuration;
        var seenSections = new HashSet<uint>();
        while (reader.TryReadSection(out StateSectionReader? section))
        {
            seenSections.Add(section.Id);
            if (declaredSections is not null)
            {
                if (!declaredSections.TryGetValue(section.Id, out var declared))
                {
                    throw new InvalidStateException(
                        $"Section 0x{section.Id:X8} is not declared in the state table.");
                }

                if (declared.Version != section.Version)
                {
                    throw new InvalidStateException(
                        $"Section 0x{section.Id:X8} version {section.Version} does not match the declared version {declared.Version}.");
                }
            }

            var supportedVersion = section.Version == 1
                || (section.Id == StateSectionIds.Memory && section.Version <= 4)
                || (section.Id == StateSectionIds.Cdrom && section.Version <= 4)
                || (section.Id == StateSectionIds.Quartz && section.Version <= 4)
                || (section.Id == StateSectionIds.Clio && section.Version <= 3)
                || (section.Id == StateSectionIds.Madam && section.Version <= 4)
                || (section.Id == StateSectionIds.Vdlp && section.Version <= 3)
                || (section.Id == StateSectionIds.Machine && section.Version <= 2)
                || (section.Id == StateSectionIds.Options && section.Version <= 9);
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
                case StateSectionIds.Options:
                    var options = new MachineOptionsSection(Tuning);
                    options.Load(section);
                    stateOptions = options.Snapshot;
                    if (stateOptions?.Region is { } savedRegion && savedRegion != RegionProfile.Region)
                    {
                        // The frame section is written after the options section, so
                        // switching here clears the stale frame before it is restored.
                        SetVideoRegion(savedRegion);
                        _stateWarnings.Add(
                            $"State video region {savedRegion} differs from the machine region; switched to {savedRegion}.");
                    }

                    if (stateOptions?.MemoryConfiguration is { } savedMemory
                        && savedMemory != machineMemory)
                    {
                        // VRAM is located directly above DRAM, so the state's memory contents
                        // only decode correctly with the layout that produced them. Apply the
                        // options value again in case a hand-built state disagrees with its
                        // memory section.
                        Memory.SetMemoryConfiguration(savedMemory);
                        Madam.RefreshMemoryConfiguration();
                        _stateWarnings.Add(
                            $"State memory capacity {MemoryLayout.FormatDescription(savedMemory)} differs from the machine layout {MemoryLayout.FormatDescription(machineMemory)}; switched to {MemoryLayout.FormatDescription(savedMemory)}.");
                    }
                    else if (stateOptions is not null
                        && stateOptions.MemoryConfiguration is null
                        && machineMemory != Memory.Layout.Configuration)
                    {
                        Madam.RefreshMemoryConfiguration();
                        _stateWarnings.Add(
                            $"State does not record memory capacity; using the stock {MemoryLayout.FormatDescription(Memory.Layout.Configuration)} layout instead of the machine {MemoryLayout.FormatDescription(machineMemory)} layout.");
                    }

                    break;
                case StateSectionIds.Machine:
                    new MachineFrameSection(_frame, Compatibility).Load(section);
                    _interlace.Reset();
                    _presentedFrame = _frame;
                    break;
                default:
                    if (UnknownSectionPolicy == StateUnknownSectionPolicy.Reject)
                    {
                        throw new InvalidStateException($"Unknown state section 0x{section.Id:X8}.");
                    }

                    _stateWarnings.Add($"Ignored unknown state section {section.Id}.");
                    break;
            }

            required.Remove(section.Id);
        }

        if (required.Count > 0)
        {
            throw new InvalidStateException($"The save state is missing sections: {string.Join(", ", required)}.");
        }

        if (declaredSections is not null)
        {
            foreach (var id in declaredSections.Keys)
            {
                if (!seenSections.Contains(id))
                {
                    _stateWarnings.Add($"The state table declares section 0x{id:X8}, but it was not present.");
                }
            }
        }

        if (stateOptions is not null)
        {
            EmulatorTuning machineOptions = Tuning with
            {
                FixMode = machineFixMode,
                MemoryConfiguration = Memory.Layout.Configuration,
            };
            foreach (MachineOptionMismatch mismatch in MachineOptionRegistry.Compare(machineOptions, stateOptions))
            {
                // The region and memory warnings above already report the switch, and the
                // header ARM-clock warning already covers the clock; avoid duplicate messages.
                if (!string.Equals(mismatch.Key, "region", StringComparison.Ordinal)
                    && !string.Equals(mismatch.Key, "memory", StringComparison.Ordinal)
                    && !string.Equals(mismatch.Key, "cpuClock", StringComparison.Ordinal))
                {
                    _stateWarnings.Add(mismatch.Message);
                }
            }
        }

        // The memory section may have switched the layout; keep the read-only RED
        // SYSBITS register consistent even when the Options section is missing.
        Madam.RefreshMemoryConfiguration();

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
        // The ROM only changes through LoadBios, so a rewind snapshot does not need to
        // re-hash a megabyte of BIOS data every time.
        if (_biosHash is not null)
        {
            return _biosHash;
        }

        var bios = new byte[EmulatorConstants.RomBankSize];
        Memory.CopyRomBank(0, bios);
        _biosHash = Convert.ToHexStringLower(MD5.HashData(bios));
        return _biosHash;
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
        BeforeFrame?.Invoke();
        Clio.OnFrame();

        var total = 0;
        var batch = 0;
        var frameCycles = RegionProfile.GetFrameCycles(Tuning.ArmClock);
        var celBusyTiming = Tuning.CelTiming == CelTimingMode.OperaBusy;
        var useJit = Tuning.CpuBackend == CpuBackendKind.Jit
            && instructionObserver is null
            && !celBusyTiming;
        uint celElapsedCycles = 0;

        while (total < frameCycles)
        {
            if (celBusyTiming)
            {
                // Opera draws a pending list and expires the busy deadline before each
                // ARM instruction, so the CPU observes CEL status at instruction
                // granularity. The FreeDO path below keeps its synchronous draw.
                Madam.TickCel(celElapsedCycles);
                celElapsedCycles = 0;
            }
            else if (Madam.Fsm == CelFsmState.InProcess)
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

            var stepCycles = useJit ? Cpu.RunJitBlock(batch) : Cpu.Step();
            batch += stepCycles;
            if (celBusyTiming)
            {
                celElapsedCycles += (uint)stepCycles;
            }

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
        for (var line = 0; line < RegionProfile.Height; line++)
        {
            Vdlp.DoLine(line, _frame);
        }
    }

    private static AudioSample MixCdAudio(AudioSample dsp, AudioSample cd, double volume)
    {
        int left = dsp.Left + (int)Math.Round(cd.Left * volume);
        int right = dsp.Right + (int)Math.Round(cd.Right * volume);
        return new AudioSample(
            (short)Math.Clamp(left, short.MinValue, short.MaxValue),
            (short)Math.Clamp(right, short.MinValue, short.MaxValue));
    }

    private void InternalFrame(int cycles, bool skipFrame)
    {
        Quartz.PushArmCycles((uint)cycles);
        Cdrom.Tick((uint)cycles);

        if (Quartz.QueueDsp())
        {
            // The DSP must run even when the host has no audio sink. The guest
            // uses the DSP for resource allocation and semaphore handshakes, not
            // just for sample output.
            uint packedSample = Dsp.Loop();
            if (!CdAudioMuted && CdAudioVolume > 0 && Cdrom.TryGetAudioSample(out AudioSample cdAudio))
            {
                AudioSample mixed = MixCdAudio(AudioSample.FromPacked(packedSample), cdAudio, CdAudioVolume);
                packedSample = mixed.Packed;
            }

            _audioSamplesProduced++;
            AudioSink?.PushSample(packedSample);
        }

        if (Tuning.ClioTimers == ClioTimerMode.OperaScheduler)
        {
            while (Quartz.QueueTimer(out var timer))
            {
                Clio.DoTimers(timer);
            }
        }
        else if (Quartz.QueueTimer())
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

        Span<byte> pbus = stackalloc byte[256];
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
            ? RegionProfile.Height * 2
            : RegionProfile.Height;
        _frame.SourceWidth = Compatibility.HighResolution
            ? RegionProfile.Width * 2
            : RegionProfile.Width;
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
            _presentedFrame = _interlace.Process(_frame, Quartz.VdHalfFrame != 0);
            FrameSink?.OnFrame(_presentedFrame);
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

        public ushort Version => 2;

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
            writer.WriteDouble(_frame.PixelAspect);
            // Only the visible source lines can be addressed by the presenter. The
            // historical v1 payload wrote all 576 lines, which made a run-ahead or
            // rewind snapshot needlessly large and slow. The full pixel backing store is
            // kept so a load restores bytes that are outside the current Width but may be
            // re-exposed by a later VDL program.
            var lineCount = Math.Clamp(_frame.SourceHeight, 0, VdlFrame.MaxLines);
            writer.WriteInt32(lineCount);
            for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
            {
                VdlLine line = _frame[lineIndex];
                writer.WriteInt32(line.Width);
                writer.WriteUInt32(line.OutControl);
                writer.WriteUInt32(line.ClutDma);
                writer.WriteUInt32(line.Background);
                writer.WriteBytes(line.ClutB);
                writer.WriteBytes(line.ClutG);
                writer.WriteBytes(line.ClutR);
                writer.WriteBytes(MemoryMarshal.AsBytes(line.PixelBuffer));
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
            var version = reader is StateSectionReader sectionReader ? sectionReader.Version : 1;
            if (version >= 2)
            {
                _frame.PixelAspect = reader.ReadDouble();
                var lineCount = reader.ReadInt32();
                if (lineCount < 0 || lineCount > VdlFrame.MaxLines)
                {
                    throw new InvalidStateException($"Invalid VDL line count {lineCount}.");
                }

                _frame.Clear();
                for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
                {
                    VdlLine line = _frame[lineIndex];
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
                    reader.ReadBytes(MemoryMarshal.AsBytes(line.PixelBuffer));
                }

                return;
            }

            _frame.PixelAspect = 1.0;
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
