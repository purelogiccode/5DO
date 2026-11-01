using FiveDo.Host;

namespace FiveDo.Tests.Host;

/// <summary>Frame-skip cadence and presentation-selection tests.</summary>
public sealed class FrameSkipPolicyTests
{
    [Theory]
    [InlineData(FrameSkipMode.Off, 1)]
    [InlineData(FrameSkipMode.Auto, 1)]
    [InlineData(FrameSkipMode.OneInTwo, 2)]
    [InlineData(FrameSkipMode.OneInThree, 3)]
    [InlineData(FrameSkipMode.OneInFour, 4)]
    [InlineData(FrameSkipMode.OneInFive, 5)]
    public void Cadence_MatchesTheSelectedMode(FrameSkipMode mode, int expected)
    {
        Assert.Equal(expected, FrameSkipPolicy.Cadence(mode));
    }

    [Fact]
    public void Off_PresentsEveryFrame()
    {
        for (var i = 0; i < 20; i++)
        {
            Assert.True(FrameSkipPolicy.ShouldPresent(FrameSkipMode.Off, i));
        }
    }

    [Fact]
    public void OneInTwo_PresentsEverySecondFrame()
    {
        var presented = 0;
        for (var i = 0; i < 20; i++)
        {
            if (FrameSkipPolicy.ShouldPresent(FrameSkipMode.OneInTwo, i))
            {
                presented++;
            }
        }

        Assert.Equal(10, presented);
        Assert.True(FrameSkipPolicy.ShouldPresent(FrameSkipMode.OneInTwo, 0));
        Assert.False(FrameSkipPolicy.ShouldPresent(FrameSkipMode.OneInTwo, 1));
    }

    [Fact]
    public void Auto_UsesTheRunnerCadence()
    {
        Assert.True(FrameSkipPolicy.ShouldPresent(FrameSkipMode.Auto, 0, autoCadence: 1));
        Assert.True(FrameSkipPolicy.ShouldPresent(FrameSkipMode.Auto, 0, autoCadence: 3));
        Assert.False(FrameSkipPolicy.ShouldPresent(FrameSkipMode.Auto, 1, autoCadence: 3));
        Assert.True(FrameSkipPolicy.ShouldPresent(FrameSkipMode.Auto, 3, autoCadence: 3));
        Assert.True(FrameSkipPolicy.ShouldPresent(FrameSkipMode.Auto, 1, autoCadence: 0));
    }

    [Fact]
    public void Describe_LabelsFixedAndAutoModes()
    {
        Assert.Equal("off", FrameSkipPolicy.Describe(FrameSkipMode.Off));
        Assert.Equal("auto", FrameSkipPolicy.Describe(FrameSkipMode.Auto));
        Assert.Equal("auto x2", FrameSkipPolicy.Describe(FrameSkipMode.Auto, 2));
        Assert.Equal("1 of 3", FrameSkipPolicy.Describe(FrameSkipMode.OneInThree));
    }
}
