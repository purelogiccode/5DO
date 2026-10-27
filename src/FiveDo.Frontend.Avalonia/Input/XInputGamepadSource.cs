using System;
using System.Runtime.InteropServices;
using FiveDo.Input;

namespace FiveDo.Frontend.Avalonia.Input;

/// <summary>
/// Windows XInput gamepad source. Supports up to four controllers and hot-plugging.
/// </summary>
/// <remarks>
/// XInput is loaded dynamically so the emulator still starts when the DLL is absent
/// (for example on a stripped-down Windows install). The frontend falls back to the
/// null source in that case.
/// </remarks>
internal sealed class XInputGamepadSource : IRawGamepadSource
{
    private const uint ErrorSuccess = 0;
    private const int MaxControllers = 4;

    private readonly XInputGetStateDelegate _getState;
    private IntPtr _library;
    private readonly XInputState[] _nativeStates = new XInputState[MaxControllers];
    private readonly GamepadState[] _states = new GamepadState[MaxControllers];
    private readonly int[] _connected = new int[MaxControllers];
    private int _count;

    private XInputGamepadSource(IntPtr library, XInputGetStateDelegate getState)
    {
        _library = library;
        _getState = getState;
    }

    /// <summary>Loads XInput, trying the common DLL names in newest-first order.</summary>
    internal static XInputGamepadSource? TryCreate()
    {
        string[] candidates = ["xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll"];
        foreach (var candidate in candidates)
        {
            if (!NativeLibrary.TryLoad(candidate, out var library))
            {
                continue;
            }

            if (!NativeLibrary.TryGetExport(library, "XInputGetState", out var export))
            {
                NativeLibrary.Free(library);
                continue;
            }

            XInputGetStateDelegate getState = Marshal.GetDelegateForFunctionPointer<XInputGetStateDelegate>(export);
            return new XInputGamepadSource(library, getState);
        }

        return null;
    }

    /// <inheritdoc />
    public int Count => _count;

    /// <inheritdoc />
    public void Poll()
    {
        _count = 0;
        for (var slot = 0; slot < MaxControllers; slot++)
        {
            if (_getState((uint)slot, out _nativeStates[slot]) != ErrorSuccess)
            {
                _states[slot] = GamepadState.Empty;
                continue;
            }

            _states[slot] = Translate(_nativeStates[slot].Gamepad);
            _connected[_count++] = slot;
        }
    }

    /// <inheritdoc />
    public GamepadState Read(int index)
    {
        if (index < 0 || index >= _count)
        {
            return GamepadState.Empty;
        }

        return _states[_connected[index]];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_library != IntPtr.Zero)
        {
            NativeLibrary.Free(_library);
            _library = IntPtr.Zero;
        }
    }

    private static GamepadState Translate(XInputGamepad gamepad)
    {
        GamepadButtons buttons = GamepadButtons.None;
        if ((gamepad.Buttons & 0x1000) != 0)
        {
            buttons |= GamepadButtons.South;
        }

        if ((gamepad.Buttons & 0x2000) != 0)
        {
            buttons |= GamepadButtons.East;
        }

        if ((gamepad.Buttons & 0x4000) != 0)
        {
            buttons |= GamepadButtons.West;
        }

        if ((gamepad.Buttons & 0x8000) != 0)
        {
            buttons |= GamepadButtons.North;
        }

        if ((gamepad.Buttons & 0x0100) != 0)
        {
            buttons |= GamepadButtons.LeftShoulder;
        }

        if ((gamepad.Buttons & 0x0200) != 0)
        {
            buttons |= GamepadButtons.RightShoulder;
        }

        if ((gamepad.Buttons & 0x0001) != 0)
        {
            buttons |= GamepadButtons.DpadUp;
        }

        if ((gamepad.Buttons & 0x0002) != 0)
        {
            buttons |= GamepadButtons.DpadDown;
        }

        if ((gamepad.Buttons & 0x0004) != 0)
        {
            buttons |= GamepadButtons.DpadLeft;
        }

        if ((gamepad.Buttons & 0x0008) != 0)
        {
            buttons |= GamepadButtons.DpadRight;
        }

        if ((gamepad.Buttons & 0x0010) != 0)
        {
            buttons |= GamepadButtons.Start;
        }

        if ((gamepad.Buttons & 0x0020) != 0)
        {
            buttons |= GamepadButtons.Select;
        }

        if ((gamepad.Buttons & 0x0040) != 0)
        {
            buttons |= GamepadButtons.LeftStick;
        }

        if ((gamepad.Buttons & 0x0080) != 0)
        {
            buttons |= GamepadButtons.RightStick;
        }

        var leftTrigger = gamepad.LeftTrigger / 255f;
        var rightTrigger = gamepad.RightTrigger / 255f;
        if (leftTrigger >= GamepadMapper.DefaultTriggerThreshold)
        {
            buttons |= GamepadButtons.LeftTrigger;
        }

        if (rightTrigger >= GamepadMapper.DefaultTriggerThreshold)
        {
            buttons |= GamepadButtons.RightTrigger;
        }

        return new GamepadState(
            buttons,
            Normalize(gamepad.LeftThumbX),
            Normalize(gamepad.LeftThumbY),
            Normalize(gamepad.RightThumbX),
            Normalize(gamepad.RightThumbY),
            leftTrigger,
            rightTrigger);
    }

    private static float Normalize(short value)
    {
        return value == short.MinValue ? -1f : Math.Clamp(value / 32767f, -1f, 1f);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint XInputGetStateDelegate(uint index, out XInputState state);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        internal uint PacketNumber;
        internal XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        internal ushort Buttons;
        internal byte LeftTrigger;
        internal byte RightTrigger;
        internal short LeftThumbX;
        internal short LeftThumbY;
        internal short RightThumbX;
        internal short RightThumbY;
    }
}
