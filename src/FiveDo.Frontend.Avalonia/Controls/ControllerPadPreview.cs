using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FiveDo.Input;

namespace FiveDo.Frontend.Avalonia.Controls;

/// <summary>
/// Draws a 3DO gamepad diagram for the input-bindings editor. Buttons with a keyboard or
/// gamepad binding are outlined, the selected binding row's button is highlighted, and
/// held controls light their mapped button.
/// </summary>
/// <remarks>
/// The geometry follows the 4DO JohnnyInput controller picture (219 x 98 reference
/// units) so the diagram is familiar to users migrating from 4DO. Everything is drawn
/// with Avalonia primitives; no image assets are required.
/// </remarks>
public sealed class ControllerPadPreview : Control
{
    /// <summary>Defines the <see cref="SelectedButton"/> property.</summary>
    public static readonly StyledProperty<ControllerButton> SelectedButtonProperty =
        AvaloniaProperty.Register<ControllerPadPreview, ControllerButton>(nameof(SelectedButton));

    /// <summary>Defines the <see cref="PressedButtons"/> property.</summary>
    public static readonly StyledProperty<ControllerButton> PressedButtonsProperty =
        AvaloniaProperty.Register<ControllerPadPreview, ControllerButton>(nameof(PressedButtons));

    /// <summary>Defines the <see cref="MappedButtons"/> property.</summary>
    public static readonly StyledProperty<ControllerButton> MappedButtonsProperty =
        AvaloniaProperty.Register<ControllerPadPreview, ControllerButton>(nameof(MappedButtons));

    private const double ReferenceWidth = 219;
    private const double ReferenceHeight = 98;
    private const double MinimumWidth = 96;
    private const double MinimumHeight = 44;

    private static readonly IBrush BodyFill = new SolidColorBrush(Color.Parse("#3A3F4A"));
    private static readonly IPen BodyPen = new Pen(new SolidColorBrush(Color.Parse("#22252C")), 1.2);
    private static readonly IBrush PlateFill = new SolidColorBrush(Color.Parse("#C8CDD7"));
    private static readonly IBrush ButtonFill = new SolidColorBrush(Color.Parse("#E9EBEF"));
    private static readonly IBrush ButtonText = new SolidColorBrush(Color.Parse("#22252C"));
    private static readonly IBrush ShoulderFill = new SolidColorBrush(Color.Parse("#4B515D"));
    private static readonly IBrush ShoulderText = new SolidColorBrush(Color.Parse("#F2F4F8"));
    private static readonly IBrush MappedFill = new SolidColorBrush(Color.Parse("#F5F8FE"));
    private static readonly IBrush SelectedFill = new SolidColorBrush(Color.Parse("#F4B942"));
    private static readonly IBrush PressedFill = new SolidColorBrush(Color.Parse("#4CD97B"));
    private static readonly IBrush DarkText = new SolidColorBrush(Color.Parse("#101318"));
    private static readonly IPen MappedPen = new Pen(new SolidColorBrush(Color.Parse("#5AA9FF")), 1.6);
    private static readonly IPen SelectedPen = new Pen(Brushes.White, 1.8);
    private static readonly IPen PressedPen = new Pen(new SolidColorBrush(Color.Parse("#C9FFDB")), 1.8);

    static ControllerPadPreview()
    {
        AffectsRender<ControllerPadPreview>(
            SelectedButtonProperty,
            PressedButtonsProperty,
            MappedButtonsProperty);
    }

    /// <summary>Gets or sets the button whose binding row is selected.</summary>
    public ControllerButton SelectedButton
    {
        get => GetValue(SelectedButtonProperty);
        set => SetValue(SelectedButtonProperty, value);
    }

    /// <summary>Gets or sets the buttons whose mapped controls are currently held.</summary>
    public ControllerButton PressedButtons
    {
        get => GetValue(PressedButtonsProperty);
        set => SetValue(PressedButtonsProperty, value);
    }

