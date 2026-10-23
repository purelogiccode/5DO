using System;
using ThreeDo.Clio;
using ThreeDo.Core;
using ThreeDo.Cpu;
using ThreeDo.Dsp;
using ThreeDo.Madam;
using ThreeDo.Memory;
using ThreeDo.Sport;
using ThreeDo.Video;
using ThreeDo.Xbus;
using ClioChip = ThreeDo.Clio.Clio;
using MadamChip = ThreeDo.Madam.Madam;
using SportChip = ThreeDo.Sport.Sport;
using XbusChip = ThreeDo.Xbus.Xbus;

namespace ThreeDo.Machine;

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
public sealed class ThreeDoMachine
{
    private readonly VdlFrame _frame = new();

    private long _framesExecuted;
    private int _lastFrameCycles;

    /// <summary>Initializes a new instance of the <see cref="ThreeDoMachine"/> class.</summary>
    /// <param name="tuning">Machine tuning. Defaults to <see cref="EmulatorTuning.Default"/>.</param>
    /// <param name="compatibility">Optional shared compatibility state.</param>
    public ThreeDoMachine(EmulatorTuning? tuning = null, CompatibilityState? compatibility = null)
    {
        Tuning = tuning ?? EmulatorTuning.Default;
        Tuning.Validate();
        Compatibility = compatibility ?? new CompatibilityState { FixMode = Tuning.FixMode };

        Memory = new ThreeDoMemory(Tuning);
        Clio = new ClioChip(Tuning);
        Diag = new DiagPort();
        Sport = new SportChip(Memory.Ram);
        Vdlp = new Vdlp(Memory.Ram, Compatibility);
        Madam = new MadamChip
        {
            Vdlp = Vdlp,
            Interrupts = Clio,
            Fifo = Clio,
            Bus = Memory.Bus,
            Ram = Memory.Ram,
        };
        Xbus = new XbusChip { Interrupts = Clio };
        Dsp = new DspStub();

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

        Reset();
    }

    /// <summary>Gets the machine tuning.</summary>
    public EmulatorTuning Tuning { get; }

    /// <summary>Gets the shared compatibility state.</summary>
    public CompatibilityState Compatibility { get; }

    /// <summary>Gets the memory subsystem.</summary>
    public ThreeDoMemory Memory { get; }

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

    /// <summary>Gets the DSP backend. M5 replaces the stub with the full core.</summary>
    public IDsp Dsp { get; }

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

    /// <summary>Loads a 32 KB NVRAM image.</summary>
    /// <param name="nvram">Exactly 32 KB of NVRAM data.</param>
    public void LoadNvram(ReadOnlySpan<byte> nvram) => Memory.LoadNvram(nvram);

    /// <summary>Copies the current NVRAM image.</summary>
    /// <param name="destination">Destination span of at least 32 KB.</param>
    public void CopyNvram(Span<byte> destination) => Memory.CopyNvram(destination);

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
        Clio.Reset(0x40);
        Dsp.Reset();
        Diag.Reset(-1);
        Quartz.Reset();
        _frame.Clear();
        _framesExecuted = 0;

        if (Compatibility.IsAnvil == 30)
        {
            Compatibility.BiosAnvil = true;
        }
    }

    /// <summary>Runs one emulated frame in single-threaded mode. FreeDO: <c>_3do_Frame</c>.</summary>
    public void RunFrame() => RunFrame(false);

    /// <summary>Runs one emulated frame.</summary>
    /// <param name="skipFrame">When true, VDLP rendering and frame presentation are skipped (multitask mode).</param>
    public void RunFrame(bool skipFrame)
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
            AudioSink?.PushSample(Dsp.Loop());
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

        _frame.SourceWidth = EmulatorConstants.VideoWidth;
        _frame.SourceHeight = EmulatorConstants.VideoHeight;

        if (!skipFrame)
        {
            FrameSink?.OnFrame(_frame);
        }
    }
}
