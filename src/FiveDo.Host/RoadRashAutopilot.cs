using System;
using FiveDo.Core;
using FiveDo.Input;
using FiveDo.Video;

namespace FiveDo.Host;

/// <summary>
/// A small visual autopilot for Road Rash. It holds the accelerate button and
/// steers toward the centre of the gray road surface in the lower part of the frame.
/// </summary>
/// <remarks>
/// This is a development aid for compatibility validation, not a replacement for
/// player input. It deliberately uses only the frame the emulator already produced
/// and therefore acts one frame behind the game.
/// </remarks>
public sealed class RoadRashAutopilot
{
    /// <summary>Screen X the road centre is steered toward.</summary>
    public const int TargetRoadCenterX = 230;

    private const int LookAheadBottom = 214;

    /// <summary>First scanline of the road-measure band.</summary>
    /// <remarks>
    /// The band starts low (near the bike) so building facades and windows above
    /// the horizon cannot outvote the asphalt. Restrict to the lower rows where
    /// the road surface dominates even in the city section.
    /// </remarks>
    private const int RoadMeasureTop = 190;
    private const int SteerHoldFrames = 10;
    private const int DecisionIntervalFrames = 16;

    /// <summary>Frames of stable off-centre road before stuck recovery triggers.</summary>
    /// <remarks>
    /// Opera reference note (libopera/opera_pbus.c, opera_pbus.h): the joypad
    /// packet carries discrete l/r/u/d/a/b/c bits, so steering here is bang-bang
    /// with a hold window rather than proportional. Recovery therefore has to
    /// come from earlier anticipation and throttle modulation, not finer steer.
    /// </remarks>
    public const int StuckTriggerFrames = 150;

    /// <summary>Frames stuck recovery steers away from the wall.</summary>
    public const int RecoveryFrames = 90;

    /// <summary>Frames at the start of recovery spent braking before re-applying throttle.</summary>
    public const int RecoveryBrakeFrames = 30;

    /// <summary>Road-centre jump between decisions treated as a wall re-lock.</summary>
    public const double RoadJumpThreshold = 12.0;

    /// <summary>Throttle duty cycle in built-up sections (frames).</summary>
    public const int PacingCycleFrames = 60;

    /// <summary>Throttled frames per pacing cycle in built-up sections.</summary>
    public const int PacingOnFrames = 40;

    /// <summary>Maximum consecutive coast frames before throttle is forced back on.</summary>
    public const int MaxCoastFrames = 20;

    /// <summary>LineX jump treated as detector unreliability rather than road motion.</summary>
    public const double UpsetJumpThreshold = 25.0;

    /// <summary>Frames to hold straight and pace after a detector upset.</summary>
    public const int UpsetHoldFrames = 30;

    /// <summary>Static frames before a stopped/wrecked bike is restarted.</summary>
    public const int StoppedTriggerFrames = 40;

    /// <summary>Frames to hold B after a stop.</summary>
    public const int CrashRestartFrames = 180;

    /// <summary>Moving-scene frames that end a restart early because the bike is rolling again.</summary>
    public const int CrashRestartMotionFrames = 10;

    /// <summary>Static frames before the post-race/menu A-advance starts.</summary>
    public const int MenuAdvanceStaticFrames = 120;

    /// <summary>Frames to hold A when advancing a post-race/menu screen.</summary>
    public const int MenuAdvanceHoldFrames = 20;

    /// <summary>Frames between A presses while a menu or cutscene is still static.</summary>
    public const int MenuAdvanceIntervalFrames = 60;

    /// <summary>Frames between A pulses when the road line has been frozen for a long time.</summary>
    public const int MenuAdvancePulseFrames = 300;

    /// <summary>Average palette-index change treated as "the scene is still".</summary>
    private const double StaticSceneThreshold = 2.0;

    /// <summary>
    /// Looser scene-still threshold for post-race/menu detection. Menus have a
    /// blinking cursor or animated marquee, so the strict 2.0 threshold resets
    /// continuously even though the bike is not racing.
    /// </summary>
    private const double MenuStaticThreshold = 6.0;

    private int[]? _sceneSignature;
    private int[]? _speedSignature;
    private int _staticFrames;
    private int _menuStaticFrames;
    private int _speedStaticFrames;
    private int _loadFrames;
    private int _lowCoverageFrames;
    private int _crashRestartMotionFrames;
    private long _crashRestartUntil;
    private long _crashRestartStart;
    private long _menuAdvanceUntil;
    private long _menuModeUntil;
    private long _lastMenuAdvanceFrame = -MenuAdvanceIntervalFrames;

