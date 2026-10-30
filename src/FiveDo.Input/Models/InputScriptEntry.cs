using System.Runtime.InteropServices;

namespace FiveDo.Input;


/// <summary>One state change in a scripted controller sequence.</summary>
/// <param name="Frame">First frame on which the state applies.</param>
/// <param name="State">Controller state from that frame onward.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct InputScriptEntry(int Frame, ControllerState State);
