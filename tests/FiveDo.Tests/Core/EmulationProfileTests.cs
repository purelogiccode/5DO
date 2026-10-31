using System;
using FiveDo.Core;

namespace FiveDo.Tests.Core;

public sealed class EmulationProfileTests
{
    [Fact]
    public void Compatible_KeepsTheFreeDoTimingBaseline()
    {
        EmulatorTuning tuning = EmulationProfiles.Apply(EmulationProfile.Compatible);

        Assert.Equal(ArmTimingMode.FreeDoCompatible, tuning.ArmTiming);
        Assert.Equal(ClioTimerMode.FreeDoCompatible, tuning.ClioTimers);
        Assert.Equal(CelTimingMode.FreeDoCompatible, tuning.CelTiming);
        Assert.Equal(CdromTimingMode.FreeDoCompatible, tuning.CdromTiming);
        Assert.Equal(CdSpeed.X2, tuning.CdSpeed);
        Assert.Equal(EmulatorConstants.ArmClock, tuning.ArmClock);
        Assert.Equal(EmulationProfile.Compatible, EmulationProfiles.Detect(tuning));
    }

    [Fact]
    public void Accuracy_EnablesTheOperaTimingModelsAtTheStockClock()
    {
        EmulatorTuning tuning = EmulationProfiles.Apply(EmulationProfile.Accuracy);

        Assert.Equal(ArmTimingMode.Arm60Datasheet, tuning.ArmTiming);
        Assert.Equal(ClioTimerMode.OperaScheduler, tuning.ClioTimers);
        Assert.Equal(CelTimingMode.OperaBusy, tuning.CelTiming);
        Assert.Equal(CdromTimingMode.OperaCycleBased, tuning.CdromTiming);
        Assert.Equal(CdSpeed.X2, tuning.CdSpeed);
        Assert.Equal(EmulatorConstants.ArmClock, tuning.ArmClock);
        Assert.Equal(EmulationProfile.Accuracy, EmulationProfiles.Detect(tuning));
    }

    [Fact]
    public void Performance_UsesTheOverclockWithFreeDoTiming()
    {
        EmulatorTuning tuning = EmulationProfiles.Apply(EmulationProfile.Performance);

        Assert.Equal(ArmTimingMode.FreeDoCompatible, tuning.ArmTiming);
        Assert.Equal(CdSpeed.X2, tuning.CdSpeed);
        Assert.Equal(
            (int)Math.Round(EmulatorConstants.ArmClock * EmulationProfiles.PerformanceCpuClockMultiplier),
            tuning.ArmClock);
        Assert.Equal(EmulationProfile.Performance, EmulationProfiles.Detect(tuning));
    }

    [Fact]
    public void Apply_PreservesPerGameFixesHighResolutionAndSeed()
    {
        var baseTuning = new EmulatorTuning
        {
            FixMode = FixMode.BitTiming6,
            HighResolution = true,
            RandomSeed = 0x1234,
            CpuBackend = CpuBackendKind.Interpreter,
        };

        EmulatorTuning accuracy = EmulationProfiles.Apply(EmulationProfile.Accuracy, baseTuning);

        Assert.Equal(FixMode.BitTiming6, accuracy.FixMode);
        Assert.True(accuracy.HighResolution);
        Assert.Equal(0x1234u, accuracy.RandomSeed);
        Assert.Equal(CpuBackendKind.Interpreter, accuracy.CpuBackend);
    }

    [Fact]
    public void Custom_LeavesTheOptionsUnchanged()
    {
        var tuning = new EmulatorTuning
        {
            ArmTiming = ArmTimingMode.Arm60Datasheet,
            CdSpeed = CdSpeed.X8,
            ArmClock = 25_000_000,
        };

        EmulatorTuning result = EmulationProfiles.Apply(EmulationProfile.Custom, tuning);

        Assert.Same(tuning, result);
    }

    [Fact]
    public void Detect_ReturnsCustomWhenAnyTimingOptionDiffers()
    {
        EmulatorTuning tuning = EmulationProfiles.Apply(EmulationProfile.Accuracy) with
        {
            CdSpeed = CdSpeed.X4,
        };

        Assert.Equal(EmulationProfile.Custom, EmulationProfiles.Detect(tuning));
    }

    [Fact]
    public void Validate_RejectsUnknownProfiles()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EmulationProfiles.Validate((EmulationProfile)99));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EmulationProfiles.Apply((EmulationProfile)99));
    }
}