    private bool _urgentObstacle;
    private int _steer;
    private long _steerUntil;
    private long _nextDecision;
    private double _decisionLineX = double.NaN;
    private double _previousLineX = double.NaN;
    private long _upsetUntil;
    private int _coastStreak;
    private double _stableLineX = double.NaN;
    private long _stableSince;
    private long _recoveryUntil;
    private long _recoveryStart;
    private int _recoverySteer;

    /// <summary>
    /// Gets or sets an optional probe that reports whether the CD is actively
    /// streaming. Load screens and cutscenes need A presses even when the scene
    /// signature keeps changing.
    /// </summary>
    public Func<bool>? IsLoadActive { get; set; }

    /// <summary>
    /// Gets or sets whether to advance long-static/post-race/menu screens with A.
    /// Enabled by the CLI's road autopilot; unit tests keep it off so the
    /// steering/recovery contracts stay focused.
    /// </summary>
    public bool EnableMenuAdvance { get; set; }

    /// <summary>Gets the controller state to apply on the next frame.</summary>
    public ControllerState State { get; private set; }

    /// <summary>Gets the most recent road-centre X, or <see cref="double.NaN"/>.</summary>
    public double LineX { get; private set; } = double.NaN;

    /// <summary>Gets the fraction of lower-band pixels classified as road.</summary>
    public double RoadCoverage { get; private set; } = double.NaN;

    /// <summary>Gets the current steering direction (-1 left, 0 centre, +1 right).</summary>
    public int Steering => _steer;

    /// <summary>Gets whether stuck recovery is currently steering away from a wall.</summary>
    public bool RecoveryActive { get; private set; }

    /// <summary>Gets whether a stopped/wrecked bike is being restarted.</summary>
    public bool CrashRestart { get; private set; }

    /// <summary>Gets whether a static post-race/menu screen is being advanced with A.</summary>
    public bool MenuAdvance { get; private set; }

    /// <summary>Gets the number of consecutive frames the scene has been still.</summary>
    public int StaticFrames => _staticFrames;

    /// <summary>Gets the obstacle-avoidance bias (-1 left, 0 none, +1 right).</summary>
    public int ObstacleSteer { get; private set; }

    /// <summary>Gets whether the driver is emergency-braking for a blocked road.</summary>
    public bool EmergencyBrake { get; private set; }

    /// <summary>Gets whether the driver is coasting through a narrow walled section.</summary>
    public bool Caution { get; private set; }

    /// <summary>Gets the lookahead road fractions (left/centre/right), or NaN when unavailable.</summary>
    public double LookaheadLeft { get; private set; } = double.NaN;

    /// <summary>Gets the lookahead road fractions (left/centre/right), or NaN when unavailable.</summary>
    public double LookaheadCentre { get; private set; } = double.NaN;

    /// <summary>Gets the lookahead road fractions (left/centre/right), or NaN when unavailable.</summary>
    public double LookaheadRight { get; private set; } = double.NaN;

    /// <summary>Gets whether urban pacing is duty-cycling the throttle.</summary>
    public bool UrbanPacing { get; private set; }

    /// <summary>Gets whether the road detector is currently distrusted after a jump.</summary>
    public bool DetectorUpset { get; private set; }

