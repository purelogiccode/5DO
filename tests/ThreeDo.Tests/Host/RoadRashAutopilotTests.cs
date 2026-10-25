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
        Assert.True(autopilot.RoadCoverage > 0.5);
    }

    [Fact]
    public void Update_SteersRightWhenTheRoadCentreIsRightOfTarget()
    {
        var autopilot = new RoadRashAutopilot();
        var frame = MakeRoadFrame(roadLeft: 220, roadRight: 320);
        OpenLookahead(frame);

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

    [Fact]
    public void Update_SteersAroundAnObstacleDirectlyAhead()
    {
        var autopilot = new RoadRashAutopilot();
        // Road centred near the target, but the centre/left lookahead band is
        // blocked while the right side stays clear.
        var frame = MakeRoadFrame(roadLeft: 130, roadRight: 330);
        BlockLookahead(frame, x0: 160, x1: 250);

        var state = autopilot.Update(frame, 0);

        Assert.Equal(1, autopilot.ObstacleSteer);
        Assert.True(state.IsDown(ControllerButton.B));
        Assert.True(state.IsDown(ControllerButton.Right));
        Assert.False(state.IsDown(ControllerButton.Left));
    }

    [Fact]
    public void Update_RecoversAfterBeingStuckOffCentre()
    {
        var autopilot = new RoadRashAutopilot();
        // Road centre well left of target; the same view never changes.
        // (Road 60-260 keeps the lookahead open so no brake rule fires.)
        var frame = MakeRoadFrame(roadLeft: 60, roadRight: 260);
        OpenLookahead(frame);

        ControllerState state = default;
        for (long f = 0; f < RoadRashAutopilot.StuckTriggerFrames + 40; f++)
        {
            ShimmerLookahead(frame, f);
            state = autopilot.Update(frame, f);
        }

        Assert.True(autopilot.RecoveryActive);
        // Stuck left of target, so recovery steers right away from the wall.
        Assert.True(state.IsDown(ControllerButton.Right));
        Assert.True(state.IsDown(ControllerButton.B));
    }

    [Fact]
    public void Update_BrakesWhenTheRoadAheadIsFullyBlocked()
    {
        var autopilot = new RoadRashAutopilot();
        var frame = MakeRoadFrame(roadLeft: 60, roadRight: 320);
        BlockLookahead(frame, x0: 120, x1: 310);

        var state = autopilot.Update(frame, 0);

        Assert.True(autopilot.EmergencyBrake);
        Assert.True(state.IsDown(ControllerButton.A));
        Assert.False(state.IsDown(ControllerButton.B));
    }

    [Fact]
    public void Update_AnticipatesAWallBeforeTheCentreIsBlocked()
    {
        var autopilot = new RoadRashAutopilot();
        // Lower band centred on target, but the lookahead centre is narrowing
        // while the right side stays open: steer right early.
        var frame = MakeRoadFrame(roadLeft: 130, roadRight: 330);
        NarrowLookahead(frame, leftRoad: (120, 130), centreRoad: (190, 220), rightRoad: (250, 310));

        var state = autopilot.Update(frame, 0);

        Assert.Equal(1, autopilot.ObstacleSteer);
        Assert.True(state.IsDown(ControllerButton.Right));
        Assert.True(state.IsDown(ControllerButton.B));
    }

    [Fact]
    public void Update_BrakesIntoASharpBend()
    {
        var autopilot = new RoadRashAutopilot();
        // Lower band on the road, but the lookahead centre and left are
        // blocked while the right is wide open: brake into the bend instead
        // of taking it at full throttle.
        var frame = MakeRoadFrame(roadLeft: 130, roadRight: 330);
        NarrowLookahead(frame, leftRoad: (120, 130), centreRoad: (190, 200), rightRoad: (250, 310));

        var state = autopilot.Update(frame, 0);

        Assert.Equal(1, autopilot.ObstacleSteer);
        Assert.True(autopilot.EmergencyBrake);
        Assert.True(state.IsDown(ControllerButton.A));
        Assert.False(state.IsDown(ControllerButton.B));
        Assert.True(state.IsDown(ControllerButton.Right));
    }

    [Fact]
    public void Update_BrakesAndSteersIntoANarrowGapAroundCloseTraffic()
    {
        var autopilot = new RoadRashAutopilot();
        // A racer fills the centre and right while only a narrow strip of the
        // left lookahead is open (0.43/0.00/0.00): lift and aim for the gap.
        // The old obstacle rule needed a 0.6-wide opening, so the driver ran
        // straight into the racer at ~33,850. Braking here stopped the bike
        // dead and got it busted, so this rule only lifts the throttle.
        var frame = MakeRoadFrame(roadLeft: 60, roadRight: 260);
        NarrowLookahead(frame, leftRoad: (120, 150), centreRoad: (0, 0), rightRoad: (0, 0));

        var state = autopilot.Update(frame, 0);

        Assert.Equal(-1, autopilot.ObstacleSteer);
        Assert.True(autopilot.Caution);
        Assert.False(autopilot.EmergencyBrake);
        Assert.True(state.IsDown(ControllerButton.Left));
        Assert.False(state.IsDown(ControllerButton.A));
        Assert.False(state.IsDown(ControllerButton.B));
    }

    [Fact]
    public void Update_AppliesObstacleSteeringWithoutWaitingForTheDecisionInterval()
    {
        var autopilot = new RoadRashAutopilot();
        // First decision: road left of target, so the normal 16-frame
        // decision interval is running.
        var first = MakeRoadFrame(roadLeft: 60, roadRight: 260);
        OpenLookahead(first);
        autopilot.Update(first, 0);

        // Four frames later an obstacle fills the left and centre while the
        // right has a narrow opening. The old code ignored ObstacleSteer
        // until the interval expired, which let the bike drift into the
        // racer/tree at 33,801-33,886.
        var second = MakeRoadFrame(roadLeft: 130, roadRight: 330);
        NarrowLookahead(second, leftRoad: (0, 0), centreRoad: (0, 0), rightRoad: (250, 280));
        var state = autopilot.Update(second, 4);

        Assert.Equal(1, autopilot.ObstacleSteer);
        Assert.True(state.IsDown(ControllerButton.Right));
        Assert.False(state.IsDown(ControllerButton.Left));
    }

    [Fact]
    public void Update_HoldsTheEscapeSteerWhileEmergencyBraking()
    {
        var autopilot = new RoadRashAutopilot();
        // Narrow right-side opening with the centre blocked: escape right.
        var first = MakeRoadFrame(roadLeft: 130, roadRight: 330);
        NarrowLookahead(first, leftRoad: (0, 0), centreRoad: (0, 0), rightRoad: (250, 310));
        var firstState = autopilot.Update(first, 0);
        Assert.True(autopilot.EmergencyBrake);
        Assert.True(firstState.IsDown(ControllerButton.Right));

        // Fully blocked ahead, same lower-band road: brake and keep the last
        // escape steer instead of chasing the lower-band centre (which is
        // already at the target here) back to straight.
        var second = MakeRoadFrame(roadLeft: 130, roadRight: 330);
        BlockLookahead(second, x0: 120, x1: 310);
        var state = autopilot.Update(second, 20);

        Assert.True(autopilot.EmergencyBrake);
        Assert.True(state.IsDown(ControllerButton.A));
        Assert.True(state.IsDown(ControllerButton.Right));
        Assert.False(state.IsDown(ControllerButton.Left));
    }

    [Fact]
    public void Update_RestartsAStoppedBikeWithThrottleOnly()
    {
        var autopilot = new RoadRashAutopilot();
        // Wreck/remount view: the scene is still and the road ahead is
        // blocked. Manual probes showed B alone remounts (34,500 and 80,000
        // -> 52 mph); steering kept the rider running in place or drove into
        // the wall, so the restart holds B with straight bars.
        var frame = MakeRoadFrame(roadLeft: 40, roadRight: 200);
        BlockLookahead(frame, x0: 120, x1: 310);

        ControllerState state = default;
        for (long f = 0; f <= RoadRashAutopilot.StoppedTriggerFrames + 5; f++)
        {
            state = autopilot.Update(frame, f);
        }

        Assert.True(autopilot.CrashRestart);
        Assert.True(state.IsDown(ControllerButton.B));
        Assert.False(state.IsDown(ControllerButton.A));
        Assert.False(state.IsDown(ControllerButton.Left));
        Assert.False(state.IsDown(ControllerButton.Right));
    }

    [Fact]
    public void Update_EndsCrashRestartWhenTheSceneMovesAgain()
    {
        var autopilot = new RoadRashAutopilot();
        var frame = MakeRoadFrame(roadLeft: 40, roadRight: 200);
        BlockLookahead(frame, x0: 120, x1: 310);

        for (long f = 0; f <= RoadRashAutopilot.StoppedTriggerFrames + 5; f++)
        {
            autopilot.Update(frame, f);
        }

        Assert.True(autopilot.CrashRestart);

        ControllerState state = default;
        for (var f = RoadRashAutopilot.StoppedTriggerFrames + 6;
            f <= RoadRashAutopilot.StoppedTriggerFrames + RoadRashAutopilot.CrashRestartMotionFrames + 8;
            f++)
        {
            ShimmerLookahead(frame, f);
            state = autopilot.Update(frame, f);
        }

        Assert.False(autopilot.CrashRestart);
        Assert.True(state.IsDown(ControllerButton.B));
    }

    [Fact]
    public void Update_DoesNotRestartWhileTheSceneIsMoving()
    {
        var autopilot = new RoadRashAutopilot();
        // A moving view with open road is a normal race: the stopped-scene
        // detector must not override the driver just because the fixture is
        // otherwise stable.
        var frame = MakeRoadFrame(roadLeft: 140, roadRight: 320);
        OpenLookahead(frame);

        for (long f = 0; f <= RoadRashAutopilot.StoppedTriggerFrames + 40; f++)
        {
            ShimmerLookahead(frame, f);
            autopilot.Update(frame, f);
        }

        Assert.False(autopilot.CrashRestart);
    }

    [Fact]
    public void Update_RestartsAfterStoppingOnOpenRoad()
    {
        var autopilot = new RoadRashAutopilot();
        // At 45,500 the bike stopped in the middle of the road after a
        // wreck; the road ahead looked open (0.57/0.99/0.93) and the old
        // gate kept the driver pacing forever. A still scene with throttle
        // applied is enough to restart.
        var frame = MakeRoadFrame(roadLeft: 140, roadRight: 320);
        OpenLookahead(frame);

        ControllerState state = default;
        for (long f = 0; f <= RoadRashAutopilot.StoppedTriggerFrames + 5; f++)
        {
            state = autopilot.Update(frame, f);
        }

        Assert.True(autopilot.CrashRestart);
        Assert.True(state.IsDown(ControllerButton.B));
    }

    [Fact]
    public void Update_RecoveryBrakesBeforeReapplyingThrottle()
    {
        var autopilot = new RoadRashAutopilot();
        // Road centre well left of target; the same view never changes.
        // (Road 60-260 keeps the lookahead open so no brake rule fires.)
        var frame = MakeRoadFrame(roadLeft: 60, roadRight: 260);
        OpenLookahead(frame);

        ControllerState state = default;
        for (long f = 0; f <= RoadRashAutopilot.StuckTriggerFrames + 10; f++)
        {
            ShimmerLookahead(frame, f);
            state = autopilot.Update(frame, f);
        }

        Assert.True(autopilot.RecoveryActive);
        // Braking phase: A held instead of B while still steering away.
        Assert.True(state.IsDown(ControllerButton.Right));
        Assert.True(state.IsDown(ControllerButton.A));
        Assert.False(state.IsDown(ControllerButton.B));

        for (long f = RoadRashAutopilot.StuckTriggerFrames + 12;
            f <= RoadRashAutopilot.StuckTriggerFrames + RoadRashAutopilot.RecoveryBrakeFrames + 20;
            f++)
        {
            ShimmerLookahead(frame, f);
            state = autopilot.Update(frame, f);
        }

        // Throttle phase: B held again with the recovery steer.
        Assert.True(state.IsDown(ControllerButton.Right));
        Assert.True(state.IsDown(ControllerButton.B));
    }

    [Fact]
    public void Update_LiftsThrottleIntoANarrowingBend()
    {
        var autopilot = new RoadRashAutopilot();
        // Off-centre road steering left while the lookahead centre narrows
        // with the left side open: coast so the bike rotates instead of
        // understeering into the outside wall.
        var frame = MakeRoadFrame(roadLeft: 60, roadRight: 260);
        NarrowLookahead(frame, leftRoad: (120, 190), centreRoad: (190, 230), rightRoad: (250, 260));

        var state = autopilot.Update(frame, 0);

        Assert.True(state.IsDown(ControllerButton.Left));
        Assert.False(state.IsDown(ControllerButton.A));
        Assert.False(state.IsDown(ControllerButton.B));
    }

    [Fact]
    public void Update_AvoidsAClosingSide()
    {
        var autopilot = new RoadRashAutopilot();
        // Left of target with the left lookahead partially blocked and the
        // right wide open: cross to the open side and lift while doing so.
        var frame = MakeRoadFrame(roadLeft: 100, roadRight: 200);
        NarrowLookahead(frame, leftRoad: (120, 160), centreRoad: (190, 250), rightRoad: (250, 310));

        var state = autopilot.Update(frame, 0);

        Assert.Equal(1, autopilot.ObstacleSteer);
        Assert.True(autopilot.Caution);
        Assert.True(state.IsDown(ControllerButton.Right));
        Assert.False(state.IsDown(ControllerButton.A));
        Assert.False(state.IsDown(ControllerButton.B));
    }

    [Fact]
    public void Update_PacesThrottleInBuiltUpSections()
    {
        var autopilot = new RoadRashAutopilot();
        // Road visible ahead but the right edge is close: duty-cycle the
        // throttle instead of holding it flat out.
        var frame = MakeRoadFrame(roadLeft: 60, roadRight: 260);

        var onPhase = autopilot.Update(frame, 0);
        Assert.True(autopilot.UrbanPacing);
        Assert.True(onPhase.IsDown(ControllerButton.B));

        var offPhase = autopilot.Update(frame, RoadRashAutopilot.PacingOnFrames + 10);
        Assert.True(autopilot.UrbanPacing);
        Assert.False(offPhase.IsDown(ControllerButton.B));
        Assert.False(offPhase.IsDown(ControllerButton.A));
    }

    [Fact]
    public void Update_HoldsStraightWhenTheDetectorJumps()
    {
        var autopilot = new RoadRashAutopilot();
        // Road centred, then the detector re-locks 160px away (sidewalk):
        // hold straight instead of chasing the false lock off-road, keeping
        // the pacing duty so the bike keeps rolling.
        var first = MakeRoadFrame(roadLeft: 140, roadRight: 320);
        autopilot.Update(first, 0);

        var second = MakeRoadFrame(roadLeft: 20, roadRight: 120);
        OpenLookahead(second);
        var state = autopilot.Update(second, 4);

        Assert.True(autopilot.DetectorUpset);
        Assert.False(state.IsDown(ControllerButton.Left));
        Assert.False(state.IsDown(ControllerButton.Right));
        Assert.False(state.IsDown(ControllerButton.A));
        Assert.True(state.IsDown(ControllerButton.B));
    }

    [Fact]
    public void Update_ForcesThrottleBackOnAfterALongCoast()
    {
        var autopilot = new RoadRashAutopilot();
        // Same narrow-canyon view every frame: Caution would coast forever,
        // but a long coast stalls the bike and gets it busted, so the floor
        // forces the throttle back on.
        var frame = MakeRoadFrame(roadLeft: 60, roadRight: 260);
        NarrowLookahead(frame, leftRoad: (120, 130), centreRoad: (190, 250), rightRoad: (250, 260));

        var forced = false;
        ControllerState state = default;
        for (long f = 0; f <= RoadRashAutopilot.MaxCoastFrames + 4; f++)
        {
            state = autopilot.Update(frame, f);
            forced |= state.IsDown(ControllerButton.B);
        }

        Assert.True(forced);
    }

    [Fact]
    public void Update_SteersToOpenRoadWhenTheCentreDriftsFast()
    {
        var autopilot = new RoadRashAutopilot();
        // First decision: road left of target with the right side open.
        var first = MakeRoadFrame(roadLeft: 116, roadRight: 316);
        autopilot.Update(first, 0);

        // The centre drifts 14px to the target while the lower band stays
        // strongly right-sided: treat it as a wall re-lock, not convergence.
        var second = MakeRoadFrame(roadLeft: 140, roadRight: 320);
        var state = autopilot.Update(second, 16);

        Assert.True(state.IsDown(ControllerButton.Right));
    }

    [Fact]
    public void Update_AdvancesALongStaticMenuWithA()
    {
        var autopilot = new RoadRashAutopilot { EnableMenuAdvance = true };
        var frame = MakeRoadFrame(roadLeft: 60, roadRight: 260);
        var sawMenuAdvance = false;
        var sawA = false;

        for (long f = 0; f < RoadRashAutopilot.MenuAdvanceStaticFrames + 80; f++)
        {
            var state = autopilot.Update(frame, f);
            if (!autopilot.MenuAdvance)
            {
                continue;
            }

            sawMenuAdvance = true;
            Assert.False(state.IsDown(ControllerButton.B));
            sawA |= state.IsDown(ControllerButton.A);
        }

        Assert.True(sawMenuAdvance);
        Assert.True(sawA);
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
            // A second road colour used to animate a synthetic moving scene
            // without changing the road fractions.
            line.ClutR[10] = 90;
            line.ClutG[10] = 90;
            line.ClutB[10] = 90;
        }

        for (var y = 120; y < 220; y++)
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

    private static void BlockLookahead(VdlFrame frame, int x0, int x1)
    {
        for (var y = 120; y < 150; y++)
        {
            var line = frame.Lines[y];
            for (var x = x0; x < x1; x++)
            {
                line.PixelBuffer[x] = 0;
            }

            line.Width = 320;
        }
    }

    /// <summary>
    /// Alternates the lookahead band between two road colours so the scene
    /// signature changes as it would while riding. Both colours are road, so
    /// the measured fractions and lower-band centre are unaffected.
    /// </summary>
    private static void ShimmerLookahead(VdlFrame frame, long frameNumber)
    {
        var index = (ushort)(frameNumber % 2 == 0 ? 1 : 10);
        for (var y = 120; y < 150; y++)
        {
            var line = frame.Lines[y];
            for (var x = 120; x < 310; x++)
            {
                line.PixelBuffer[x] = index;
            }

            line.Width = 320;
        }

        // The stopped-scene signature samples the left scenery (rows
        // 96-156, x 20-140); animate that too so a moving test view never
        // looks like a wreck.
        for (var y = 96; y <= 156; y += 12)
        {
            var line = frame.Lines[y];
            for (var x = 20; x < 140; x++)
            {
                line.PixelBuffer[x] = index;
            }

            line.Width = 320;
        }
    }

    private static void OpenLookahead(VdlFrame frame)
    {
        for (var y = 120; y < 150; y++)
        {
            var line = frame.Lines[y];
            for (var x = 120; x < 310; x++)
            {
                line.PixelBuffer[x] = 1;
            }

            line.Width = 320;
        }
    }

    private static void NarrowLookahead(
        VdlFrame frame,
        (int Left, int Right) leftRoad,
        (int Left, int Right) centreRoad,
        (int Left, int Right) rightRoad)
    {
        for (var y = 120; y < 150; y++)
        {
            var line = frame.Lines[y];
            for (var x = 120; x < 310; x++)
            {
                var road = (x >= leftRoad.Left && x < leftRoad.Right)
                    || (x >= centreRoad.Left && x < centreRoad.Right)
                    || (x >= rightRoad.Left && x < rightRoad.Right);
                line.PixelBuffer[x] = (ushort)(road ? 1 : 0);
            }

            line.Width = 320;
        }
    }
}
