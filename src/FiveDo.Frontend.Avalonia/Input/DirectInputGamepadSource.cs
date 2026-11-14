using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FiveDo.Input;

namespace FiveDo.Frontend.Avalonia.Input;

/// <summary>
/// Translates a DirectInput joystick state into the vendor-neutral gamepad sample.
/// </summary>
/// <remarks>
/// The translation is separated from the COM enumeration so it can be tested without a
/// physical device. Axes use the DirectInput logical range of -10000 to 10000; POV values
/// are hundredths of a degree with <c>-1</c> (0xFFFFFFFF) meaning centered.
/// </remarks>
internal static class DirectInputStateTranslator
{
    internal const int AxisCount = 8;
    internal const int PovCount = 4;
    internal const int ButtonCount = 128;

    /// <summary>Translates one raw DirectInput sample.</summary>
    /// <param name="axes">X, Y, Z, Rx, Ry, Rz, slider 0, and slider 1.</param>
    /// <param name="pov">Four POV hat values.</param>
    /// <param name="buttons">128 DirectInput button bytes.</param>
    /// <param name="leftTrigger">Calibrated left trigger value in the range 0-1.</param>
    /// <param name="rightTrigger">Calibrated right trigger value in the range 0-1.</param>
    /// <returns>The translated sample.</returns>
    public static GamepadState Translate(
        ReadOnlySpan<int> axes,
        ReadOnlySpan<uint> pov,
        ReadOnlySpan<byte> buttons,
        float leftTrigger,
        float rightTrigger)
    {
        GamepadButtons result = GamepadButtons.None;
        if (IsPressed(buttons, 0))
        {
            result |= GamepadButtons.South;
        }

        if (IsPressed(buttons, 1))
        {
            result |= GamepadButtons.East;
        }

        if (IsPressed(buttons, 2))
        {
            result |= GamepadButtons.West;
        }

        if (IsPressed(buttons, 3))
        {
            result |= GamepadButtons.North;
        }

        if (IsPressed(buttons, 4))
        {
            result |= GamepadButtons.LeftShoulder;
        }

        if (IsPressed(buttons, 5))
        {
            result |= GamepadButtons.RightShoulder;
        }

        if (IsPressed(buttons, 6))
        {
            result |= GamepadButtons.Select;
        }

        if (IsPressed(buttons, 7))
        {
            result |= GamepadButtons.Start;
        }

        if (IsPressed(buttons, 8))
        {
            result |= GamepadButtons.LeftStick;
        }

        if (IsPressed(buttons, 9))
        {
            result |= GamepadButtons.RightStick;
        }

        leftTrigger = Math.Clamp(leftTrigger, 0f, 1f);
        rightTrigger = Math.Clamp(rightTrigger, 0f, 1f);
        if (leftTrigger >= GamepadMapper.DefaultTriggerThreshold)
        {
            result |= GamepadButtons.LeftTrigger;
        }

        if (rightTrigger >= GamepadMapper.DefaultTriggerThreshold)
        {
            result |= GamepadButtons.RightTrigger;
        }

        if (pov.Length > 0)
        {
            ApplyPov(pov[0], ref result);
        }

        return new GamepadState(
            result,
            NormalizeAxis(GetAxis(axes, 0)),
            NormalizeAxis(GetAxis(axes, 1)),
            NormalizeAxis(GetAxis(axes, 3)),
            NormalizeAxis(GetAxis(axes, 4)),
            leftTrigger,
            rightTrigger);
    }

    /// <summary>Normalizes a DirectInput axis value to -1 through +1.</summary>
    /// <param name="value">Raw axis value.</param>
    /// <returns>The normalized axis.</returns>
    internal static float NormalizeAxis(int value)
    {
        return Math.Clamp(value / 10000f, -1f, 1f);
    }

    /// <summary>Maps a POV hat angle to the four D-pad directions.</summary>
    /// <param name="value">POV value in hundredths of a degree, or <c>0xFFFFFFFF</c>.</param>
    /// <param name="buttons">Buttons to update.</param>
    internal static void ApplyPov(uint value, ref GamepadButtons buttons)
    {
        if (value is 0xFFFFFFFFu or > 35999u)
        {
            return;
        }

        var angle = (int)value;
        if (angle <= 4500 || angle >= 31500)
        {
            buttons |= GamepadButtons.DpadUp;
        }

        if (angle is >= 4500 and <= 13500)
        {
            buttons |= GamepadButtons.DpadRight;
        }

        if (angle is >= 13500 and <= 22500)
        {
            buttons |= GamepadButtons.DpadDown;
        }

        if (angle is >= 22500 and <= 31500)
        {
            buttons |= GamepadButtons.DpadLeft;
        }
    }

