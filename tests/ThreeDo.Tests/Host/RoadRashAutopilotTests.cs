using System;
using ThreeDo.Core;
using ThreeDo.Host;
using ThreeDo.Input;

namespace ThreeDo.Tests.Host;

public sealed class RoadRashAutopilotTests
{
    [Fact]
    public void Update_SteersTowardTheRoadCentreAndHoldsAccelerate()
    {
        var autopilot = new RoadRashAutopilot();
        var frame = MakeRoadFrame(roadLeft: 60, roadRight: 260);

        var state = autopilot.Update(frame, 0);

        Assert.True(state.IsDown(ControllerButton.B));
        Assert.True(state.IsDown(ControllerButton.Left));
        Assert.False(state.IsDown(ControllerButton.Right));
        Assert.Equal(160, autopilot.LineX);
    }

    [Fact]
    public void Update_SteersRightWhenTheRoadCentreIsRightOfTarget()
    {
        var autopilot = new RoadRashAutopilot();
        var frame = MakeRoadFrame(roadLeft: 220, roadRight: 320);

        var state = autopilot.Update(frame, 0);

        Assert.True(state.IsDown(ControllerButton.B));
        Assert.True(state.IsDown(ControllerButton.Right));
        Assert.False(state.IsDown(ControllerButton.Left));
        Assert.Equal(270, autopilot.LineX);
    }

    [Fact]
    public void Update_ReleasesSteeringWhenTheRoadIsNearTheTarget()
    {
        var autopilot = new RoadRashAutopilot();
        var frame = MakeRoadFrame(roadLeft: 140, roadRight: 320);

        var state = autopilot.Update(frame, 0);

        Assert.True(state.IsDown(ControllerButton.B));
        Assert.False(state.IsDown(ControllerButton.Left));
        Assert.False(state.IsDown(ControllerButton.Right));
    }

    private static VdlFrame MakeRoadFrame(int roadLeft, int roadRight)
    {
        var frame = new VdlFrame();
        foreach (var line in frame.Lines)
        {
            line.Clear();
            line.ClutR[0] = 0;
            line.ClutG[0] = 120;
            line.ClutB[0] = 0;
            line.ClutR[1] = 90;
            line.ClutG[1] = 90;
            line.ClutB[1] = 90;
        }

        for (var y = 140; y < 220; y++)
        {
            var line = frame.Lines[y];
            for (var x = 0; x < 320; x++)
            {
                line.PixelBuffer[x] = (ushort)(x >= roadLeft && x < roadRight ? 1 : 0);
            }

            line.Width = 320;
        }

        return frame;
    }
}
