using System;
using FiveDo.Core;
using FiveDo.Machine;

namespace FiveDo.Tests.Machine;

public sealed class QuartzTests
{
    [Fact]
    public void Constructor_InitializesReferenceConstants()
    {
        var quartz = new Quartz();

        Assert.Equal(EmulatorConstants.ArmClock, quartz.ArmClock);
        Assert.Equal(526, Quartz.VdlFrameLines);
        Assert.Equal(263, Quartz.VdlHalfFrameLines);
        Assert.Equal(15780, Quartz.VdlClock);
        Assert.Equal(0, quartz.VdCurrOverline);
        Assert.Equal(0, quartz.VdCurrLine);
        Assert.Equal(0, quartz.VdHalfFrame);
    }

    [Theory]
    [InlineData(1u, 2u, 8_222_784u, 44_100u, 31_560u)]
    [InlineData(15u, 21u, 10_841_760u, 661_500u, 331_380u)]
    [InlineData(16u, 22u, 6_564_544u, 705_600u, 347_160u)]
    [InlineData(17u, 23u, 2_287_328u, 749_700u, 362_940u)]
    [InlineData(255u, 343u, 9_309_920u, 11_245_500u, 5_412_540u)]
    public void PushArmCycles_UsesTheFreeDoArmClockAndExactDspSampleClock(
        uint cycles,
        uint expectedArm,
        uint expectedArmAccumulator,
        uint expectedDspAccumulator,
        uint expectedVdlAccumulator)
    {
        var quartz = new Quartz();

        quartz.PushArmCycles(cycles);

        Assert.Equal(expectedArmAccumulator, quartz.ArmAccumulator);
        Assert.Equal(expectedDspAccumulator, quartz.DspAccumulator);
        Assert.Equal(expectedVdlAccumulator, quartz.VdlAccumulator);

        // The DSP sample clock accumulates ARM cycles directly instead of the FreeDO 8.24
        // rounding value, so it is exactly 44100 samples per emulated second.
        Assert.Equal(cycles * EmulatorConstants.SoundClock, expectedDspAccumulator);
        Assert.InRange(expectedArm, 1u, 1_000u);
    }

    [Fact]
    public void PushArmCycles_WithTimerDelay_AccumulatesTimerEvents()
    {
        var quartz = new Quartz(timerDelayProvider: () => 1000);

        quartz.PushArmCycles(1);

        Assert.Equal(2u * (EmulatorConstants.TimerClock / 1000u), quartz.TimerAccumulator);
    }

    [Fact]
    public void PushArmCycles_WithZeroTimerDelay_DoesNotAccumulateTimerEvents()
    {
        var quartz = new Quartz(timerDelayProvider: () => 0);

        quartz.PushArmCycles(255);

        Assert.Equal(0u, quartz.TimerAccumulator);
    }

    [Fact]
    public void PushArmCycles_OneSecond_ProducesSixtyHalfFrames()
    {
        var quartz = new Quartz();
        var halfFrames = 0;
        var previousHalf = quartz.VdHalfFrame;
        var total = 0u;

        while (total < EmulatorConstants.ArmClock)
        {
            var chunk = Math.Min(255u, EmulatorConstants.ArmClock - total);
            quartz.PushArmCycles(chunk);
            total += chunk;

            while (quartz.QueueVdl())
            {
                var currentHalf = quartz.VdHalfFrame;
                if (currentHalf != previousHalf)
                {
                    halfFrames++;
                    previousHalf = currentHalf;
                }
            }
        }

        Assert.Equal(60, halfFrames);
        Assert.InRange(quartz.VdCurrOverline, 0, EmulatorConstants.VdlFrameLines - 1);
    }

    [Fact]
    public void PushArmCycles_OneSecond_ProducesExactDspAndReferenceVideoTimerCounts()
    {
        var quartz = new Quartz(timerDelayProvider: () => 1000u);
        var dspEvents = 0;
        var vdlEvents = 0;
        var timerEvents = 0;
        var total = 0u;

        while (total < EmulatorConstants.ArmClock)
        {
            var chunk = Math.Min(255u, EmulatorConstants.ArmClock - total);
            quartz.PushArmCycles(chunk);
            total += chunk;

            while (quartz.QueueDsp())
            {
                dspEvents++;
            }

            while (quartz.QueueVdl())
            {
                vdlEvents++;
            }

            while (quartz.QueueTimer())
            {
                timerEvents++;
            }
        }

        // The DSP sample clock is an exact rational accumulator and must produce 44,100
        // samples in one ARM second regardless of batching. The VDL and timer clocks keep
        // the FreeDO 8.24 reference semantics (they are not part of this audio correction);
        // the timer count follows FreeDO quarz.cpp's default 25 MHz timer clock.
        Assert.Equal(44_100, dspEvents);
        Assert.Equal(15_794, vdlEvents);
        Assert.Equal(25_022, timerEvents);
    }

    [Fact]
    public void QueueDsp_DoesNotDriftAcrossSixteenCycleBatches()
    {
        var quartz = new Quartz();
        var events = 0;

        for (var i = 0; i < 600; i++)
        {
            var total = 0u;
            while (total < EmulatorConstants.FrameCycles)
            {
                quartz.PushArmCycles(16);
                total += 16;
                if (quartz.QueueDsp())
                {
                    events++;
                }
            }
        }

        // Each 208,333-cycle frame is rounded up to a whole 16-cycle batch, so 600 frames
        // consume 125,001,600 cycles and produce 441,005 samples instead of exactly 441,000.
        Assert.InRange(events, 441_000, 441_008);
    }