    /// <summary>Gets or sets the buttons that have a keyboard or gamepad binding.</summary>
    public ControllerButton MappedButtons
    {
        get => GetValue(MappedButtonsProperty);
        set => SetValue(MappedButtonsProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Rect bounds = new(Bounds.Size);
        if (bounds.Width < MinimumWidth || bounds.Height < MinimumHeight)
        {
            return;
        }

        double scale = Math.Min(bounds.Width / ReferenceWidth, bounds.Height / ReferenceHeight);
        double offsetX = (bounds.Width - (ReferenceWidth * scale)) / 2;
        double offsetY = (bounds.Height - (ReferenceHeight * scale)) / 2;

        // Shoulder buttons sit behind the body.
        DrawRoundedButton(
            context,
            Map(new Rect(13, 1, 54, 17), scale, offsetX, offsetY),
            6 * scale,
            ControllerButton.L,
            ShoulderFill,
            ShoulderText,
            "L",
            8 * scale);

        DrawRoundedButton(
            context,
            Map(new Rect(152, 1, 54, 17), scale, offsetX, offsetY),
            6 * scale,
            ControllerButton.R,
            ShoulderFill,
            ShoulderText,
            "R",
            8 * scale);

        // Body: a rounded bridge between two grips.
        context.DrawRectangle(
            BodyFill,
            BodyPen,
            new RoundedRect(Map(new Rect(28, 12, 163, 78), scale, offsetX, offsetY), 26 * scale));
        Point leftGrip = MapPoint(40, 52, scale, offsetX, offsetY);
        Point rightGrip = MapPoint(179, 52, scale, offsetX, offsetY);
        context.DrawEllipse(BodyFill, BodyPen, leftGrip, 40 * scale, 43 * scale);
        context.DrawEllipse(BodyFill, BodyPen, rightGrip, 40 * scale, 43 * scale);

        // The two light circular face plates.
        context.DrawEllipse(PlateFill, null, MapPoint(42, 53, scale, offsetX, offsetY), 32 * scale, 31 * scale);
        context.DrawEllipse(PlateFill, null, MapPoint(179, 50, scale, offsetX, offsetY), 32 * scale, 31 * scale);

        // D-pad: each arm is a separate hit target so one direction can light.
        DrawRoundedButton(
            context,
            Map(new Rect(40, 32, 14, 17), scale, offsetX, offsetY),
            2.5 * scale,
            ControllerButton.Up,
            PlateFill,
            ButtonText,
            null,
            0);
        DrawRoundedButton(
            context,
            Map(new Rect(40, 56, 14, 17), scale, offsetX, offsetY),
            2.5 * scale,
            ControllerButton.Down,
            PlateFill,
            ButtonText,
            null,
            0);
        DrawRoundedButton(
            context,
            Map(new Rect(26, 48, 16, 10), scale, offsetX, offsetY),
            2.5 * scale,
            ControllerButton.Left,
            PlateFill,
            ButtonText,
            null,
            0);
        DrawRoundedButton(
            context,
            Map(new Rect(52, 48, 16, 10), scale, offsetX, offsetY),
            2.5 * scale,
            ControllerButton.Right,
            PlateFill,
            ButtonText,
            null,
            0);

        // P (play/pause) and X (stop) between the D-pad and the face buttons.
        DrawRoundedButton(
            context,
            Map(new Rect(94, 63, 11, 11), scale, offsetX, offsetY),
            2.5 * scale,
            ControllerButton.X,
            ShoulderFill,
            ShoulderText,
            "X",
            6.5 * scale);

        DrawRoundedButton(
            context,
            Map(new Rect(114, 63, 11, 11), scale, offsetX, offsetY),
            2.5 * scale,
            ControllerButton.P,
            ShoulderFill,
            ShoulderText,
            "P",
            6.5 * scale);

        // A, B, C face buttons, laid out diagonally like the 3DO pad.
        DrawRoundButton(
            context,
            MapPoint(152, 62, scale, offsetX, offsetY),
            10.5 * scale,
            ControllerButton.A,
            ButtonFill,
            ButtonText,
            "A",
            9 * scale);

        DrawRoundButton(
            context,
            MapPoint(172, 48, scale, offsetX, offsetY),
            10.5 * scale,
            ControllerButton.B,
            ButtonFill,
            ButtonText,
            "B",
            9 * scale);

        DrawRoundButton(
            context,
            MapPoint(194, 42, scale, offsetX, offsetY),
            10.5 * scale,
            ControllerButton.C,
            ButtonFill,
            ButtonText,
            "C",
            9 * scale);
    }

    private void DrawRoundedButton(
        DrawingContext context,
        Rect rect,
        double radius,
        ControllerButton button,
        IBrush normalFill,
        IBrush normalText,
        string? label,
        double fontSize)
    {
        ControllerPreviewState state = GetState(button);
        IBrush fill = FillFor(state, normalFill);
        IPen? pen = PenFor(state);
        context.DrawRectangle(fill, pen, new RoundedRect(rect, radius));
        if (label is not null && fontSize > 0)
        {
            DrawLabel(context, label, rect, normalText, state, fontSize);
        }
    }

    private void DrawRoundButton(
        DrawingContext context,
        Point center,
        double radius,
        ControllerButton button,
        IBrush normalFill,
        IBrush normalText,
        string label,
        double fontSize)
    {
        ControllerPreviewState state = GetState(button);
        IBrush fill = FillFor(state, normalFill);
        IPen? pen = PenFor(state);
        context.DrawEllipse(fill, pen, center, radius, radius);
        var text = CreateText(label, fontSize, TextFor(state, normalText));
        context.DrawText(
            text,
            new Point(center.X - (text.Width / 2), center.Y - (text.Height / 2)));
    }

    private void DrawLabel(
        DrawingContext context,
        string label,
        Rect rect,
        IBrush normalText,
        ControllerPreviewState state,
        double fontSize)
    {
        var text = CreateText(label, fontSize, TextFor(state, normalText));
        context.DrawText(
            text,
            new Point(
                rect.Center.X - (text.Width / 2),
                rect.Center.Y - (text.Height / 2)));
    }

    private static FormattedText CreateText(string text, double fontSize, IBrush brush)
    {
        return new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            fontSize,
            brush);
    }

