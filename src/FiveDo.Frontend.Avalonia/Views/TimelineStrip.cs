using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using FiveDo.Input;
using FiveDo.Tas;

namespace FiveDo.Frontend.Avalonia.Views;

/// <summary>
/// Draws an input timeline as one row per 3DO pad button with a selectable playhead.
/// </summary>
public sealed class TimelineStrip : Control
{
    private static readonly ControllerButton[] Buttons =
    [
        ControllerButton.A,
        ControllerButton.B,
        ControllerButton.C,
        ControllerButton.P,
        ControllerButton.X,
        ControllerButton.L,
        ControllerButton.R,
        ControllerButton.Up,
        ControllerButton.Down,
        ControllerButton.Left,
        ControllerButton.Right,
    ];

    private static readonly IBrush[] RowBrushes =
    [
        new SolidColorBrush(Color.Parse("#E4572E")),
        new SolidColorBrush(Color.Parse("#F3A712")),
        new SolidColorBrush(Color.Parse("#A8C686")),
        new SolidColorBrush(Color.Parse("#669BBC")),
        new SolidColorBrush(Color.Parse("#C9ADA7")),
        new SolidColorBrush(Color.Parse("#9BC53D")),
        new SolidColorBrush(Color.Parse("#5BC0EB")),
        new SolidColorBrush(Color.Parse("#D81159")),
        new SolidColorBrush(Color.Parse("#8F2D56")),
        new SolidColorBrush(Color.Parse("#218380")),
        new SolidColorBrush(Color.Parse("#F2C14E")),
    ];

    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#101418"));
    private static readonly IBrush GridBrush = new SolidColorBrush(Color.Parse("#2A3138"));
    private static readonly IBrush PlayheadBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly Pen PlayheadPen = new(PlayheadBrush, 2);
    private static readonly Pen GridPen = new(GridBrush);

    private InputTimeline? _timeline;
    private int _selectedFrame;

    /// <summary>Initializes a new timeline strip.</summary>
    public TimelineStrip()
    {
        Focusable = true;
        MinHeight = 180;
    }

    /// <summary>Raised when the user selects a frame.</summary>
    public event EventHandler<FrameSelectedEventArgs>? FrameSelected;

    /// <summary>Gets or sets the timeline to draw.</summary>
    public InputTimeline? Timeline
    {
        get => _timeline;
        set
        {
            _timeline = value;
            InvalidateVisual();
        }
    }

    /// <summary>Gets or sets the selected frame.</summary>
    public int SelectedFrame
    {
        get => _selectedFrame;
        set
        {
            _selectedFrame = Math.Max(0, value);
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var width = Bounds.Width;
        var height = Bounds.Height;
        context.FillRectangle(BackgroundBrush, new Rect(0, 0, width, height));
        if (_timeline is null || _timeline.FrameCount <= 0 || width <= 0 || height <= 0)
        {
            return;
        }

        var rowHeight = height / Buttons.Length;
        for (var frame = 60; frame < _timeline.FrameCount; frame += 60)
        {
            var x = frame / (double)_timeline.FrameCount * width;
            context.DrawLine(GridPen, new Point(x, 0), new Point(x, height));
        }

        for (var row = 0; row < Buttons.Length; row++)
        {
            ControllerButton button = Buttons[row];
            for (var index = 0; index < _timeline.Entries.Count; index++)
            {
                InputScriptEntry entry = _timeline.Entries[index];
                if (!entry.State.IsDown(button))
                {
                    continue;
                }

                var end = index + 1 < _timeline.Entries.Count
                    ? _timeline.Entries[index + 1].Frame
                    : _timeline.FrameCount;
                var x = entry.Frame / (double)_timeline.FrameCount * width;
                var right = Math.Max(x + 1, end / (double)_timeline.FrameCount * width);
                context.FillRectangle(
                    RowBrushes[row],
                    new Rect(x, row * rowHeight + 1, Math.Max(1, right - x), Math.Max(1, rowHeight - 2)));
            }
        }

        var playhead = Math.Clamp(_selectedFrame, 0, _timeline.FrameCount - 1)
            / (double)_timeline.FrameCount * width;
        context.DrawLine(PlayheadPen, new Point(playhead, 0), new Point(playhead, height));
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerPressed(e);
        if (_timeline is null || _timeline.FrameCount <= 0 || Bounds.Width <= 0)
        {
            return;
        }

        var point = e.GetPosition(this);
        var frame = (int)Math.Clamp(
            point.X / Bounds.Width * _timeline.FrameCount,
            0,
            _timeline.FrameCount - 1);
        SelectedFrame = frame;
        FrameSelected?.Invoke(this, new FrameSelectedEventArgs(frame));
        e.Handled = true;
    }
}

/// <summary>Frame selection details from <see cref="TimelineStrip"/>.</summary>
/// <param name="frame">Selected zero-based frame.</param>
public sealed class FrameSelectedEventArgs(int frame) : EventArgs
{
    /// <summary>Gets the selected zero-based frame.</summary>
    public int Frame { get; } = frame;
}