    /// <summary>Observes a completed frame and returns the next controller state.</summary>
    /// <param name="frame">The most recent video frame.</param>
    /// <param name="frameNumber">Absolute emulated frame number.</param>
    /// <returns>The controller state for the next frame.</returns>
    public ControllerState Update(VdlFrame frame, long frameNumber)
    {
        LineX = FindRoadCenter(frame, out var coverage);
        RoadCoverage = coverage;
        (ObstacleSteer, EmergencyBrake, Caution, _urgentObstacle) = FindObstacleState(frame, LineX);
        (LookaheadLeft, LookaheadCentre, LookaheadRight) = FindLookaheadFractions(frame);
        UrbanPacing = !double.IsNaN(LineX)
            && !double.IsNaN(LookaheadCentre)
            && LookaheadCentre > 0.3
            && Math.Min(LookaheadLeft, LookaheadRight) < 0.6;
        UpdateUpsetTracking(frameNumber);
        UpdateStuckTracking(frame, frameNumber);
        UpdateStopTracking(frame, frameNumber);

        if (frameNumber < _menuModeUntil)
        {
            // A post-race/menu/cutscene stayed static/loading for two seconds.
            // The game advances these screens with A (B does not); hold A for a
            // short pulse, then release everything until the next pulse so the
            // throttle does not fight the menu.
            RecoveryActive = false;
            _steer = 0;
            _steerUntil = frameNumber;
            _nextDecision = frameNumber + 4;
        }
        else if (CrashRestart)
        {
            // This is the wreck/remount or rider-running state, not a racing
            // line. Hold B with straight bars: manual probes showed B alone
            // remounts (34,500 and 80,000 -> 52 mph), while steering keeps
            // the rider running in place or drives into the wall. A cyclic
            // right/left search was tried and drove the 83,000 run into the
            // BUSTED screen; throttle-only took it to 91,000 live frames.
            RecoveryActive = false;
            _steer = 0;
            _steerUntil = frameNumber + SteerHoldFrames;
            _nextDecision = frameNumber + 4;
        }
        else if (RecoveryActive && frameNumber < _recoveryUntil)
        {
            _steer = _recoverySteer;
            _steerUntil = frameNumber + SteerHoldFrames;
        }
        else if (ObstacleSteer != 0 && _urgentObstacle)
        {
            // Urgent close traffic: the lookahead says the centre is blocked
            // and only a narrow side opening remains. Apply the escape
            // immediately instead of waiting for the normal decision
            // interval. From 33,801-33,886 the other racer kept the old
            // interval straight for 6-16 frames at a time, so the bike still
            // drifted into it. Keep ordinary obstacle steering on the v9
            // interval: applying every obstacle bias immediately oversteered
            // into a taxi at ~32,000 in the clean 30k run.
            RecoveryActive = false;
            _steer = ObstacleSteer;
            _steerUntil = frameNumber + SteerHoldFrames;
            _nextDecision = frameNumber + 4;
        }
        else if (DetectorUpset)
        {
            // The detector just jumped between surfaces (road/sidewalk): hold
            // straight on the pacing duty instead of chasing a false lock
            // off the road. Throttle is duty-cycled (not cut) so the bike
            // keeps rolling: a stopped bike gets busted.
            RecoveryActive = false;
            _steer = 0;
            _steerUntil = frameNumber + SteerHoldFrames;
            _nextDecision = Math.Max(_nextDecision, frameNumber + 4);
        }
        else if (EmergencyBrake && ObstacleSteer == 0)
        {
            // The road ahead is fully blocked and no side is open enough to
            // aim for. Keep the last avoidance steer and brake; do not chase
            // a LineX that has just locked onto the tree or wall that caused
            // the block. At 33,890 LineX jumped to 240 and the old path
            // steered right into the trunk while braking.
            RecoveryActive = false;
            _steerUntil = frameNumber + SteerHoldFrames;
            _nextDecision = frameNumber + 4;
        }
        else
        {
            RecoveryActive = false;
            if (frameNumber >= _nextDecision)
            {
                if (ObstacleSteer != 0)
                {
                    _steer = ObstacleSteer;
                    _steerUntil = frameNumber + SteerHoldFrames;
                    _nextDecision = frameNumber + DecisionIntervalFrames;
                }
                else if (double.IsNaN(LineX))
                {
                    _steer = 0;
                    _nextDecision = frameNumber + 4;
                }
                else
                {
                    var error = LineX - TargetRoadCenterX;
                    var centreDir = error > 8 ? 1 : error < -8 ? -1 : 0;
                    var sideSteer = FindSideSteer(frame, out _);
                    var jumped = !double.IsNaN(_decisionLineX)
                        && !double.IsNaN(LineX)
                        && Math.Abs(LineX - _decisionLineX) > RoadJumpThreshold;
                    if (jumped && sideSteer != 0)
                    {
                        // The centre estimate drifted fast since the last
                        // decision: it is re-locking onto a wall/sidewalk, so
                        // trust the open-road side, not the drift.
                        _steer = sideSteer;
                    }
                    else if (centreDir != 0 && sideSteer != 0 && sideSteer != centreDir)
                    {
                        // The centre estimate disagrees with the open road: the
                        // detector is locked onto a building or sidewalk, so steer
                        // toward the side with visibly more road.
                        _steer = sideSteer;
                    }
                    else
                    {
                        _steer = centreDir;
                    }

                    _decisionLineX = LineX;
                    _steerUntil = frameNumber + SteerHoldFrames;
                    _nextDecision = frameNumber + (centreDir == 0 && _steer == 0 ? 4 : DecisionIntervalFrames);
                }
            }
        }

        if (frameNumber > _steerUntil)
        {
            _steer = 0;
        }

        ControllerButton buttons;
        if (frameNumber < _menuAdvanceUntil)
        {
            buttons = ControllerButton.A;
        }
        else if (frameNumber < _menuModeUntil)
        {
            buttons = ControllerButton.None;
        }
        else if (CrashRestart)
        {
            // A stopped bike is caught and busted; get it rolling before
            // resuming obstacle/pacing logic.
            buttons = ControllerButton.B;
        }
        else if (RecoveryActive && frameNumber < _recoveryUntil
            && frameNumber - _recoveryStart < RecoveryBrakeFrames)
        {
            // Wedged against a wall with the throttle pinned only digs in:
            // brake first, then re-apply throttle with the recovery steer.
            buttons = ControllerButton.A;
        }
        else if (EmergencyBrake)
        {
            buttons = ControllerButton.A;
        }
        else if (IsBendLift())
        {
            // Steering off-centre into a narrowing bend with the target side
            // open: lift the throttle so the bike rotates instead of
            // understeering into the outside wall.
            buttons = 0;
        }
        else if (IsPacingCoast(frameNumber))
        {
            // Built-up section with edges close on at least one side: cap
            // speed with a throttle duty cycle. Wall/tree touches at full
            // city speed end the race (BUSTED); finishing slowly beats that.
            buttons = 0;
        }
        else if (Caution)
        {
            buttons = 0;
        }
        else
        {
            buttons = ControllerButton.B;
        }

        if (_steer < 0)
        {
            buttons |= ControllerButton.Left;
        }
        else if (_steer > 0)
        {
            buttons |= ControllerButton.Right;
        }

        // A stopped bike gets busted. Coasting (pacing, caution, bend lift) may
        // slow the bike but must never stall it: force the throttle back on
        // after a bounded coast streak. Deliberate braking is exempt.
        var coasting = (buttons & (ControllerButton.B | ControllerButton.A)) == 0;
        if (coasting && !EmergencyBrake && !MenuAdvance)
        {
            _coastStreak++;
            if (_coastStreak > MaxCoastFrames)
            {
                buttons |= ControllerButton.B;
                _coastStreak = 0;
            }
        }
        else
        {
            _coastStreak = 0;
        }

        State = new ControllerState(buttons);
        return State;
    }

