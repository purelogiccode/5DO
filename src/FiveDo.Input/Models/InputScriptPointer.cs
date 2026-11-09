namespace FiveDo.Input;

/// <summary>Identifies the pointer device described by an input-script entry.</summary>
public enum InputScriptPointerKind
{
    /// <summary>No pointer device; every pointer button is released.</summary>
    None,

    /// <summary>A 3DO mouse.</summary>
    Mouse,

    /// <summary>A 3DO lightgun.</summary>
    Lightgun,

    /// <summary>An arcade lightgun.</summary>
    ArcadeLightgun,

    /// <summary>An Orbatak trackball with its cabinet buttons.</summary>
    Trackball,
}

/// <summary>
/// Optional pointer-device state attached to an <see cref="InputScriptEntry"/>.
/// </summary>
/// <remarks>
/// Only the field selected by <see cref="Kind"/> is meaningful. Coordinates are
/// relative deltas for <see cref="InputScriptPointerKind.Mouse"/> and
/// <see cref="InputScriptPointerKind.Trackball"/>, and 0-319/0-239 screen pixels
/// for the lightguns.
/// </remarks>
/// <param name="Kind">The active pointer device.</param>
/// <param name="Mouse">Mouse state when <paramref name="Kind"/> is mouse.</param>
/// <param name="Lightgun">Lightgun state when <paramref name="Kind"/> is lightgun.</param>
/// <param name="ArcadeLightgun">Arcade lightgun state when <paramref name="Kind"/> is arcade lightgun.</param>
/// <param name="Trackball">Trackball motion when <paramref name="Kind"/> is trackball.</param>
/// <param name="TrackballButtons">Trackball cabinet buttons when <paramref name="Kind"/> is trackball.</param>
public readonly record struct InputScriptPointer(
    InputScriptPointerKind Kind,
    MouseState Mouse,
    LightgunState Lightgun,
    ArcadeLightgunState ArcadeLightgun,
    OrbatakTrackballState Trackball,
    OrbatakButtonsState TrackballButtons)
{
    /// <summary>Gets a pointer state that releases every pointer button.</summary>
    public static InputScriptPointer Empty => default;

    /// <summary>Creates a mouse pointer state.</summary>
    /// <param name="state">Mouse state.</param>
    /// <returns>A pointer state with <see cref="InputScriptPointerKind.Mouse"/>.</returns>
    public static InputScriptPointer FromMouse(MouseState state)
    {
        return new InputScriptPointer(
            InputScriptPointerKind.Mouse,
            state,
            default,
            default,
            default,
            default);
    }

    /// <summary>Creates a lightgun pointer state.</summary>
    /// <param name="state">Lightgun state.</param>
    /// <returns>A pointer state with <see cref="InputScriptPointerKind.Lightgun"/>.</returns>
    public static InputScriptPointer FromLightgun(LightgunState state)
    {
        return new InputScriptPointer(
            InputScriptPointerKind.Lightgun,
            default,
            state,
            default,
            default,
            default);
    }

    /// <summary>Creates an arcade lightgun pointer state.</summary>
    /// <param name="state">Arcade lightgun state.</param>
    /// <returns>A pointer state with <see cref="InputScriptPointerKind.ArcadeLightgun"/>.</returns>
    public static InputScriptPointer FromArcadeLightgun(ArcadeLightgunState state)
    {
        return new InputScriptPointer(
            InputScriptPointerKind.ArcadeLightgun,
            default,
            default,
            state,
            default,
            default);
    }

    /// <summary>Creates an Orbatak trackball pointer state.</summary>
    /// <param name="motion">Trackball motion.</param>
    /// <param name="buttons">Trackball cabinet buttons.</param>
    /// <returns>A pointer state with <see cref="InputScriptPointerKind.Trackball"/>.</returns>
    public static InputScriptPointer FromTrackball(
        OrbatakTrackballState motion,
        OrbatakButtonsState buttons)
    {
        return new InputScriptPointer(
            InputScriptPointerKind.Trackball,
            default,
            default,
            default,
            motion,
            buttons);
    }
}
