using System;
using System.Threading;
using FiveDo.Core;

namespace FiveDo.Input;


/// <summary>
/// A platform gamepad backend. Implementations poll the OS and map physical inputs to the
/// six controller slots; the emulation core never sees platform APIs.
/// </summary>
public interface IGamepadBackend
{
    /// <summary>Gets the number of connected controllers.</summary>
    int ControllerCount { get; }

    /// <summary>Polls the platform for the current state.</summary>
    void Poll();

    /// <summary>Gets a controller state.</summary>
    /// <param name="player">Zero-based player index.</param>
    /// <returns>The controller state.</returns>
    ControllerState GetState(int player);
}