    private static double FindRoadCenter(VdlFrame frame, out double coverage)
    {
        var total = 0.0;
        var rows = 0;
        var totalPixels = 0;
        var roadPixels = 0;
        var bottom = Math.Min(LookAheadBottom, frame.Lines.Length);
        var top = Math.Min(RoadMeasureTop, bottom);
        for (var y = top; y < bottom; y += 2)
        {
            var line = frame.Lines[y];
            var width = Math.Min(line.Width, line.PixelBuffer.Length);
            var bestStart = 0;
            var bestLength = 0;
            var runStart = -1;
            for (var x = 20; x < width; x++)
            {
                totalPixels++;
                if (IsRoadPixel(line, x))
                {
                    roadPixels++;
                    if (runStart < 0)
                    {
                        runStart = x;
                    }
                }
                else if (runStart >= 0)
                {
                    var length = x - runStart;
                    if (length > bestLength)
                    {
                        bestLength = length;
                        bestStart = runStart;
                    }

                    runStart = -1;
                }
            }

            if (runStart >= 0)
            {
                var length = width - runStart;
                if (length > bestLength)
                {
                    bestLength = length;
                    bestStart = runStart;
                }
            }

            if (bestLength >= 100)
            {
                total += bestStart + (bestLength / 2.0);
                rows++;
            }
        }

        coverage = totalPixels == 0 ? double.NaN : (double)roadPixels / totalPixels;
        return rows == 0 ? double.NaN : total / rows;
    }

