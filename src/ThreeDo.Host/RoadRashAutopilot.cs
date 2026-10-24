using System;
using ThreeDo.Core;
using ThreeDo.Input;
using ThreeDo.Video;

namespace ThreeDo.Host;

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
    private const int SteerHoldFrames = 10;
    private const int DecisionIntervalFrames = 16;

    private int _steer;
    private long _steerUntil;
    private long _nextDecision;

    /// <summary>Gets the controller state to apply on the next frame.</summary>
    public ControllerState State { get; private set; }

    /// <summary>Gets the most recent road-centre X, or <see cref="double.NaN"/>.</summary>
    public double LineX { get; private set; } = double.NaN;

    /// <summary>Gets the current steering direction (-1 left, 0 centre, +1 right).</summary>
    public int Steering => _steer;

    /// <summary>Observes a completed frame and returns the next controller state.</summary>
    /// <param name="frame">The most recent video frame.</param>
    /// <param name="frameNumber">Absolute emulated frame number.</param>
    /// <returns>The controller state for the next frame.</returns>
    public ControllerState Update(VdlFrame frame, long frameNumber)
    {
        LineX = FindRoadCenter(frame);

        if (frameNumber >= _nextDecision)
        {
            if (double.IsNaN(LineX))
            {
                _steer = 0;
                _nextDecision = frameNumber + 4;
            }
            else
            {
                var error = LineX - TargetRoadCenterX;
                if (error > 8)
                {
                    _steer = 1;
                    _steerUntil = frameNumber + SteerHoldFrames;
                    _nextDecision = frameNumber + DecisionIntervalFrames;
                }
                else if (error < -8)
                {
                    _steer = -1;
                    _steerUntil = frameNumber + SteerHoldFrames;
                    _nextDecision = frameNumber + DecisionIntervalFrames;
                }
                else
                {
                    _steer = 0;
                    _nextDecision = frameNumber + 4;
                }
            }
        }

        if (frameNumber > _steerUntil)
        {
            _steer = 0;
        }

        var buttons = ControllerButton.B;
        if (_steer < 0)
        {
            buttons |= ControllerButton.Left;
        }
        else if (_steer > 0)
        {
            buttons |= ControllerButton.Right;
        }

        State = new ControllerState(buttons);
        return State;
    }

    private static double FindRoadCenter(VdlFrame frame)
    {
        var total = 0.0;
        var rows = 0;
        var bottom = Math.Min(LookAheadBottom, frame.Lines.Length);
        for (var y = 150; y < bottom; y += 2)
        {
            var line = frame.Lines[y];
            var width = Math.Min(line.Width, line.PixelBuffer.Length);
            var bestStart = 0;
            var bestLength = 0;
            var runStart = -1;
            for (var x = 20; x < width; x++)
            {
                var index = line.PixelBuffer[x] & 0x1f;
                int r = line.ClutR[index];
                int g = line.ClutG[index];
                int b = line.ClutB[index];
                var max = Math.Max(r, Math.Max(g, b));
                var min = Math.Min(r, Math.Min(g, b));
                var brightness = (r + g + b) / 3;
                var road = max - min <= 40 && brightness is >= 45 and <= 150;
                if (road)
                {
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

        return rows == 0 ? double.NaN : total / rows;
    }
}
