using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using FiveDo.Core;

namespace FiveDo.Input;


/// <summary>One state change in a scripted controller sequence.</summary>
/// <param name="Frame">First frame on which the state applies.</param>
/// <param name="State">Controller state from that frame onward.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct InputScriptEntry(int Frame, ControllerState State);