    /// <summary>
    /// Checks the lookahead band above the road-measure area for traffic directly
    /// ahead. Returns the steer bias (-1/0/+1), emergency-brake and caution
    /// flags, and whether the obstacle is urgent close traffic that must
    /// preempt the normal decision interval.
    /// </summary>
    private static (int Steer, bool Brake, bool Caution, bool Urgent) FindObstacleState(
        VdlFrame frame,
        double lineX)
    {
        var (leftFraction, centreFraction, rightFraction) = FindLookaheadFractions(frame);
        if (double.IsNaN(leftFraction))
        {
            return (0, false, false, false);
        }

        // Fully blocked ahead while still on the road: brake instead of ramming it.
        // (When LineX is NaN the game is in FMV/menus, so keep holding B to advance.)
        if (!double.IsNaN(lineX)
            && centreFraction < 0.2
            && leftFraction < 0.35
            && rightFraction < 0.35)
        {
            return (0, true, false, false);
        }

        // Narrow walled canyon with road ahead: coast instead of accelerating hard.
        if (!double.IsNaN(lineX)
            && centreFraction > 0.4
            && leftFraction < 0.4
            && rightFraction < 0.4)
        {
            return (0, false, true, false);
        }

        // Steering toward a wall while the other side is open: steer away instead.
        // The road-centre estimate can lock onto a building facade or sidewalk, so
        // the lookahead side fractions are the trustworthy signal here.
        if (!double.IsNaN(lineX))
        {
            if (lineX < TargetRoadCenterX - 8 && leftFraction < 0.4 && rightFraction > 0.7)
            {
                return (1, false, false, false);
            }

            if (lineX > TargetRoadCenterX + 8 && rightFraction < 0.4 && leftFraction > 0.7)
            {
                return (-1, false, false, false);
            }
        }

        // Sharp bend: the centre and one side are blocked while the other side
        // is wide open. Steering alone at full throttle understeers into the
        // outside wall (city pin at ~33250: lookahead 0.39/0.29/0.94 took the
        // bend flat out and wedged). Brake while steering to the open side.
        if (!double.IsNaN(lineX)
            && centreFraction < 0.35
            && Math.Max(leftFraction, rightFraction) > 0.8
            && Math.Min(leftFraction, rightFraction) < 0.4)
        {
            return (leftFraction > rightFraction ? -1 : 1, true, false, false);
        }

        // Close traffic/tree fills the centre and one side, leaving only a
        // narrow opening. At 33,820-33,850 the other racer produced lookahead
        // 0.43/0.07/0.17 and then 0.44/0.00/0.00; every rule above needed a
        // 0.6-wide opening, so the bike ran straight into the racer. Steer
        // into the opening and lift the throttle while the gap is narrow.
        // Do not brake: a 25-frame full brake from speed 43 stopped the bike
        // dead, and a stopped bike is caught and busted. This runs even when
        // LineX is NaN: the lower-band detector loses its 100-px run exactly
        // when the obstacle is close enough to fill the view.
        if (centreFraction < 0.2
            && Math.Max(leftFraction, rightFraction) is >= 0.3 and < 0.6
            && Math.Abs(leftFraction - rightFraction) > 0.2)
        {
            return (leftFraction > rightFraction ? -1 : 1, false, true, true);
        }

        // Closing side: steering toward a partially blocked side while the
        // other side is clearly open (tree alley at ~31800: lookahead
        // 0.46/0.95/1.00 steered left into the roadside tree). Steer to the
        // open side and lift while crossing over.
        if (!double.IsNaN(lineX))
        {
            if (lineX < TargetRoadCenterX - 8 && leftFraction < 0.6 && rightFraction > 0.8)
            {
                return (1, false, true, false);
            }

            if (lineX > TargetRoadCenterX + 8 && rightFraction < 0.6 && leftFraction > 0.8)
            {
                return (-1, false, true, false);
            }
        }

        if (centreFraction < 0.35
            && Math.Max(leftFraction, rightFraction) > 0.6
            && Math.Abs(leftFraction - rightFraction) > 0.25)
        {
            return (leftFraction > rightFraction ? -1 : 1, false, false, false);
        }

        // Earlier wall anticipation: the centre is narrowing while one side is
        // clearly more open. Steer to the open side before the lower band locks
        // onto the wall, even when the bike still looks centred.
        if (centreFraction < 0.55
            && Math.Max(leftFraction, rightFraction) > 0.6
            && Math.Abs(leftFraction - rightFraction) > 0.3)
        {
            return (leftFraction > rightFraction ? -1 : 1, false, false, false);
        }

        return (0, false, false, false);
    }

