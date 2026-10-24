
namespace ThreeDo.Input;

/// <summary>
/// The state of one 3DO gamepad for a single frame.
/// </summary>
/// <param name="Buttons">Pressed buttons.</param>
public readonly record struct ControllerState(ControllerButton Buttons)
{
    /// <summary>Gets an empty controller state.</summary>
    public static ControllerState Empty => default;

    /// <summary>Gets a value indicating whether any button is pressed.</summary>
    public bool IsPressed => Buttons != ControllerButton.None;

    /// <summary>Returns a state with the given buttons added.</summary>
    /// <param name="buttons">Buttons to press.</param>
    /// <returns>The updated state.</returns>
    public ControllerState With(ControllerButton buttons) => new(Buttons | buttons);

    /// <summary>Returns a state with the given buttons released.</summary>
    /// <param name="buttons">Buttons to release.</param>
    /// <returns>The updated state.</returns>
    public ControllerState Without(ControllerButton buttons) => new(Buttons & ~buttons);

    /// <summary>Tests whether a button is pressed.</summary>
    /// <param name="button">Button to test.</param>
    /// <returns><see langword="true"/> when pressed.</returns>
    public bool IsDown(ControllerButton button) => (Buttons & button) != 0;

    /// <summary>Applies a pressed/released transition.</summary>
    /// <param name="button">Button to change.</param>
    /// <param name="pressed">Whether the button is pressed.</param>
    /// <returns>The updated state.</returns>
    public ControllerState Set(ControllerButton button, bool pressed)
    {
        return pressed ? With(button) : Without(button);
    }
}
