using System;
using FiveDo.Input;

namespace FiveDo.Frontend.Avalonia.Input;


/// <summary>
/// A raw platform gamepad source. Implementations translate native devices into
/// <see cref="GamepadState"/> samples and are polled once per emulated frame.
/// </summary>
internal interface IRawGamepadSource : IDisposable
{
    /// <summary>Gets the number of connected controllers.</summary>
    int Count { get; }

    /// <summary>Polls the platform for new input.</summary>
    void Poll();

    /// <summary>Reads a controller sample by compact index.</summary>
    /// <param name="index">Zero-based connected-controller index.</param>
    /// <returns>The current sample, or <see cref="GamepadState.Empty"/>.</returns>
    GamepadState Read(int index);
}