    /// <summary>
    /// Measures the road fraction in the lookahead band thirds (left 120-190,
    /// centre 190-250, right 250-310). Returns NaNs when the band is empty.
    /// </summary>
    private static (double Left, double Centre, double Right) FindLookaheadFractions(VdlFrame frame)
    {
        var top = Math.Min(150, frame.Lines.Length);
        var leftRoad = 0;
        var leftTotal = 0;
        var centreRoad = 0;
        var centreTotal = 0;
        var rightRoad = 0;
        var rightTotal = 0;
        for (var y = 120; y < top; y += 2)
        {
            var line = frame.Lines[y];
            var width = Math.Min(line.Width, line.PixelBuffer.Length);
            for (var x = 120; x < Math.Min(190, width); x++)
            {
                leftTotal++;
                if (IsRoadPixel(line, x))
                {
                    leftRoad++;
                }
            }

            for (var x = 190; x < Math.Min(250, width); x++)
            {
                centreTotal++;
                if (IsRoadPixel(line, x))
                {
                    centreRoad++;
                }
            }

            for (var x = 250; x < Math.Min(310, width); x++)
            {
                rightTotal++;
                if (IsRoadPixel(line, x))
                {
                    rightRoad++;
                }
            }
        }

        if (centreTotal == 0 || leftTotal == 0 || rightTotal == 0)
        {
            return (double.NaN, double.NaN, double.NaN);
        }

        return ((double)leftRoad / leftTotal, (double)centreRoad / centreTotal, (double)rightRoad / rightTotal);
    }

    /// <summary>
    /// Compares the road fraction on the left and right of the lower band.
    /// Used by stuck recovery to steer toward open road when the centre estimate
    /// cannot be trusted. Returns -1/0/+1.
    /// </summary>
    private static int FindSideSteer(VdlFrame frame, out double difference)
    {
        var bottom = Math.Min(LookAheadBottom, frame.Lines.Length);
        var top = Math.Min(RoadMeasureTop, bottom);
        var leftRoad = 0;
        var leftTotal = 0;
        var rightRoad = 0;
        var rightTotal = 0;
        for (var y = top; y < bottom; y += 2)
        {
            var line = frame.Lines[y];
            var width = Math.Min(line.Width, line.PixelBuffer.Length);
            for (var x = 20; x < Math.Min(160, width); x++)
            {
                leftTotal++;
                if (IsRoadPixel(line, x))
                {
                    leftRoad++;
                }
            }

            for (var x = 160; x < Math.Min(300, width); x++)
            {
                rightTotal++;
                if (IsRoadPixel(line, x))
                {
                    rightRoad++;
                }
            }
        }

        if (leftTotal == 0 || rightTotal == 0)
        {
            difference = 0;
            return 0;
        }

        var leftFraction = (double)leftRoad / leftTotal;
        var rightFraction = (double)rightRoad / rightTotal;
        difference = Math.Abs(leftFraction - rightFraction);
        if (difference <= 0.5)
        {
            return 0;
        }

        return leftFraction > rightFraction ? -1 : 1;
    }

    /// <summary>
    /// Lifts the throttle while steering off-centre into a narrowing bend whose
    /// target side is open. Used for the throttle decision; pure steering state
    /// is handled by the caller.
    /// </summary>
    private bool IsBendLift()
    {
        if (_steer == 0 || double.IsNaN(LineX) || Math.Abs(LineX - TargetRoadCenterX) <= 8)
        {
            return false;
        }

        if (double.IsNaN(LookaheadCentre))
        {
            return false;
        }

        var sideFraction = _steer < 0 ? LookaheadLeft : LookaheadRight;
        return LookaheadCentre < 0.7 && sideFraction > 0.8;
    }

    /// <summary>
    /// Duty-cycles the throttle in built-up sections (road visible ahead but
    /// edges close on at least one side). Returns true on coast frames.
    /// Open road, FMV/menus (NaN), and braking states are unaffected.
    /// </summary>
    private bool IsPacingCoast(long frameNumber)
    {
        return UrbanPacing && (frameNumber % PacingCycleFrames) >= PacingOnFrames;
    }

    /// <summary>
    /// Tracks detector reliability across frames. A sudden LineX jump means the
    /// detector re-locked onto a different gray surface (sidewalk, plaza) rather
    /// than road motion; distrust it for a short hold instead of chasing it.
    /// The test is frame-to-frame, so a frozen line cannot latch the hold.
    /// </summary>
    private void UpdateUpsetTracking(long frameNumber)
    {
        DetectorUpset = frameNumber < _upsetUntil;
        if (double.IsNaN(LineX))
        {
            _previousLineX = double.NaN;
            return;
        }

        if (!double.IsNaN(_previousLineX)
            && Math.Abs(LineX - _previousLineX) > UpsetJumpThreshold)
        {
            _upsetUntil = frameNumber + UpsetHoldFrames;
            DetectorUpset = true;
        }

        _previousLineX = LineX;
    }

