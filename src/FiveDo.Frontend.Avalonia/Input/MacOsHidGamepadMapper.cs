using System;
using System.Runtime.InteropServices;
using FiveDo.Input;

namespace FiveDo.Frontend.Avalonia.Input;

/// <summary>
/// A single IOKit HID element sample with the descriptor range needed to normalize it.
/// </summary>
/// <param name="UsagePage">HID usage page (Generic Desktop or Button).</param>
/// <param name="Usage">HID usage within the page.</param>
/// <param name="LogicalMinimum">Descriptor logical minimum.</param>
/// <param name="LogicalMaximum">Descriptor logical maximum.</param>
/// <param name="Value">Current integer value reported by IOKit.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MacOsHidElementSample(
    ushort UsagePage,
    ushort Usage,
    int LogicalMinimum,
    int LogicalMaximum,
    int Value);

/// <summary>
/// Translates raw IOKit HID gamepad elements into the vendor-neutral <see cref="GamepadState"/>
/// used by <see cref="PlatformGamepadBackend"/> and the existing eight-player profile.
/// </summary>
/// <remarks>
/// The mapper supports the standard HID gamepad layout used by Xbox-compatible controllers:
/// Generic Desktop X/Y for the left stick, Rx/Ry for the right stick, and Z/Rz for the
/// left/right triggers. When a device exposes only Z/Rz, those axes are treated as the right
/// stick instead. Buttons use Button-page usages 1-10, the hat switch uses usage 0x39, and
/// controllers that report a d-pad as buttons use usages 11-14. The mapping is deliberately
/// pure so it can be unit-tested on every host without IOKit hardware.
/// </remarks>
internal static class MacOsHidGamepadMapper
{
    internal const ushort GenericDesktopPage = 0x01;
    internal const ushort ButtonPage = 0x09;

    internal const ushort X = 0x30;
    internal const ushort Y = 0x31;
    internal const ushort Z = 0x32;
    internal const ushort Rx = 0x33;
    internal const ushort Ry = 0x34;
    internal const ushort Rz = 0x35;
    internal const ushort HatSwitch = 0x39;

