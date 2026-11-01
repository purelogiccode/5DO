using System;
using System.Diagnostics;
using FiveDo.Core;

namespace FiveDo.Machine;

/// <summary>
/// The 8.24 fixed-point clock scheduler ported from FreeDOCore's <c>quarz.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each accumulator counts events in 8.24 fixed point. A queue operation consumes one
/// whole event (<c>0x1000000</c>) and returns true at most once per call, matching the
/// reference's event semantics.
/// </para>
/// <para>
/// <see cref="PushArmCycles"/> is called with small cycle batches (normally under 256
/// cycles). The reference computes <c>clks &lt;&lt; 24</c> in 32 bits, so larger batches
/// would overflow; a debug assertion documents that contract.
/// </para>
/// </remarks>
public sealed class Quartz : IStateSection
{
    private const uint TimerSlotCount = 16;
    private const uint TimerSlotTicks = 4;

    private readonly ClioTimerMode _timerMode;
    private VideoRegionProfile _region;
    private int _armClock;
    private uint _accArm;
    private uint _accVdl;
    private uint _tCount;
    private uint _vdlLine;

    // Opera's timer scheduler walks one slot per 4 M25M ticks and inserts a slack gap
    // after slot 15. The accumulator is 16.16 ARM cycles so a 12.5 MHz CPU produces
    // exact two-cycle slots instead of rounding every scan.
    private ulong _timerAccumulator1616;
    private uint _timerSlot;
    private bool _timerInGap;

    // The DSP sample accumulator counts ARM cycles toward the next 44.1 kHz sample. The
    // FreeDO 8.24 scheme quantizes every batch to 1/2^24 second and over-produced samples by
    // up to ~0.7% for the 16-32 cycle batches this emulator uses (audibly sharp pitch and a
    // constantly overfilled audio ring). The sample clock is independent of the VDL and timer
    // accumulators so this correction cannot move video or timer events.
    private uint _dspCycleRemainder;

    /// <summary>Initializes a new instance of the <see cref="Quartz"/> class.</summary>
    /// <param name="tuning">Machine tuning. Defaults to <see cref="EmulatorTuning.Default"/>.</param>
    /// <param name="timerDelayProvider">Supplies the CLIO timer delay. May be null before CLIO is initialized.</param>
    public Quartz(EmulatorTuning? tuning = null, Func<uint>? timerDelayProvider = null)
    {
        EmulatorTuning effectiveTuning = tuning ?? EmulatorTuning.Default;
        effectiveTuning.Validate();
        _armClock = effectiveTuning.ArmClock;
        _region = VideoRegionProfile.For(effectiveTuning.Region);
        _timerMode = effectiveTuning.ClioTimers;
        TimerDelayProvider = timerDelayProvider;
    }

    /// <summary>Gets the VDL events per second. FreeDO: <c>VDL_CLOCK</c>.</summary>
    public static int VdlClock => EmulatorConstants.VdlClock;

    /// <summary>Gets the VDL lines in one half-frame. FreeDO: <c>VDL_HS</c>.</summary>
    public static int VdlHalfFrameLines => EmulatorConstants.VdlLinesPerHalfFrame;

    /// <summary>Gets the VDL lines in one full cycle. FreeDO: <c>VDL_FS</c>.</summary>
    public static int VdlFrameLines => EmulatorConstants.VdlFrameLines;

    /// <summary>Gets the active region profile.</summary>
    public VideoRegionProfile RegionProfile => _region;

    /// <summary>Switches the region and resets the VDL phase at the next frame boundary.</summary>
    /// <param name="region">New video region.</param>
    public void SetRegion(VideoRegion region)
    {
        _region = VideoRegionProfile.For(region);
        _accVdl = 0;
        _vdlLine = 0;
    }

    /// <summary>Gets the VDL lines in one field for the active region.</summary>
    public int VdlFieldLines => _region.FieldLines;

    /// <summary>Gets the VDL lines in one full two-field cycle for the active region.</summary>
    public int VdlFrameLineCount => _region.FrameLines;

    /// <summary>Gets the VDL events per second for the active region.</summary>
    public int VdlEventClock => _region.VdlClock;

    /// <summary>Gets or sets the ARM clock in hertz. FreeDO: <c>ARM_CLOCK</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not positive.</exception>
    public int ArmClock
    {
        get => _armClock;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The ARM clock must be positive.");
            }