    /// <summary>
    /// Detects the wreck/remount and rider-running states from a still scene.
    /// Steering while the rider is down keeps the bike pinned or running in
    /// place; the manual probes showed B alone remounts at 34,500 and 80,000.
    /// </summary>
    private void UpdateStopTracking(VdlFrame frame, long frameNumber)
    {
        if (frameNumber < _menuModeUntil
            && !double.IsNaN(LineX)
            && RoadCoverage > 0.7)
        {
            // The race is back on screen; stop the menu pulse immediately.
            _menuModeUntil = frameNumber;
            _menuAdvanceUntil = frameNumber;
        }

        MenuAdvance = frameNumber < _menuModeUntil;
        CrashRestart = frameNumber < _crashRestartUntil;
        if (CrashRestart)
        {
            // A remount needs throttle-only, but a traffic stop that starts
            // rolling must resume steering immediately. At 91,600 the bike was
            // moving at 2 mph while the 180-frame straight restart was still
            // active; it then drove off the road at 91,820.
            if (_staticFrames == 0)
            {
                _crashRestartMotionFrames++;
            }
            else
            {
                _crashRestartMotionFrames = 0;
            }

            if (_crashRestartMotionFrames >= CrashRestartMotionFrames)
            {
                _crashRestartUntil = frameNumber;
                CrashRestart = false;
                _crashRestartMotionFrames = 0;
            }
        }
        else
        {
            _crashRestartMotionFrames = 0;
        }

        var signature = BuildSceneSignature(frame);
        if (_sceneSignature is null)
        {
            _sceneSignature = signature;
            return;
        }

        var difference = 0L;
        for (var i = 0; i < signature.Length; i++)
        {
            difference += Math.Abs(signature[i] - _sceneSignature[i]);
        }

        var average = (double)difference / signature.Length;
        if (average <= StaticSceneThreshold)
        {
            _staticFrames++;
        }
        else
        {
            _staticFrames = 0;
            _sceneSignature = signature;
        }

        if (average <= MenuStaticThreshold)
        {
            _menuStaticFrames++;
        }
        else
        {
            _menuStaticFrames = 0;
        }

        var speedSignature = BuildSpeedSignature(frame);
        if (_speedSignature is null)
        {
            _speedSignature = speedSignature;
        }
        else
        {
            var speedDifference = 0;
            for (var i = 0; i < speedSignature.Length; i++)
            {
                speedDifference += Math.Abs(speedSignature[i] - _speedSignature[i]);
            }

            if ((double)speedDifference / speedSignature.Length <= 1.0)
            {
                _speedStaticFrames++;
            }
            else
            {
                _speedStaticFrames = 0;
                _speedSignature = speedSignature;
            }
        }

        if (IsLoadActive?.Invoke() == true)
        {
            _loadFrames++;
        }
        else
        {
            _loadFrames = 0;
        }

        if (!double.IsNaN(RoadCoverage) && RoadCoverage < 0.5)
        {
            _lowCoverageFrames++;
        }
        else
        {
            _lowCoverageFrames = 0;
        }

        var sceneStatic = Math.Max(_menuStaticFrames, _speedStaticFrames);
        var restartStuck = CrashRestart
            && frameNumber - _crashRestartStart >= CrashRestartFrames;
        var menuSignal = sceneStatic >= MenuAdvanceStaticFrames
            || _loadFrames >= MenuAdvanceStaticFrames
            || _lowCoverageFrames >= MenuAdvanceStaticFrames
            || restartStuck;
        var menuInterval = !restartStuck && sceneStatic >= MenuAdvanceStaticFrames
            ? MenuAdvanceIntervalFrames
            : MenuAdvancePulseFrames;
        if (EnableMenuAdvance
            && !RecoveryActive
            && menuSignal
            && frameNumber - _lastMenuAdvanceFrame >= menuInterval)
        {
            // The regular restart keeps B held and resets _staticFrames every
            // 40 frames, so use separate long-static, frozen-line, CD-load, and
            // low-coverage counters. A race keeps coverage high (0.8-0.93) and a
            // scrolling road line; a load/menu/cutscene does not. A advances
            // those screens (B does not).
            _lastMenuAdvanceFrame = frameNumber;
            _menuAdvanceUntil = frameNumber + MenuAdvanceHoldFrames;
            _menuModeUntil = frameNumber + MenuAdvancePulseFrames;
            _crashRestartUntil = frameNumber;
            _crashRestartMotionFrames = 0;
            CrashRestart = false;
            MenuAdvance = true;
        }

        // A still scene while the driver is applying throttle means the bike
        // is stopped, whether against a wall (35,000) or in the middle of an
        // open road after a wreck (45,500). Do not gate on the road detector:
        // it can call a wall "open road" and it can see a clear road while
        // the rider stands beside a stopped bike.
        if (frameNumber >= _crashRestartUntil
            && !RecoveryActive
            && _staticFrames >= StoppedTriggerFrames)
        {
            _crashRestartUntil = frameNumber + CrashRestartFrames;
            _crashRestartStart = frameNumber;
            _staticFrames = 0;
            _crashRestartMotionFrames = 0;
            CrashRestart = true;
        }
    }

