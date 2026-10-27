using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FiveDo.Input;

namespace FiveDo.Frontend.Avalonia.Input;

/// <summary>
/// Linux evdev gamepad source. Scans <c>/dev/input/event*</c> for devices that expose
/// gamepad buttons and absolute axes, reads their non-blocking event streams, and rescans
/// periodically for hot-plugging.
/// </summary>
/// <remarks>
/// The implementation deliberately uses the kernel's stable evdev ABI rather than adding
/// SDL or a windowing dependency. It works on X11, Wayland, and headless sessions as long
/// as the user has permission to read the event device (normally the <c>input</c> group).
/// </remarks>
internal sealed class EvdevGamepadSource : IRawGamepadSource
{
    private const int OpenReadOnly = 0;
    private const int OpenNonBlock = 0x800;
    private const int EventKey = 1;
    private const int EventAbs = 3;
    private const int KeyMax = 0x2ff;
    private const int AbsMax = 0x3f;
    private const int AxisMax = 0x40;
    private const int EventBufferEvents = 64;
    private const int RescanIntervalMilliseconds = 2000;

    private static readonly int EventSize = Marshal.SizeOf<InputEvent>();

    private readonly List<Device> _devices = [];
    private readonly byte[] _keyBits = new byte[(KeyMax / 8) + 1];
    private readonly byte[] _absBits = new byte[(AbsMax / 8) + 1];
    private readonly byte[] _eventBuffer = new byte[EventSize * EventBufferEvents];
    private long _nextRescan;
    private bool _disposed;

    private EvdevGamepadSource()
    {
    }

    /// <summary>Creates the source when the platform exposes readable event devices.</summary>
    internal static EvdevGamepadSource? TryCreate()
    {
        if (!Directory.Exists("/dev/input"))
        {
            return null;
        }

        var source = new EvdevGamepadSource();
        source.Rescan();
        return source;
    }

    /// <inheritdoc />
    public int Count => _devices.Count;

    /// <inheritdoc />
    public void Poll()
    {
        if (_disposed)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now >= _nextRescan)
        {
            _nextRescan = now + RescanIntervalMilliseconds;
            Rescan();
        }

