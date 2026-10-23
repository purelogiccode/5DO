using System;
using System.Diagnostics;
using ThreeDo.Core;

namespace ThreeDo.Machine;

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
    private int _armClock;
    private uint _accArm;
    private uint _accDsp;
    private uint _accVdl;
    private uint _tCount;
    private uint _vdlLine;

    /// <summary>Initializes a new instance of the <see cref="Quartz"/> class.</summary>
    /// <param name="tuning">Machine tuning. Defaults to <see cref="EmulatorTuning.Default"/>.</param>
    /// <param name="timerDelayProvider">Supplies the CLIO timer delay. May be null before CLIO is initialized.</param>
    public Quartz(EmulatorTuning? tuning = null, Func<uint>? timerDelayProvider = null)
    {
        var effectiveTuning = tuning ?? EmulatorTuning.Default;
        effectiveTuning.Validate();
        _armClock = effectiveTuning.ArmClock;
        TimerDelayProvider = timerDelayProvider;
    }

    /// <summary>Gets the VDL events per second. FreeDO: <c>VDL_CLOCK</c>.</summary>
    public static int VdlClock => EmulatorConstants.VdlClock;

    /// <summary>Gets the VDL lines in one half-frame. FreeDO: <c>VDL_HS</c>.</summary>
    public static int VdlHalfFrameLines => EmulatorConstants.VdlLinesPerHalfFrame;

    /// <summary>Gets the VDL lines in one full cycle. FreeDO: <c>VDL_FS</c>.</summary>
    public static int VdlFrameLines => EmulatorConstants.VdlFrameLines;

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

    /// <summary>Gets the raw DSP accumulator. Exposed for tests and diagnostics.</summary>
    public uint DspAccumulator => _accDsp;

    /// <summary>Gets the raw VDL accumulator. Exposed for tests and diagnostics.</summary>
    public uint VdlAccumulator => _accVdl;

    /// <summary>Gets the raw timer accumulator. Exposed for tests and diagnostics.</summary>
    public uint TimerAccumulator => _tCount;

    /// <summary>Gets the current VDL line within the half-frame. FreeDO: <c>_qrz_VDCurrLine()</c>.</summary>
    public int VdCurrLine => (int)(_vdlLine % EmulatorConstants.VdlLinesPerHalfFrame);

    /// <summary>Gets the current half-frame index. FreeDO: <c>_qrz_VDHalfFrame()</c>.</summary>
    public int VdHalfFrame => (int)(_vdlLine / EmulatorConstants.VdlLinesPerHalfFrame);

    /// <summary>Gets the absolute VDL line counter. FreeDO: <c>_qrz_VDCurrOverline()</c>.</summary>
    public int VdCurrOverline => (int)_vdlLine;

    /// <summary>Resets all accumulators and the line counter.</summary>
    public void Reset()
    {
        _accArm = 0;
        _accDsp = 0;
        _accVdl = 0;
        _tCount = 0;
        _vdlLine = 0;
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

        _accDsp = unchecked(_accDsp + (arm * EmulatorConstants.SoundClock));
        _accVdl = unchecked(_accVdl + (arm * (uint)EmulatorConstants.VdlClock));

        var timerDelay = TimerDelayProvider?.Invoke() ?? 0;
        if (timerDelay != 0)
        {
            _tCount = unchecked(_tCount + (arm * ((uint)EmulatorConstants.TimerClock / timerDelay)));
        }
    }

    /// <summary>Consumes one DSP sample event. FreeDO: <c>_qrz_QueueDSP</c>.</summary>
    /// <returns><see langword="true"/> when a sample is due.</returns>
    public bool QueueDsp()
    {
        if ((_accDsp >> 24) != 0)
        {
            _accDsp = unchecked(_accDsp - 0x1000000);
            return true;
        }

        return false;
    }

    /// <summary>Consumes one timer event. FreeDO: <c>_qrz_QueueTimer</c>.</summary>
    /// <returns><see langword="true"/> when a timer tick is due.</returns>
    public bool QueueTimer()
    {
        if ((_tCount >> 24) != 0)
        {
            _tCount = unchecked(_tCount - 0x1000000);
            return true;
        }

        return false;
    }

    /// <summary>Consumes one VDL line event. FreeDO: <c>_qrz_QueueVDL</c>.</summary>
    /// <returns><see langword="true"/> when a VDL line is due.</returns>
    public bool QueueVdl()
    {
        if ((_accVdl >> 24) != 0)
        {
            _accVdl = unchecked(_accVdl - 0x1000000);
            _vdlLine++;
            _vdlLine %= EmulatorConstants.VdlFrameLines;
            return true;
        }

        return false;
    }

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Quartz;

    /// <summary>Gets the save-state section version.</summary>
    public ushort Version => 1;

    /// <summary>Writes the scheduler section.</summary>
    /// <param name="writer">State writer.</param>
    public void Save(IStateWriter writer) => SaveState(writer);

    /// <summary>Reads the scheduler section.</summary>
    /// <param name="reader">State reader.</param>
    public void Load(IStateReader reader) => LoadState(reader);

    /// <summary>Writes the scheduler state.</summary>
    /// <param name="writer">State writer.</param>
    public void SaveState(IStateWriter writer)
    {
        writer.WriteUInt32(_accArm);
        writer.WriteUInt32(_accDsp);
        writer.WriteUInt32(_accVdl);
        writer.WriteUInt32(_tCount);
        writer.WriteUInt32(_vdlLine);
    }

    /// <summary>Reads the scheduler state.</summary>
    /// <param name="reader">State reader.</param>
    /// <exception cref="InvalidStateException">Thrown when the line counter is out of range.</exception>
    public void LoadState(IStateReader reader)
    {
        _accArm = reader.ReadUInt32();
        _accDsp = reader.ReadUInt32();
        _accVdl = reader.ReadUInt32();
        _tCount = reader.ReadUInt32();
        var line = reader.ReadUInt32();
        if (line >= EmulatorConstants.VdlFrameLines)
        {
            throw new InvalidStateException($"Invalid quartz VDL line {line}.");
        }

        _vdlLine = line;
    }
}