    private static bool IsPressed(ReadOnlySpan<byte> buttons, int index)
    {
        return index < buttons.Length && buttons[index] != 0;
    }

    private static int GetAxis(ReadOnlySpan<int> axes, int index)
    {
        return index < axes.Length ? axes[index] : 0;
    }
}

/// <summary>
/// A Windows gamepad source that combines legacy DirectInput/HID devices with XInput.
/// </summary>
/// <remarks>
/// DirectInput 8 enumerates legacy HID pads (including axes, POV hats, and triggers) that
/// the XInput API cannot see. XInput slots are appended as the fallback for Xbox-compatible
/// controllers. The source re-enumerates every ten seconds so hot-plugging works; devices
/// missing from the new list are released.
/// </remarks>
internal sealed class DirectInputGamepadSource : IRawGamepadSource
{
    private const uint DirectInputVersion = 0x0800;
    private const uint Di8DevClassGameControl = 0x0004;
    private const uint DiEDflAttachedOnly = 0x0001;
    private const uint DisclNonExclusive = 0x00000002;
    private const uint DisclBackground = 0x00000008;
    private const int DeviceStateBytes = 272;
    private const int ButtonOffset = 48;
    private const int PovOffset = 32;
    private const int AxisOffset = 0;
    private const int ReenumerationMilliseconds = 10_000;
    private const int DiInputLost = unchecked((int)0x8007001E);
    private const int DiNotAcquired = unchecked((int)0x8007000C);

    private static readonly Guid DirectInput8W = new("BF798031-483A-4DA2-AA99-5D64ED369700");

    private readonly IntPtr _library;
    private readonly IntPtr _directInput;
    private readonly IntPtr _dataFormat;
    private readonly IntPtr _window;
    private readonly Dictionary<Guid, Device> _devices = [];
    private readonly List<GamepadState> _samples = [];
    private readonly long _reenumerationTicks = Stopwatch.Frequency * ReenumerationMilliseconds / 1000;
    private long _lastEnumeration;
    private int _count;

    private DirectInputGamepadSource(IntPtr library, IntPtr directInput, IntPtr dataFormat)
    {
        _library = library;
        _directInput = directInput;
        _dataFormat = dataFormat;
        _window = NativeMethods.GetDesktopWindow();
        Enumerate();
    }

    /// <inheritdoc />
    public int Count => _count;

    /// <summary>Loads DirectInput 8 and creates the source.</summary>
    /// <returns>The source, or <see langword="null"/> when DirectInput is unavailable.</returns>
    internal static DirectInputGamepadSource? TryCreate()
    {
        if (!OperatingSystem.IsWindows()
            || !NativeLibrary.TryLoad("dinput8.dll", out var library))
        {
            return null;
        }

        IntPtr directInput = IntPtr.Zero;
        try
        {
            if (!NativeLibrary.TryGetExport(library, "DirectInput8Create", out var createAddress)
                || !NativeLibrary.TryGetExport(library, "c_dfDIJoystick2", out var dataFormat))
            {
                NativeLibrary.Free(library);
                return null;
            }

            var create = Marshal.GetDelegateForFunctionPointer<NativeMethods.CreateDelegate>(createAddress);
            IntPtr module = NativeMethods.GetModuleHandleW(null);
            Guid interfaceId = DirectInput8W;
            int result = create(
                module,
                DirectInputVersion,
                ref interfaceId,
                out directInput,
                IntPtr.Zero);
            if (result < 0 || directInput == IntPtr.Zero)
            {
                NativeLibrary.Free(library);
                return null;
            }

            return new DirectInputGamepadSource(library, directInput, dataFormat);
        }
        catch (Exception exception) when (exception is DllNotFoundException
            or EntryPointNotFoundException
            or InvalidOperationException
            or ArgumentException)
        {
            Serilog.Log.Debug(exception, "DirectInput gamepad initialization failed.");
            if (directInput != IntPtr.Zero)
            {
                Release(directInput);
            }

            NativeLibrary.Free(library);
            return null;
        }
    }

    /// <inheritdoc />
    public void Poll()
    {
        _samples.Clear();
        var now = Stopwatch.GetTimestamp();
        if (now - _lastEnumeration >= _reenumerationTicks)
        {
            Enumerate();
        }

        var stale = new List<Guid>();
        foreach ((Guid id, Device device) in _devices)
        {
            if (!PollDevice(device))
            {
                stale.Add(id);
                continue;
            }

            _samples.Add(ReadDevice(device));
        }

        foreach (Guid id in stale)
        {
            _devices[id].Dispose();
            _devices.Remove(id);
        }

        _count = _samples.Count;
    }