    /// <summary>Maps a set of HID elements to a normalized gamepad state.</summary>
    /// <param name="elements">Elements from one device.</param>
    /// <returns>The mapped state. Missing elements stay centered/released.</returns>
    internal static GamepadState Map(ReadOnlySpan<MacOsHidElementSample> elements)
    {
        GamepadButtons buttons = GamepadButtons.None;
        var leftX = 0f;
        var leftY = 0f;
        var rightX = 0f;
        var rightY = 0f;
        var leftTrigger = 0f;
        var rightTrigger = 0f;

        var hasRx = HasUsage(elements, GenericDesktopPage, Rx);
        var hasRy = HasUsage(elements, GenericDesktopPage, Ry);
        var hasZ = HasUsage(elements, GenericDesktopPage, Z);
        var hasRz = HasUsage(elements, GenericDesktopPage, Rz);

        // Standard HID pads put the right stick on Rx/Ry and the triggers on Z/Rz.
        // If Rx/Ry are absent, a signed Z/Rz pair is the older two-axis right-stick
        // layout. Unsigned Z/Rz axes are analog triggers, which is the common
        // single-stick gamepad report.
        var zRzAreRightStick = !hasRx
            && !hasRy
            && hasZ
            && hasRz
            && IsSignedAxis(elements, Z)
            && IsSignedAxis(elements, Rz);

        foreach (MacOsHidElementSample element in elements)
        {
            if (element.UsagePage == ButtonPage)
            {
                if (element.Value != 0)
                {
                    buttons |= MapButton(element.Usage);
                }

                continue;
            }

            if (element.UsagePage != GenericDesktopPage)
            {
                continue;
            }

            switch (element.Usage)
            {
                case X:
                    leftX = NormalizeAxis(element, invert: false);
                    break;
                case Y:
                    leftY = NormalizeAxis(element, invert: true);
                    break;
                case Rx when hasRx || hasRy:
                    rightX = NormalizeAxis(element, invert: false);
                    break;
                case Ry when hasRx || hasRy:
                    rightY = NormalizeAxis(element, invert: true);
                    break;
                case Z when zRzAreRightStick:
                    rightX = NormalizeAxis(element, invert: false);
                    break;
                case Rz when zRzAreRightStick:
                    rightY = NormalizeAxis(element, invert: true);
                    break;
                case Z:
                    leftTrigger = NormalizeTrigger(element);
                    break;
                case Rz:
                    rightTrigger = NormalizeTrigger(element);
                    break;
                case HatSwitch:
                    buttons |= MapHat(element);
                    break;
            }
        }

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
            leftX,
            leftY,
            rightX,
            rightY,
            leftTrigger,
            rightTrigger);
    }

    /// <summary>Maps a Button-page usage to a neutral gamepad button.</summary>
    /// <param name="usage">Button usage (1-based).</param>
    /// <returns>The mapped button, or <see cref="GamepadButtons.None"/>.</returns>
    internal static GamepadButtons MapButton(ushort usage)
    {
        return usage switch
        {
            // Xbox-style HID report order: A, B, X, Y, LB, RB, Back, Start, LS, RS.
            1 => GamepadButtons.South,
            2 => GamepadButtons.East,
            3 => GamepadButtons.West,
            4 => GamepadButtons.North,
            5 => GamepadButtons.LeftShoulder,
            6 => GamepadButtons.RightShoulder,
            7 => GamepadButtons.Select,
            8 => GamepadButtons.Start,
            9 => GamepadButtons.LeftStick,
            10 => GamepadButtons.RightStick,
            11 => GamepadButtons.DpadUp,
            12 => GamepadButtons.DpadDown,
            13 => GamepadButtons.DpadLeft,
            14 => GamepadButtons.DpadRight,
            _ => GamepadButtons.None,
        };
    }

    private static bool HasUsage(ReadOnlySpan<MacOsHidElementSample> elements, ushort page, ushort usage)
    {
        foreach (MacOsHidElementSample element in elements)
        {
            if (element.UsagePage == page && element.Usage == usage)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSignedAxis(ReadOnlySpan<MacOsHidElementSample> elements, ushort usage)
    {
        foreach (MacOsHidElementSample element in elements)
        {
            if (element.UsagePage == GenericDesktopPage && element.Usage == usage)
            {
                return element.LogicalMinimum < 0;
            }
        }

        return false;
    }

    private static float NormalizeAxis(MacOsHidElementSample element, bool invert)
    {
        if (element.LogicalMaximum <= element.LogicalMinimum)
        {
            return 0f;
        }

        var value = (2f * (element.Value - element.LogicalMinimum)
            / (element.LogicalMaximum - element.LogicalMinimum)) - 1f;
        value = Math.Clamp(value, -1f, 1f);
        return invert ? -value : value;
    }

    private static float NormalizeTrigger(MacOsHidElementSample element)
    {
        if (element.LogicalMaximum <= element.LogicalMinimum)
        {
            return 0f;
        }

        return Math.Clamp(
            (element.Value - element.LogicalMinimum)
            / (float)(element.LogicalMaximum - element.LogicalMinimum),
            0f,
            1f);
    }

    private static GamepadButtons MapHat(MacOsHidElementSample element)
    {
        if (element.LogicalMaximum < element.LogicalMinimum)
        {
            return GamepadButtons.None;
        }

        var steps = element.LogicalMaximum - element.LogicalMinimum + 1;
        var value = element.Value - element.LogicalMinimum;
        if (value < 0 || value >= steps)
        {
            return GamepadButtons.None;
        }

        // A 4-direction hat uses 0..3; an 8-direction hat uses 0..7. Ignore the
        // conventional 8/15 "centered" sentinel, which falls outside both ranges.
        var direction = steps <= 4 ? value * 2 : value;
        return direction switch
        {
            0 => GamepadButtons.DpadUp,
            1 => GamepadButtons.DpadUp | GamepadButtons.DpadRight,
            2 => GamepadButtons.DpadRight,
            3 => GamepadButtons.DpadDown | GamepadButtons.DpadRight,
            4 => GamepadButtons.DpadDown,
            5 => GamepadButtons.DpadDown | GamepadButtons.DpadLeft,
            6 => GamepadButtons.DpadLeft,
            7 => GamepadButtons.DpadUp | GamepadButtons.DpadLeft,
            _ => GamepadButtons.None,
        };
    }
}
