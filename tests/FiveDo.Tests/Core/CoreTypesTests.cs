using System;
using FiveDo.Core;

namespace FiveDo.Tests.Core;

public sealed class CoreTypesTests
{
    [Fact]
    public void EmulatorTuning_DefaultsMatchReference()
    {
        EmulatorTuning tuning = EmulatorTuning.Default;

        Assert.Equal(12_500_000, tuning.ArmClock);
        Assert.Equal(FixMode.None, tuning.FixMode);
        Assert.Equal(CpuBackendKind.Interpreter, tuning.CpuBackend);
        Assert.False(tuning.HighResolution);
        Assert.Equal(ArmTimingMode.FreeDoCompatible, tuning.ArmTiming);
        Assert.Equal(ClioTimerMode.FreeDoCompatible, tuning.ClioTimers);
        tuning.Validate();
    }

    [Fact]
    public void EmulatorTuning_AcceptsBothArmTimingModes()
    {
        new EmulatorTuning { ArmTiming = ArmTimingMode.FreeDoCompatible }.Validate();
        new EmulatorTuning { ArmTiming = ArmTimingMode.Arm60Datasheet }.Validate();
    }

    [Fact]
    public void EmulatorTuning_UnknownArmTiming_Throws()
    {
        var tuning = new EmulatorTuning { ArmTiming = (ArmTimingMode)99 };

        Assert.Throws<ArgumentOutOfRangeException>(() => tuning.Validate());
    }

    [Fact]
    public void EmulatorTuning_AcceptsBothClioTimerModes()
    {
        new EmulatorTuning { ClioTimers = ClioTimerMode.FreeDoCompatible }.Validate();
        new EmulatorTuning { ClioTimers = ClioTimerMode.OperaScheduler }.Validate();
    }

    [Fact]
    public void EmulatorTuning_UnknownClioTimerMode_Throws()
    {
        var tuning = new EmulatorTuning { ClioTimers = (ClioTimerMode)99 };

        Assert.Throws<ArgumentOutOfRangeException>(() => tuning.Validate());
    }

    [Fact]
    public void EmulatorTuning_JitBackend_IsRejectedUntilImplemented()
    {
        var tuning = new EmulatorTuning { CpuBackend = CpuBackendKind.Jit };

        Assert.Throws<ArgumentOutOfRangeException>(() => tuning.Validate());
    }

    [Fact]
    public void EmulatorTuning_InvalidArmClock_Throws()
    {
        var tuning = new EmulatorTuning { ArmClock = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => tuning.Validate());
    }

    [Fact]
    public void FixMode_ComposesFlags()
    {
        FixMode mode = FixMode.BitTiming1 | FixMode.GraphicsStepY;

        Assert.True(mode.HasFlag(FixMode.BitTiming1));
        Assert.True(mode.HasFlag(FixMode.GraphicsStepY));
        Assert.False(mode.HasFlag(FixMode.BitTiming6));
    }

    [Fact]
    public void GameQuirks_NoneHasNoFlags()
    {
        GameQuirks quirks = GameQuirks.None;

        Assert.Equal(FixMode.None, quirks.FixMode);
        Assert.False(quirks.SpeedFixes);
        Assert.False(quirks.UnknownFlag11);
        Assert.False(quirks.Jw);
    }

    [Fact]
    public void CompatibilityState_ResetRestoresDefaults()
    {
        var state = new CompatibilityState
        {
            FixMode = FixMode.BitTiming1,
            IsAnvil = 30,
            BiosAnvil = true,
            SpeedFixes = 200000,
            Sf = 1,
            Sdf = 2,
            UnknownFlag11 = 100000,
            Jw = 3,
            Cnbfix = true,
            HighResolution = true,
        };

        state.Reset();

        Assert.Equal(FixMode.None, state.FixMode);
        Assert.Equal(0, state.IsAnvil);
        Assert.False(state.BiosAnvil);
        Assert.Equal(0, state.SpeedFixes);
        Assert.Equal(5_000_000, state.Sf);
        Assert.Equal(0, state.Sdf);
        Assert.Equal(0, state.UnknownFlag11);
        Assert.Equal(0, state.Jw);
        Assert.False(state.Cnbfix);
        Assert.False(state.HighResolution);
    }

    [Fact]
    public void FrameBuffer_HasExpectedDimensionsAndStride()
    {
        var frame = new FrameBuffer(320, 240);

        Assert.Equal(320, frame.Width);
        Assert.Equal(240, frame.Height);
        Assert.Equal(320 * 4, frame.Stride);
        Assert.Equal(320 * 4 * 240, frame.Pixels.Length);
        Assert.Equal(PixelFormat.Bgra8888, frame.Format);
    }

    [Fact]
    public void FrameBuffer_Clear_ZeroesPixels()
    {
        var frame = new FrameBuffer(2, 2);
        frame.PixelSpan[0] = 0xFF;

        frame.Clear();

        Assert.All(frame.PixelSpan.ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void FrameBuffer_InvalidDimensions_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameBuffer(0, 240));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameBuffer(320, 0));
    }

    [Fact]
    public void VdlLine_DefaultsAndWidthValidation()
    {
        var line = new VdlLine();

        Assert.Equal(320, line.Width);
        Assert.Equal(320, line.Pixels.Length);
        Assert.Equal(VdlLine.MaxPixels, line.PixelBuffer.Length);
        Assert.Equal(32, line.ClutB.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => line.Width = VdlLine.MaxPixels + 1);
    }

    [Fact]
    public void VdlLine_Clear_ResetsPixelsAndControls()
    {
        var line = new VdlLine
        {
            Width = 8,
            OutControl = 1,
            ClutDma = 2,
            Background = 3,
        };
        line.Pixels[0] = 0xFFFF;
        line.ClutR[0] = 0xFF;

        line.Clear();

        Assert.Equal(0, line.Pixels[0]);
        Assert.Equal(0, line.ClutR[0]);
        Assert.Equal(0u, line.OutControl);
        Assert.Equal(0u, line.ClutDma);
        Assert.Equal(0u, line.Background);
        Assert.Equal(320, line.Width);
    }

    [Fact]
    public void VdlFrame_HasReservedHighResolutionLines()
    {
        var frame = new VdlFrame();

        Assert.Equal(VdlFrame.MaxLines, frame.Lines.Length);
        Assert.Equal(320, frame.SourceWidth);
        Assert.Equal(240, frame.SourceHeight);
    }

    [Fact]
    public void VdlFrame_Clear_ResetsSourceDimensions()
    {
        var frame = new VdlFrame
        {
            SourceWidth = 640,
            SourceHeight = 480,
        };

        frame.Clear();

        Assert.Equal(320, frame.SourceWidth);
        Assert.Equal(240, frame.SourceHeight);
    }

    [Fact]
    public void AudioSample_PackedRoundtrip()
    {
        var sample = new AudioSample(-1234, 5678);

        var packed = sample.Packed;
        var restored = AudioSample.FromPacked(packed);

        Assert.Equal(sample, restored);
        Assert.Equal(unchecked((ushort)-1234), packed & 0xFFFF);
        Assert.Equal(5678u, packed >> 16);
    }
}