    /// <inheritdoc />
    public GamepadState Read(int index)
    {
        return index >= 0 && index < _samples.Count ? _samples[index] : GamepadState.Empty;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (Device device in _devices.Values)
        {
            device.Dispose();
        }

        _devices.Clear();
        _samples.Clear();
        _count = 0;
        Release(_directInput);
        if (_library != IntPtr.Zero)
        {
            NativeLibrary.Free(_library);
        }
    }

    private void Enumerate()
    {
        _lastEnumeration = Stopwatch.GetTimestamp();
        var found = new List<Guid>();
        NativeMethods.EnumCallbackDelegate callback = (instance, _) =>
        {
            DidDeviceInstance device = Marshal.PtrToStructure<DidDeviceInstance>(instance);
            if (!IsXInputDevice(device.ProductName) && !IsXInputDevice(device.InstanceName))
            {
                found.Add(device.Instance);
            }

            return 1;
        };

        int result = GetDelegate<NativeMethods.EnumDevicesDelegate>(_directInput, 4)(
            _directInput,
            Di8DevClassGameControl,
            Marshal.GetFunctionPointerForDelegate(callback),
            IntPtr.Zero,
            DiEDflAttachedOnly);

        // The native enumerator calls through a function pointer that the GC cannot
        // see. Keep the delegate rooted until EnumDevices returns.
        GC.KeepAlive(callback);
        if (result < 0)
        {
            return;
        }

        foreach (Guid id in found)
        {
            if (!_devices.ContainsKey(id)
                && TryCreateDevice(id, out Device? device)
                && device is not null)
            {
                _devices.Add(id, device);
            }
        }

        var removed = new List<Guid>();
        foreach (Guid id in _devices.Keys)
        {
            if (!found.Contains(id))
            {
                removed.Add(id);
            }
        }

        foreach (Guid id in removed)
        {
            _devices[id].Dispose();
            _devices.Remove(id);
        }
    }

    private bool TryCreateDevice(Guid id, out Device? device)
    {
        device = null;
        IntPtr handle;
        int result = GetDelegate<NativeMethods.CreateDeviceDelegate>(_directInput, 3)(
            _directInput,
            ref id,
            out handle,
            IntPtr.Zero);
        if (result < 0 || handle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            result = GetDelegate<NativeMethods.SetDataFormatDelegate>(handle, 11)(handle, _dataFormat);
            if (result < 0)
            {
                Release(handle);
                return false;
            }

            result = GetDelegate<NativeMethods.SetCooperativeLevelDelegate>(handle, 13)(
                handle,
                _window,
                DisclBackground | DisclNonExclusive);
            if (result < 0)
            {
                Release(handle);
                return false;
            }

            _ = GetDelegate<NativeMethods.AcquireDelegate>(handle, 7)(handle);
            device = new Device(handle, Marshal.AllocHGlobal(DeviceStateBytes));
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            Serilog.Log.Debug(exception, "DirectInput device setup failed.");
            Release(handle);
            return false;
        }
    }

    private static bool PollDevice(Device device)
    {
        int result = device.Poll(device.Handle);
        if (result >= 0)
        {
            return true;
        }

        if (result is DiInputLost or DiNotAcquired)
        {
            _ = device.Acquire(device.Handle);
        }

        return result != DiInputLost;
    }