        for (var i = _devices.Count - 1; i >= 0; i--)
        {
            if (!ReadEvents(_devices[i]))
            {
                _devices[i].Dispose();
                _devices.RemoveAt(i);
            }
        }
    }

    /// <inheritdoc />
    public GamepadState Read(int index)
    {
        return index >= 0 && index < _devices.Count ? _devices[index].State : GamepadState.Empty;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        foreach (Device device in _devices)
        {
            device.Dispose();
        }

        _devices.Clear();
    }

    private void Rescan()
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (Device device in _devices)
        {
            known.Add(device.Path);
        }

        string[] paths;
        try
        {
            paths = Directory.GetFiles("/dev/input", "event*");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        Array.Sort(paths, StringComparer.Ordinal);
        foreach (var path in paths)
        {
            if (known.Contains(path) || _devices.Count >= PbusEncoder.MaxControllers)
            {
                continue;
            }

            Device? device = TryOpen(path);
            if (device is not null)
            {
                _devices.Add(device);
            }
        }

        for (var i = _devices.Count - 1; i >= 0; i--)
        {
            if (!File.Exists(_devices[i].Path))
            {
                _devices[i].Dispose();
                _devices.RemoveAt(i);
            }
        }
    }

    private Device? TryOpen(string path)
    {
        var fd = OpenDevice(path, OpenReadOnly | OpenNonBlock);
        if (fd < 0)
        {
            return null;
        }

        try
        {
            Array.Clear(_keyBits);
            Array.Clear(_absBits);
            if (IoctlBuffer(fd, EventIoctl(0x20 + EventKey, _keyBits.Length), _keyBits) < 0
                || IoctlBuffer(fd, EventIoctl(0x20 + EventAbs, _absBits.Length), _absBits) < 0)
            {
                _ = CloseDevice(fd);
                return null;
            }

            var hasGamepadButton = HasAnyBit(_keyBits, 0x130, 0x13e) || HasBit(_keyBits, 0x120) || HasBit(_keyBits, 0x121);
            if (!hasGamepadButton || !HasBit(_absBits, 0x00) || !HasBit(_absBits, 0x01))
            {
                _ = CloseDevice(fd);
                return null;
            }

            var device = new Device(path, fd);
            for (var axis = 0; axis < AxisMax; axis++)
            {
                if (!HasBit(_absBits, axis))
                {
                    continue;
                }

                if (IoctlAbs(fd, AbsIoctl(axis), ref device.Absolute[axis]) == 0)
                {
                    device.HasAxis[axis] = true;
                }
                else
                {
                    device.Absolute[axis] = new InputAbsInfo { Minimum = -32768, Maximum = 32767 };
                }
            }

            device.Refresh();
            return device;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            _ = CloseDevice(fd);
            return null;
        }
    }

    private bool ReadEvents(Device device)
    {
        while (true)
        {
            var count = ReadDevice(device.FileDescriptor, _eventBuffer, (nuint)_eventBuffer.Length);
            if (count <= 0)
            {
                if (count == 0)
                {
                    return true;
                }

                var error = Marshal.GetLastPInvokeError();
                return error is 11 or 4;
            }

            var length = (int)count;
            for (var offset = 0; offset + EventSize <= length; offset += EventSize)
            {
                InputEvent inputEvent = MemoryMarshal.Read<InputEvent>(_eventBuffer.AsSpan(offset, EventSize));
                if (inputEvent.Type == EventKey)
                {
                    device.SetButton(inputEvent.Code, inputEvent.Value != 0);
                }
                else if (inputEvent.Type == EventAbs)
                {
                    device.SetAxis(inputEvent.Code, inputEvent.Value);
                }
            }

            device.Refresh();
            if (length < _eventBuffer.Length)
            {
                return true;
            }
        }
    }

    private static nuint EventIoctl(int number, int length)
    {
        return (2u << 30) | ((uint)length << 16) | ((uint)number << 8) | 'E';
    }

    private static nuint AbsIoctl(int axis)
    {
        return EventIoctl(0x40 + axis, Marshal.SizeOf<InputAbsInfo>());
    }

    private static bool HasBit(byte[] bits, int bit)
    {
        var index = bit >> 3;
        return index < bits.Length && (bits[index] & (1 << (bit & 7))) != 0;
    }

    private static bool HasAnyBit(byte[] bits, int first, int last)
    {
        for (var bit = first; bit <= last; bit++)
        {
            if (HasBit(bits, bit))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class Device : IDisposable
    {
        internal Device(string path, int fileDescriptor)
        {
            Path = path;
            FileDescriptor = fileDescriptor;
        }

        internal string Path { get; }

        internal int FileDescriptor { get; }

        internal InputAbsInfo[] Absolute { get; } = new InputAbsInfo[AxisMax];

        internal bool[] HasAxis { get; } = new bool[AxisMax];

        private GamepadButtons Buttons { get; set; }

        internal GamepadState State { get; private set; }

        internal void SetButton(int code, bool pressed)
        {
            GamepadButtons button = code switch
            {
                0x120 or 0x130 => GamepadButtons.South,
                0x121 or 0x131 => GamepadButtons.East,
                0x132 or 0x133 => GamepadButtons.North,
                0x122 or 0x134 => GamepadButtons.West,
                0x136 => GamepadButtons.LeftShoulder,
                0x137 => GamepadButtons.RightShoulder,
                0x138 => GamepadButtons.LeftTrigger,
                0x139 => GamepadButtons.RightTrigger,
                0x13a => GamepadButtons.Select,
                0x13b => GamepadButtons.Start,
                0x13d => GamepadButtons.LeftStick,
                0x13e => GamepadButtons.RightStick,
                _ => GamepadButtons.None,
            };

            if (button == GamepadButtons.None)
            {
                return;
            }

            Buttons = pressed ? Buttons | button : Buttons & ~button;
        }

        internal void SetAxis(int code, int value)
        {
            if (code >= 0 && code < AxisMax)
            {
                Absolute[code].Value = value;
            }
        }

        internal void Refresh()
        {
            GamepadButtons buttons = Buttons;
            var hatX = AxisValue(0x10);
            var hatY = AxisValue(0x11);
            if (hatX < 0)
            {
                buttons |= GamepadButtons.DpadLeft;
            }
            else if (hatX > 0)
            {
                buttons |= GamepadButtons.DpadRight;
            }

            if (hatY < 0)
            {
                buttons |= GamepadButtons.DpadUp;
            }
            else if (hatY > 0)
            {
                buttons |= GamepadButtons.DpadDown;
            }

            var leftTrigger = TriggerValue(0x02);
            var rightTrigger = TriggerValue(0x05);
            if (leftTrigger >= GamepadMapper.DefaultTriggerThreshold)
            {
                buttons |= GamepadButtons.LeftTrigger;
            }

            if (rightTrigger >= GamepadMapper.DefaultTriggerThreshold)
            {
                buttons |= GamepadButtons.RightTrigger;
            }

            State = new GamepadState(
                buttons,
                Normalize(0x00),
                Normalize(0x01),
                Normalize(0x03),
                Normalize(0x04),
                leftTrigger,
                rightTrigger);
        }

        private float Normalize(int axis)
        {
            InputAbsInfo info = Absolute[axis];
            if (!HasAxis[axis] || info.Maximum <= info.Minimum)
            {
                return 0f;
            }

            var value = (2f * (info.Value - info.Minimum) / (info.Maximum - info.Minimum)) - 1f;
            value = Math.Clamp(value, -1f, 1f);

            // evdev Y axes grow downward; GamepadState uses positive-up.
            return axis is 0x01 or 0x04 ? -value : value;
        }

        private float TriggerValue(int axis)
        {
            InputAbsInfo info = Absolute[axis];
            if (!HasAxis[axis] || info.Maximum <= info.Minimum)
            {
                return 0f;
            }

            return Math.Clamp((info.Value - info.Minimum) / (float)(info.Maximum - info.Minimum), 0f, 1f);
        }

        private int AxisValue(int axis)
        {
            return HasAxis[axis] ? Absolute[axis].Value : 0;
        }

        public void Dispose()
        {
            _ = CloseDevice(FileDescriptor);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InputEvent
    {
        internal nint Seconds;
        internal nint Microseconds;
        internal ushort Type;
        internal ushort Code;
        internal int Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InputAbsInfo
    {
        internal int Value;
        internal int Minimum;
        internal int Maximum;
        internal int Fuzz;
        internal int Flat;
        internal int Resolution;
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenDevice([MarshalAs(UnmanagedType.LPUTF8Str)] string pathname, int flags);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int CloseDevice(int fileDescriptor);

    [DllImport("libc", EntryPoint = "read", SetLastError = true)]
    private static extern nint ReadDevice(int fileDescriptor, byte[] buffer, nuint count);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int IoctlBuffer(int fileDescriptor, nuint request, byte[] argument);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int IoctlAbs(int fileDescriptor, nuint request, ref InputAbsInfo argument);
}