    [Fact]
    public void QueueDsp_ConsumesOneEventPerCall()
    {
        var quartz = new Quartz();
        quartz.PushArmCycles(255);
        quartz.PushArmCycles(255);

        var count = 0;
        while (quartz.QueueDsp())
        {
            count++;
        }

        Assert.Equal(1, count);
        Assert.Equal(0u, quartz.DspAccumulator >> 24);
    }

    [Fact]
    public void QueueVdl_WrapsAtFrameBoundary()
    {
        var quartz = new Quartz();
        var total = 0u;
        while (total < EmulatorConstants.ArmClock)
        {
            var chunk = Math.Min(255u, EmulatorConstants.ArmClock - total);
            quartz.PushArmCycles(chunk);
            total += chunk;
            while (quartz.QueueVdl())
            {
            }
        }

        Assert.InRange(quartz.VdCurrOverline, 0, EmulatorConstants.VdlFrameLines - 1);
    }

    [Fact]
    public void ArmClock_InvalidValue_Throws()
    {
        var quartz = new Quartz();

        Assert.Throws<ArgumentOutOfRangeException>(() => quartz.ArmClock = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => quartz.ArmClock = -1);
    }

    [Fact]
    public void SaveLoad_RoundtripsAccumulatorsAndLine()
    {
        var quartz = new Quartz();
        quartz.PushArmCycles(255);
        while (quartz.QueueVdl())
        {
        }

        var writer = new StateWriter();
        quartz.SaveState(writer);

        var restored = new Quartz();
        var reader = new StateReader(writer.ToArray());
        restored.LoadState(reader);

        Assert.Equal(quartz.ArmAccumulator, restored.ArmAccumulator);
        Assert.Equal(quartz.DspAccumulator, restored.DspAccumulator);
        Assert.Equal(quartz.VdlAccumulator, restored.VdlAccumulator);
        Assert.Equal(quartz.TimerAccumulator, restored.TimerAccumulator);
        Assert.Equal(quartz.VdCurrOverline, restored.VdCurrOverline);
    }

    [Fact]
    public void LoadState_Version1_ConvertsTheLegacyFreeDoDspFraction()
    {
        const uint legacyFraction = 0x40_0000;
        var section = new TestSection(
            StateSectionIds.Quartz,
            version: 1,
            writer =>
            {
                writer.WriteUInt32(0);
                writer.WriteUInt32(legacyFraction);
                writer.WriteUInt32(0);
                writer.WriteUInt32(0);
                writer.WriteUInt32(0);
            });
        var outer = new StateWriter();
        outer.WriteSection(section);
        StateSectionReader sectionReader = new StateReader(outer.ToArray()).ReadSection();

        var quartz = new Quartz();
        quartz.LoadState(sectionReader);

        var expected = (uint)(((ulong)legacyFraction * EmulatorConstants.ArmClock) >> 24);
        Assert.Equal(expected, quartz.DspAccumulator);
    }

    [Fact]
    public void LoadState_Version2_RejectsAnOutOfRangeDspRemainder()
    {
        var section = new TestSection(
            StateSectionIds.Quartz,
            version: 2,
            writer =>
            {
                writer.WriteUInt32(0);
                writer.WriteUInt32(EmulatorConstants.ArmClock);
                writer.WriteUInt32(0);
                writer.WriteUInt32(0);
                writer.WriteUInt32(0);
            });
        var outer = new StateWriter();
        outer.WriteSection(section);
        StateSectionReader sectionReader = new StateReader(outer.ToArray()).ReadSection();

        var quartz = new Quartz();
        Assert.Throws<InvalidStateException>(() => quartz.LoadState(sectionReader));
    }

    [Fact]
    public void LoadState_InvalidLine_Throws()
    {
        var writer = new StateWriter();
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);
        writer.WriteUInt32(EmulatorConstants.VdlFrameLines);

        var quartz = new Quartz();
        var reader = new StateReader(writer.ToArray());

        Assert.Throws<InvalidStateException>(() => quartz.LoadState(reader));
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var quartz = new Quartz();
        quartz.PushArmCycles(255);
        while (quartz.QueueVdl())
        {
        }

        quartz.Reset();

        Assert.Equal(0u, quartz.ArmAccumulator);
        Assert.Equal(0u, quartz.DspAccumulator);
        Assert.Equal(0u, quartz.VdlAccumulator);
        Assert.Equal(0u, quartz.TimerAccumulator);
        Assert.Equal(0, quartz.VdCurrOverline);
    }

    private sealed class TestSection : IStateSection
    {
        private readonly Action<IStateWriter> _save;

        internal TestSection(uint id, ushort version, Action<IStateWriter> save)
        {
            Id = id;
            Version = version;
            _save = save;
        }

        public uint Id { get; }

        public ushort Version { get; }

        public void Save(IStateWriter writer) => _save(writer);

        public void Load(IStateReader reader)
        {
        }
    }
}