    private static GamepadState ReadDevice(Device device)
    {
        int result = device.GetState(device.Handle, DeviceStateBytes, device.State);
        if (result == DiNotAcquired)
        {
            _ = device.Acquire(device.Handle);
            return GamepadState.Empty;
        }

        if (result < 0)
        {
            return GamepadState.Empty;
        }

        Span<int> axes = stackalloc int[DirectInputStateTranslator.AxisCount];
        for (var i = 0; i < axes.Length; i++)
        {
            axes[i] = Marshal.ReadInt32(device.State, AxisOffset + (i * 4));
        }

        Span<uint> pov = stackalloc uint[DirectInputStateTranslator.PovCount];
        for (var i = 0; i < pov.Length; i++)
        {
            pov[i] = unchecked((uint)Marshal.ReadInt32(device.State, PovOffset + (i * 4)));
        }

        Span<byte> buttons = stackalloc byte[DirectInputStateTranslator.ButtonCount];
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i] = Marshal.ReadByte(device.State, ButtonOffset + i);
        }

        device.CalibrateTriggers(axes);
        (float left, float right) = device.ReadTriggers(axes);
        return DirectInputStateTranslator.Translate(axes, pov, buttons, left, right);
    }

    private static bool IsXInputDevice(string name)
    {
        return name.Contains("Xbox", StringComparison.OrdinalIgnoreCase)
            || name.Contains("XInput", StringComparison.OrdinalIgnoreCase)
            || name.Contains("XBOX", StringComparison.Ordinal);
    }

    private static void Release(IntPtr comObject)
    {
        if (comObject == IntPtr.Zero)
        {
            return;
        }

        _ = Marshal.GetDelegateForFunctionPointer<NativeMethods.ReleaseDelegate>(
            Marshal.ReadIntPtr(Marshal.ReadIntPtr(comObject), 2 * IntPtr.Size))(comObject);
    }

    private static T GetDelegate<T>(IntPtr comObject, int index)
        where T : Delegate
    {
        IntPtr vtable = Marshal.ReadIntPtr(comObject);
        IntPtr method = Marshal.ReadIntPtr(vtable, index * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<T>(method);
    }

    private sealed class Device : IDisposable
    {
        private bool _hasCalibration;
        private int _zRest;
        private int _rzRest;

        public Device(IntPtr handle, IntPtr state)
        {
            Handle = handle;
            State = state;
            Poll = GetDelegate<NativeMethods.PollDelegate>(handle, 25);
            Acquire = GetDelegate<NativeMethods.AcquireDelegate>(handle, 7);
            GetState = GetDelegate<NativeMethods.GetDeviceStateDelegate>(handle, 9);
        }

        public IntPtr Handle { get; }

        public IntPtr State { get; }

        public NativeMethods.PollDelegate Poll { get; }

        public NativeMethods.AcquireDelegate Acquire { get; }

        public NativeMethods.GetDeviceStateDelegate GetState { get; }

        public void CalibrateTriggers(ReadOnlySpan<int> axes)
        {
            if (_hasCalibration)
            {
                return;
            }

            _zRest = axes[2];
            _rzRest = axes[5];
            _hasCalibration = true;
        }

        public (float Left, float Right) ReadTriggers(ReadOnlySpan<int> axes)
        {
            var left = 0f;
            var right = 0f;
            int z = axes[2];
            if (_zRest < -2000)
            {
                left = Normalize(z, _zRest, 10000);
            }
            else if (_zRest > 2000)
            {
                left = Normalize(z, _zRest, -10000);
            }
            else
            {
                int delta = z - _zRest;
                if (delta < 0)
                {
                    left = Math.Clamp(-delta / 10000f, 0f, 1f);
                }
                else
                {
                    right = Math.Clamp(delta / 10000f, 0f, 1f);
                }
            }

            int rz = axes[5];
            if (_rzRest < -2000)
            {
                right = Math.Max(right, Normalize(rz, _rzRest, 10000));
            }
            else if (_rzRest > 2000)
            {
                right = Math.Max(right, Normalize(rz, _rzRest, -10000));
            }
            else if (rz != _rzRest)
            {
                right = Math.Max(right, Math.Clamp(Math.Abs(rz - _rzRest) / 10000f, 0f, 1f));
            }

            return (left, right);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                Release(Handle);
            }

            if (State != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(State);
            }
        }

        private static float Normalize(int value, int rest, int limit)
        {
            int span = Math.Abs(limit - rest);
            return span == 0 ? 0f : Math.Clamp((value - rest) / (float)span, 0f, 1f);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DidDeviceInstance
    {
        internal int Size;
        internal Guid Instance;
        internal Guid Product;
        internal int DeviceType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        internal string InstanceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        internal string ProductName;
        internal Guid ForceFeedbackDriver;
        internal ushort UsagePage;
        internal ushort Usage;
    }

    private static class NativeMethods
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int CreateDelegate(
            IntPtr instance,
            uint version,
            ref Guid interfaceId,
            out IntPtr directInput,
            IntPtr outer);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int EnumCallbackDelegate(IntPtr instance, IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int CreateDeviceDelegate(
            IntPtr self,
            ref Guid instance,
            out IntPtr device,
            IntPtr outer);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int EnumDevicesDelegate(
            IntPtr self,
            uint deviceType,
            IntPtr callback,
            IntPtr context,
            uint flags);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int SetDataFormatDelegate(IntPtr self, IntPtr dataFormat);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int SetCooperativeLevelDelegate(IntPtr self, IntPtr window, uint flags);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int AcquireDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int PollDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int GetDeviceStateDelegate(IntPtr self, int size, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate uint ReleaseDelegate(IntPtr self);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetDesktopWindow();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandleW(string? moduleName);
    }
}