    /// <summary>
    /// Samples the road scene on a coarse grid. Palette indices are enough:
    /// while riding, the road texture scrolls and the samples change; while
    /// stopped or crashed, the same cells keep the same indices.
    /// </summary>
    private static int[] BuildSceneSignature(VdlFrame frame)
    {
        const int columns = 12;
        const int rows = 6;
        const int speedSamples = 12;
        var signature = new int[(columns * rows) + speedSamples];
        var index = 0;
        // Sample the left scenery, not the centre where the rider runs back
        // to the bike. The first version covered x=40-270 and the rider's run
        // animation kept resetting the still-scene counter at 34,000.
        for (var row = 0; row < rows; row++)
        {
            var y = 96 + (row * 12);
            if (y >= frame.Lines.Length)
            {
                index += columns;
                continue;
            }

            var line = frame.Lines[y];
            var width = Math.Min(line.Width, line.PixelBuffer.Length);
            for (var column = 0; column < columns; column++)
            {
                var x = 20 + (column * 10);
                signature[index++] = x < width ? line.PixelBuffer[x] : 0;
            }
        }

        // The speed readout is the most direct "is the bike moving" signal:
        // it changes while riding and stays at 0 through a wreck or rider run.
        for (var row = 0; row < 2; row++)
        {
            var y = 212 + (row * 12);
            if (y >= frame.Lines.Length)
            {
                index += 6;
                continue;
            }

            var line = frame.Lines[y];
            var width = Math.Min(line.Width, line.PixelBuffer.Length);
            for (var column = 0; column < 6; column++)
            {
                var x = 196 + (column * 12);
                signature[index++] = x < width ? line.PixelBuffer[x] : 0;
            }
        }

        return signature;
    }

    /// <summary>
    /// Samples only the speed readout. A stopped/menu/cutscene frame keeps the
    /// readout constant even when scenery or a cursor elsewhere animates.
    /// </summary>
    private static int[] BuildSpeedSignature(VdlFrame frame)
    {
        var signature = new int[12];
        var index = 0;
        for (var row = 0; row < 2; row++)
        {
            var y = 212 + (row * 12);
            if (y >= frame.Lines.Length)
            {
                index += 6;
                continue;
            }

            var line = frame.Lines[y];
            var width = Math.Min(line.Width, line.PixelBuffer.Length);
            for (var column = 0; column < 6; column++)
            {
                var x = 196 + (column * 12);
                signature[index++] = x < width ? line.PixelBuffer[x] : 0;
            }
        }

        return signature;
    }

    private void UpdateStuckTracking(VdlFrame frame, long frameNumber)
    {
        if (RecoveryActive)
        {
            return;
        }

        if (double.IsNaN(LineX) || Math.Abs(LineX - TargetRoadCenterX) <= 8)
        {
            _stableLineX = LineX;
            _stableSince = frameNumber;
            return;
        }

        if (double.IsNaN(_stableLineX) || Math.Abs(LineX - _stableLineX) >= 2)
        {
            _stableLineX = LineX;
            _stableSince = frameNumber;
            return;
        }

        if (frameNumber - _stableSince >= StuckTriggerFrames)
        {
            RecoveryActive = true;
            var sideSteer = FindSideSteer(frame, out _);
            _recoverySteer = sideSteer != 0
                ? sideSteer
                : LineX < TargetRoadCenterX ? 1 : -1;
            _recoveryStart = frameNumber;
            _recoveryUntil = frameNumber + RecoveryFrames;
            _stableLineX = double.NaN;
            _stableSince = frameNumber;
        }
    }

    private static bool IsRoadPixel(VdlLine line, int x)
    {
        var index = line.PixelBuffer[x] & 0x1f;
        int r = line.ClutR[index];
        int g = line.ClutG[index];
        int b = line.ClutB[index];
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var brightness = (r + g + b) / 3;
        // Note: dark grass (41,65,49) also passes this filter. Requiring
        // blue >= green was tried on 2026-10-25 but made the road detector
        // lose the asphalt in the city and pinned the bike at ~45,000, so
        // the proven v9/v10 brightness+chroma test is kept.
        return max - min <= 40 && brightness is >= 45 and <= 120;
    }
}