            _armClock = value;
        }
    }

    /// <summary>Gets or sets the timer delay provider. FreeDO: <c>_clio_GetTimerDelay()</c>.</summary>
    public Func<uint>? TimerDelayProvider { get; set; }

    /// <summary>Gets the raw ARM accumulator. Exposed for tests and diagnostics.</summary>
    public uint ArmAccumulator => _accArm;

    /// <summary>Gets the ARM cycles accumulated toward the next DSP sample. Exposed for tests and diagnostics.</summary>
    public uint DspAccumulator => _dspCycleRemainder;

    /// <summary>Gets the raw VDL accumulator. Exposed for tests and diagnostics.</summary>
    public uint VdlAccumulator => _accVdl;

    /// <summary>Gets the raw timer accumulator. Exposed for tests and diagnostics.</summary>
    /// <remarks>
    /// In <see cref="ClioTimerMode.FreeDoCompatible"/> mode this is the 8.24 FreeDO
    /// accumulator. In <see cref="ClioTimerMode.OperaScheduler"/> mode it is the whole
    /// ARM-cycle part of the 16.16 per-slot accumulator.
    /// </remarks>
    public uint TimerAccumulator => _timerMode == ClioTimerMode.OperaScheduler
        ? (uint)(_timerAccumulator1616 >> 16)
        : _tCount;

    /// <summary>Gets the timer mode selected at construction.</summary>
    public ClioTimerMode TimerMode => _timerMode;

    /// <summary>Gets the next timer slot in Opera mode. Exposed for tests and diagnostics.</summary>
    public int TimerSlot => (int)_timerSlot;

    /// <summary>Gets a value indicating whether the scheduler is in the post-slot gap in Opera mode.</summary>
    public bool TimerInGap => _timerInGap;

    /// <summary>Gets the current VDL line within the half-frame. FreeDO: <c>_qrz_VDCurrLine()</c>.</summary>
    public int VdCurrLine => (int)(_vdlLine % (uint)_region.FieldLines);

    /// <summary>Gets the current half-frame index. FreeDO: <c>_qrz_VDHalfFrame()</c>.</summary>
    public int VdHalfFrame => (int)(_vdlLine / (uint)_region.FieldLines);

    /// <summary>Gets the absolute VDL line counter. FreeDO: <c>_qrz_VDCurrOverline()</c>.</summary>
    public int VdCurrOverline => (int)_vdlLine;

    /// <summary>Resets all accumulators and the line counter.</summary>
    public void Reset()
    {
        _accArm = 0;
        _dspCycleRemainder = 0;
        _accVdl = 0;
        _tCount = 0;
        _vdlLine = 0;
        _timerAccumulator1616 = 0;
        _timerSlot = 0;
        _timerInGap = false;
    }

    /// <summary>Adds ARM cycles to the scheduler. FreeDO: <c>_qrz_PushARMCycles</c>.</summary>
    /// <param name="cycles">ARM cycles executed since the previous push.</param>
    public void PushArmCycles(uint cycles)
    {
        Debug.Assert(cycles < 256, "Quartz expects small cycle batches; clks << 24 overflows otherwise.");

        var arm = unchecked((cycles << 24) / (uint)_armClock);
        _accArm = unchecked(_accArm + (arm * (uint)_armClock));
        if ((_accArm >> 24) != cycles)
        {
            arm++;
            _accArm = unchecked(_accArm + (uint)_armClock);
            _accArm &= 0xFFFFFF;
        }

        // Exact rational sample clock: 44100 samples per ARM second. Both factors fit in a
        // uint for the normal 16-256 cycle batches; the remainder is always below _armClock.
        _dspCycleRemainder = unchecked(_dspCycleRemainder + (cycles * EmulatorConstants.SoundClock));
        _accVdl = unchecked(_accVdl + (arm * (uint)_region.VdlClock));

        if (_timerMode == ClioTimerMode.OperaScheduler)
        {
            // Opera accumulates the raw ARM cycle batch in 16.16 and walks one timer
            // slot per four M25M ticks, plus a slack gap after slot 15.
            _timerAccumulator1616 += (ulong)cycles << 16;
        }
        else
        {
            var timerDelay = TimerDelayProvider?.Invoke() ?? 0;
            if (timerDelay != 0)
            {
                _tCount = unchecked(_tCount + (arm * (EmulatorConstants.TimerClock / timerDelay)));
            }
        }
    }

    /// <summary>Consumes one DSP sample event. FreeDO: <c>_qrz_QueueDSP</c>.</summary>
    /// <returns><see langword="true"/> when a sample is due.</returns>
    public bool QueueDsp()
    {
        if (_dspCycleRemainder >= (uint)_armClock)
        {
            _dspCycleRemainder -= (uint)_armClock;
            return true;
        }

        return false;
    }

    /// <summary>Consumes one timer event. FreeDO: <c>_qrz_QueueTimer</c>.</summary>
    /// <returns><see langword="true"/> when a timer tick is due.</returns>
    public bool QueueTimer()
    {
        return QueueTimer(out _);
    }

    /// <summary>Consumes one timer event and reports the due timer slot.</summary>
    /// <param name="timer">
    /// Zero-based timer slot in <see cref="ClioTimerMode.OperaScheduler"/> mode. In
    /// <see cref="ClioTimerMode.FreeDoCompatible"/> mode the all-timer scan is due and
    /// the value is zero.
    /// </param>
    /// <returns><see langword="true"/> when a timer event is due.</returns>
    public bool QueueTimer(out int timer)
    {
        if (_timerMode == ClioTimerMode.OperaScheduler)
        {
            return QueueOperaTimer(out timer);
        }

        timer = 0;
        if ((_tCount >> 24) != 0)
        {
            _tCount = unchecked(_tCount - 0x1000000);
            return true;
        }

        return false;
    }

    private bool QueueOperaTimer(out int timer)
    {
        // CLIO resets 0x220 to 64, which is also Opera's default timer slack.
        var slack = TimerDelayProvider?.Invoke() ?? 64u;
        if (slack > 0x3ffu)
        {
            slack = 0x3ffu;
        }

        var ticks = _timerInGap ? TimerSlotTicks + slack : TimerSlotTicks;
        var threshold = (((ulong)(uint)_armClock * ticks) << 16) / EmulatorConstants.TimerClock;
        if (threshold == 0)
        {
            threshold = 1;
        }

        if (_timerAccumulator1616 < threshold)
        {
            timer = -1;
            return false;
        }

        _timerAccumulator1616 -= threshold;
        timer = (int)_timerSlot;

        _timerSlot++;
        if (_timerSlot >= TimerSlotCount)
        {
            _timerSlot = 0;
            _timerInGap = true;
        }
        else
        {
            _timerInGap = false;
        }

        return true;
    }

    /// <summary>Consumes one VDL line event. FreeDO: <c>_qrz_QueueVDL</c>.</summary>
    /// <returns><see langword="true"/> when a VDL line is due.</returns>
    public bool QueueVdl()
    {
        if ((_accVdl >> 24) != 0)
        {
            _accVdl = unchecked(_accVdl - 0x1000000);
            _vdlLine++;
            _vdlLine %= (uint)_region.FrameLines;
            return true;
        }

        return false;
    }

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Quartz;

    /// <summary>Gets the save-state section version.</summary>
    /// <remarks>
    /// Version 2 stores the exact DSP cycle remainder instead of FreeDO's 8.24 sample
    /// accumulator. Version 3 adds the Opera timer slot/accumulator state. Version-1 and
    /// version-2 sections are still accepted; missing timer fields default to a reset
    /// scheduler.
    /// </remarks>
    public ushort Version => 3;

    /// <summary>Writes the scheduler section.</summary>
    /// <param name="writer">State writer.</param>
    public void Save(IStateWriter writer)
    {
        SaveState(writer);
    }

    /// <summary>Reads the scheduler section.</summary>
    /// <param name="reader">State reader.</param>
    public void Load(IStateReader reader)
    {
        LoadState(reader);
    }

    /// <summary>Writes the scheduler state.</summary>
    /// <param name="writer">State writer.</param>
    public void SaveState(IStateWriter writer)
    {
        writer.WriteUInt32(_accArm);
        writer.WriteUInt32(_dspCycleRemainder);
        writer.WriteUInt32(_accVdl);
        writer.WriteUInt32(_tCount);
        writer.WriteUInt32(_vdlLine);
        writer.WriteUInt64(_timerAccumulator1616);
        writer.WriteUInt32(_timerSlot);
        writer.WriteUInt32(_timerInGap ? 1u : 0u);
    }

    /// <summary>Reads the scheduler state.</summary>
    /// <param name="reader">State reader.</param>
    /// <exception cref="InvalidStateException">Thrown when the line counter is out of range.</exception>
    public void LoadState(IStateReader reader)
    {
        _accArm = reader.ReadUInt32();
        var dspClock = reader.ReadUInt32();
        _accVdl = reader.ReadUInt32();
        _tCount = reader.ReadUInt32();
        var line = reader.ReadUInt32();
        if (line >= _region.FrameLines)
        {
            throw new InvalidStateException($"Invalid quartz VDL line {line}.");
        }

        if (reader is StateSectionReader { Version: 1 })
        {
            // FreeDO's 8.24 sample accumulator: the high byte is the completed-sample count
            // and the low 24 bits are the fractional sample position. Convert that fraction
            // to ARM cycles toward the next exact-clock sample.
            var fraction = dspClock & 0xFFFFFFu;
            _dspCycleRemainder = (uint)(((ulong)fraction * (uint)_armClock) >> 24);
        }
        else
        {
            if (dspClock >= (uint)_armClock)
            {
                throw new InvalidStateException($"Invalid quartz DSP remainder {dspClock}.");
            }

            _dspCycleRemainder = dspClock;
        }

        _vdlLine = line;

        if (reader is StateSectionReader { Version: 1 or 2 })
        {
            _timerAccumulator1616 = 0;
            _timerSlot = 0;
            _timerInGap = false;
            return;
        }

        _timerAccumulator1616 = reader.ReadUInt64();
        var timerSlot = reader.ReadUInt32();
        var timerInGap = reader.ReadUInt32();
        if (timerSlot >= TimerSlotCount)
        {
            throw new InvalidStateException($"Invalid quartz timer slot {timerSlot}.");
        }

        if (timerInGap > 1)
        {
            throw new InvalidStateException($"Invalid quartz timer gap flag {timerInGap}.");
        }

        _timerSlot = timerSlot;
        _timerInGap = timerInGap != 0;
    }
}
