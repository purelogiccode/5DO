using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FiveDo.Input;

namespace FiveDo.Frontend.Avalonia.Input;

/// <summary>
/// Native macOS gamepad source built on IOKit's HID Manager.
/// </summary>
/// <remarks>
/// <para>
/// The source matches IOKit HID devices that advertise the Generic Desktop Joystick,
/// Gamepad, or Multi-axis Controller usage. It polls each device's input elements with
/// <c>IOHIDDeviceGetValue</c> once per emulated frame, so no run-loop callback or extra
/// thread is required. Devices are rescanned every two seconds for hot-plugging.
/// </para>
/// <para>
/// macOS may require Input Monitoring permission for the process to read HID devices.
/// When IOKit cannot open the manager the frontend falls back to the null source, exactly
/// like a missing XInput DLL on Windows.
/// </para>
/// </remarks>
internal sealed class MacOsGamepadSource : IRawGamepadSource
{
    private const int MaxControllers = PbusEncoder.MaxControllers;
    private const int RescanIntervalMilliseconds = 2000;
    private const uint NoOptions = 0;
    private const int Success = 0;

    private const string CoreFoundation =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    private const string Iokit =
        "/System/Library/Frameworks/IOKit.framework/IOKit";

    private static readonly IntPtr DeviceUsagePageKey = CreateString("DeviceUsagePage");
    private static readonly IntPtr DeviceUsageKey = CreateString("DeviceUsage");
    private static readonly IntPtr LocationIdKey = CreateString("LocationID");

    private readonly IntPtr _manager;
    private readonly List<Device> _devices = [];
    private readonly GamepadState[] _states = new GamepadState[MaxControllers];
    private long _nextRescan;
    private bool _disposed;

    private MacOsGamepadSource(IntPtr manager)
    {
        _manager = manager;
    }