    private ControllerPreviewState GetState(ControllerButton button)
    {
        return ControllerPreview.GetState(button, MappedButtons, SelectedButton, PressedButtons);
    }

    private static IBrush FillFor(ControllerPreviewState state, IBrush normalFill)
    {
        return state switch
        {
            ControllerPreviewState.Pressed => PressedFill,
            ControllerPreviewState.Selected => SelectedFill,
            ControllerPreviewState.Mapped => MappedFill,
            _ => normalFill,
        };
    }

    private static IBrush TextFor(ControllerPreviewState state, IBrush normalText)
    {
        return state is ControllerPreviewState.Pressed or ControllerPreviewState.Selected
            ? DarkText
            : normalText;
    }

    private static IPen? PenFor(ControllerPreviewState state)
    {
        return state switch
        {
            ControllerPreviewState.Pressed => PressedPen,
            ControllerPreviewState.Selected => SelectedPen,
            ControllerPreviewState.Mapped => MappedPen,
            _ => null,
        };
    }

    private static Rect Map(Rect rect, double scale, double offsetX, double offsetY)
    {
        return new Rect(
            offsetX + (rect.X * scale),
            offsetY + (rect.Y * scale),
            rect.Width * scale,
            rect.Height * scale);
    }

    private static Point MapPoint(double x, double y, double scale, double offsetX, double offsetY)
    {
        return new Point(offsetX + (x * scale), offsetY + (y * scale));
    }
}