    /// <summary>Creates the IOKit HID source when the platform and permissions allow it.</summary>
    internal static MacOsGamepadSource? TryCreate()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        IntPtr manager = IntPtr.Zero;
        try
        {
            manager = IOHIDManagerCreate(IntPtr.Zero, NoOptions);
            if (manager == IntPtr.Zero)
            {
                return null;
            }

            SetMatchingDevices(manager);
            if (IOHIDManagerOpen(manager, NoOptions) != Success)
            {
                CFRelease(manager);
                return null;
            }

            var source = new MacOsGamepadSource(manager);
            source.Rescan();
            return source;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            Serilog.Log.Debug(exception, "IOKit HID manager is unavailable.");
            if (manager != IntPtr.Zero)
            {
                CFRelease(manager);
            }

            return null;
        }
    }

    /// <inheritdoc />
    public int Count => _disposed ? 0 : _devices.Count;

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

        for (var i = 0; i < _devices.Count; i++)
        {
            _states[i] = _devices[i].Poll();
        }
    }

    /// <inheritdoc />
    public GamepadState Read(int index)
    {
        return _disposed || index < 0 || index >= _devices.Count
            ? GamepadState.Empty
            : _states[index];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ClearDevices();
        if (_manager != IntPtr.Zero)
        {
            _ = IOHIDManagerClose(_manager, NoOptions);
            CFRelease(_manager);
        }
    }

    private void Rescan()
    {
        IntPtr set = IOHIDManagerCopyDevices(_manager);
        if (set == IntPtr.Zero)
        {
            ClearDevices();
            return;
        }

        try
        {
            var count = (int)CFSetGetCount(set);
            if (count <= 0)
            {
                ClearDevices();
                return;
            }

            var deviceRefs = new IntPtr[count];
            CFSetGetValues(set, deviceRefs);
            Array.Sort(deviceRefs, CompareLocation);

            var devices = new List<Device>(Math.Min(count, MaxControllers));
            foreach (IntPtr deviceRef in deviceRefs)
            {
                if (devices.Count >= MaxControllers)
                {
                    break;
                }

                Device? device = Device.TryCreate(deviceRef);
                if (device is not null)
                {
                    devices.Add(device);
                }
            }

            ClearDevices();
            _devices.AddRange(devices);
        }
        finally
        {
            CFRelease(set);
        }
    }

    private void ClearDevices()
    {
        foreach (Device device in _devices)
        {
            device.Dispose();
        }

        _devices.Clear();
        Array.Clear(_states);
    }

    private static int CompareLocation(IntPtr left, IntPtr right)
    {
        return ReadLocationId(left).CompareTo(ReadLocationId(right));
    }

    private static long ReadLocationId(IntPtr device)
    {
        IntPtr number = IOHIDDeviceGetProperty(device, LocationIdKey);
        return number != IntPtr.Zero && CFNumberGetValue(number, 4, out var value)
            ? value
            : 0;
    }

    private static void SetMatchingDevices(IntPtr manager)
    {
        // CoreFoundation's type callbacks retain the dictionary keys/values and the
        // array's dictionaries. Using them lets us release our temporary strings and
        // numbers as soon as the matching array is attached to the manager.
        IntPtr coreFoundation = NativeLibrary.Load(CoreFoundation);
        IntPtr dictionaryKeyCallbacks = NativeLibrary.GetExport(
            coreFoundation,
            "kCFTypeDictionaryKeyCallBacks");
        IntPtr dictionaryValueCallbacks = NativeLibrary.GetExport(
            coreFoundation,
            "kCFTypeDictionaryValueCallBacks");
        IntPtr arrayCallbacks = NativeLibrary.GetExport(
            coreFoundation,
            "kCFTypeArrayCallBacks");

        IntPtr[] dictionaries =
        [
            CreateMatchingDictionary(usage: 4, dictionaryKeyCallbacks, dictionaryValueCallbacks),
            CreateMatchingDictionary(usage: 5, dictionaryKeyCallbacks, dictionaryValueCallbacks),
            CreateMatchingDictionary(usage: 8, dictionaryKeyCallbacks, dictionaryValueCallbacks),
        ];

        IntPtr array = IntPtr.Zero;
        try
        {
            array = CFArrayCreate(IntPtr.Zero, dictionaries, dictionaries.Length, arrayCallbacks);
            if (array != IntPtr.Zero)
            {
                IOHIDManagerSetDeviceMatchingMultiple(manager, array);
            }
        }
        finally
        {
            if (array != IntPtr.Zero)
            {
                CFRelease(array);
            }

            foreach (IntPtr dictionary in dictionaries)
            {
                if (dictionary != IntPtr.Zero)
                {
                    CFRelease(dictionary);
                }
            }
        }
    }

    private static IntPtr CreateMatchingDictionary(
        int usage,
        IntPtr keyCallbacks,
        IntPtr valueCallbacks)
    {
        IntPtr page = CreateNumber(GenericDesktopUsagePage);
        IntPtr usageNumber = CreateNumber(usage);
        try
        {
            if (page == IntPtr.Zero || usageNumber == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            return CFDictionaryCreate(
                IntPtr.Zero,
                [DeviceUsagePageKey, DeviceUsageKey],
                [page, usageNumber],
                2,
                keyCallbacks,
                valueCallbacks);
        }
        finally
        {
            if (page != IntPtr.Zero)
            {
                CFRelease(page);
            }

            if (usageNumber != IntPtr.Zero)
            {
                CFRelease(usageNumber);
            }
        }
    }

    private const int GenericDesktopUsagePage = 0x01;

    private static IntPtr CreateString(string value)
    {
        return CFStringCreateWithCString(IntPtr.Zero, value, 0x08000100);
    }

    private static IntPtr CreateNumber(int value)
    {
        return CFNumberCreate(IntPtr.Zero, 3, ref value);
    }

    private sealed class Device : IDisposable
    {
        private readonly IntPtr _device;
        private readonly IntPtr _elements;
        private readonly IntPtr[] _elementRefs;
        private readonly MacOsHidElementSample[] _samples;

        private Device(
            IntPtr device,
            IntPtr elements,
            IntPtr[] elementRefs,
            MacOsHidElementSample[] samples)
        {
            _device = device;
            _elements = elements;
            _elementRefs = elementRefs;
            _samples = samples;
        }

        internal static Device? TryCreate(IntPtr deviceRef)
        {
            IntPtr elements = IOHIDDeviceCopyMatchingElements(deviceRef, IntPtr.Zero, NoOptions);
            if (elements == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var count = (int)CFArrayGetCount(elements);
                var elementRefs = new List<IntPtr>(Math.Max(0, count));
                var samples = new List<MacOsHidElementSample>(Math.Max(0, count));
                for (var i = 0; i < count; i++)
                {
                    IntPtr element = CFArrayGetValueAtIndex(elements, i);
                    if (element == IntPtr.Zero)
                    {
                        continue;
                    }

                    // IOKit values: 1 = input/misc, 2 = input/button, 3 = input/axis,
                    // 4 = input/scan-codes. Output, feature, and collection elements
                    // are not readable gamepad samples.
                    var elementType = IOHIDElementGetType(element);
                    if (elementType is < 1 or > 4)
                    {
                        continue;
                    }

                    var page = (ushort)IOHIDElementGetUsagePage(element);
                    if (page is not MacOsHidGamepadMapper.GenericDesktopPage
                        and not MacOsHidGamepadMapper.ButtonPage)
                    {
                        continue;
                    }

                    var usage = (ushort)IOHIDElementGetUsage(element);
                    if (usage == 0)
                    {
                        continue;
                    }

                    var minimum = (int)IOHIDElementGetLogicalMin(element);
                    var maximum = (int)IOHIDElementGetLogicalMax(element);
                    elementRefs.Add(element);
                    samples.Add(new MacOsHidElementSample(page, usage, minimum, maximum, 0));
                }

                if (elementRefs.Count == 0)
                {
                    CFRelease(elements);
                    return null;
                }

                _ = CFRetain(deviceRef);
                return new Device(deviceRef, elements, [.. elementRefs], [.. samples]);
            }
            catch
            {
                CFRelease(elements);
                throw;
            }
        }

        internal GamepadState Poll()
        {
            for (var i = 0; i < _elementRefs.Length; i++)
            {
                if (IOHIDDeviceGetValue(_device, _elementRefs[i], out IntPtr value) != Success
                    || value == IntPtr.Zero)
                {
                    continue;
                }

                try
                {
                    _samples[i] = _samples[i] with
                    {
                        Value = (int)IOHIDValueGetIntegerValue(value),
                    };
                }
                finally
                {
                    CFRelease(value);
                }
            }

            return MacOsHidGamepadMapper.Map(_samples);
        }

        public void Dispose()
        {
            CFRelease(_elements);
            CFRelease(_device);
        }
    }

    [DllImport(CoreFoundation, EntryPoint = "CFStringCreateWithCString")]
    private static extern IntPtr CFStringCreateWithCString(
        IntPtr allocator,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value,
        uint encoding);

    [DllImport(CoreFoundation, EntryPoint = "CFNumberCreate")]
    private static extern IntPtr CFNumberCreate(IntPtr allocator, int type, ref int value);

    [DllImport(CoreFoundation, EntryPoint = "CFDictionaryCreate")]
    private static extern IntPtr CFDictionaryCreate(
        IntPtr allocator,
        IntPtr[] keys,
        IntPtr[] values,
        nint count,
        IntPtr keyCallbacks,
        IntPtr valueCallbacks);

    [DllImport(CoreFoundation, EntryPoint = "CFArrayCreate")]
    private static extern IntPtr CFArrayCreate(
        IntPtr allocator,
        IntPtr[] values,
        nint count,
        IntPtr callbacks);

    [DllImport(CoreFoundation, EntryPoint = "CFArrayGetCount")]
    private static extern nint CFArrayGetCount(IntPtr array);

    [DllImport(CoreFoundation, EntryPoint = "CFArrayGetValueAtIndex")]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

    [DllImport(CoreFoundation, EntryPoint = "CFSetGetCount")]
    private static extern nint CFSetGetCount(IntPtr set);

    [DllImport(CoreFoundation, EntryPoint = "CFSetGetValues")]
    private static extern void CFSetGetValues(IntPtr set, IntPtr[] values);

    [DllImport(CoreFoundation, EntryPoint = "CFNumberGetValue")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool CFNumberGetValue(IntPtr number, int type, out long value);

    [DllImport(CoreFoundation, EntryPoint = "CFRetain")]
    private static extern IntPtr CFRetain(IntPtr value);

    [DllImport(CoreFoundation, EntryPoint = "CFRelease")]
    private static extern void CFRelease(IntPtr value);

    [DllImport(Iokit, EntryPoint = "IOHIDManagerCreate")]
    private static extern IntPtr IOHIDManagerCreate(IntPtr allocator, uint options);

    [DllImport(Iokit, EntryPoint = "IOHIDManagerSetDeviceMatchingMultiple")]
    private static extern void IOHIDManagerSetDeviceMatchingMultiple(IntPtr manager, IntPtr multiple);

    [DllImport(Iokit, EntryPoint = "IOHIDManagerOpen")]
    private static extern int IOHIDManagerOpen(IntPtr manager, uint options);

    [DllImport(Iokit, EntryPoint = "IOHIDManagerClose")]
    private static extern int IOHIDManagerClose(IntPtr manager, uint options);

    [DllImport(Iokit, EntryPoint = "IOHIDManagerCopyDevices")]
    private static extern IntPtr IOHIDManagerCopyDevices(IntPtr manager);

    [DllImport(Iokit, EntryPoint = "IOHIDDeviceCopyMatchingElements")]
    private static extern IntPtr IOHIDDeviceCopyMatchingElements(
        IntPtr device,
        IntPtr matching,
        uint options);

    [DllImport(Iokit, EntryPoint = "IOHIDDeviceGetValue")]
    private static extern int IOHIDDeviceGetValue(
        IntPtr device,
        IntPtr element,
        out IntPtr value);

    [DllImport(Iokit, EntryPoint = "IOHIDDeviceGetProperty")]
    private static extern IntPtr IOHIDDeviceGetProperty(IntPtr device, IntPtr key);

    [DllImport(Iokit, EntryPoint = "IOHIDElementGetType")]
    private static extern int IOHIDElementGetType(IntPtr element);

    [DllImport(Iokit, EntryPoint = "IOHIDElementGetUsagePage")]
    private static extern uint IOHIDElementGetUsagePage(IntPtr element);

    [DllImport(Iokit, EntryPoint = "IOHIDElementGetUsage")]
    private static extern uint IOHIDElementGetUsage(IntPtr element);

    [DllImport(Iokit, EntryPoint = "IOHIDElementGetLogicalMin")]
    private static extern nint IOHIDElementGetLogicalMin(IntPtr element);

    [DllImport(Iokit, EntryPoint = "IOHIDElementGetLogicalMax")]
    private static extern nint IOHIDElementGetLogicalMax(IntPtr element);

    [DllImport(Iokit, EntryPoint = "IOHIDValueGetIntegerValue")]
    private static extern nint IOHIDValueGetIntegerValue(IntPtr value);
}
