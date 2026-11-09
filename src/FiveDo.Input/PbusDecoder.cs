using System;
using System.Text;

namespace FiveDo.Input;

/// <summary>
/// Decodes the 16-byte PBUS packet written by <see cref="PbusEncoder"/> back into
/// per-player controller states. This is used by TAS input display and diagnostics.
/// </summary>
public static class PbusDecoder
{
    private static readonly ControllerButton[] ButtonOrder =
    [
        ControllerButton.A,
        ControllerButton.B,
        ControllerButton.C,
        ControllerButton.P,
        ControllerButton.X,
        ControllerButton.L,
        ControllerButton.R,
        ControllerButton.Up,
        ControllerButton.Down,
        ControllerButton.Left,
        ControllerButton.Right,
    ];

    /// <summary>Decodes every controller slot in a PBUS packet.</summary>
    /// <param name="packet">Packet bytes: a 16-byte legacy packet or an Opera device chain.</param>
    /// <returns>Six controller states; slots outside the packet are empty.</returns>
    public static ControllerState[] Decode(ReadOnlySpan<byte> packet)
    {
        var states = new ControllerState[PbusEncoder.MaxPorts];
        Decode(packet, states);
        return states;
    }

    /// <summary>Decodes every controller slot into an existing destination.</summary>
    /// <param name="packet">Packet bytes, normally 16.</param>
    /// <param name="destination">Destination span of at least eight entries.</param>
    /// <exception cref="ArgumentException">Thrown when the destination is too small.</exception>
    public static void Decode(ReadOnlySpan<byte> packet, Span<ControllerState> destination)
    {
        if (destination.Length < PbusEncoder.MaxPorts)
        {
            throw new ArgumentException(
                $"The decoder needs {PbusEncoder.MaxPorts} controller slots.",
                nameof(destination));
        }

        if (packet.Length >= 2 && packet[0] == 0x00 && packet[1] == 0x48)
        {
            for (var slot = 0; slot < PbusEncoder.MaxPorts; slot++)
            {
                destination[slot] = DecodeController(packet, slot);
            }

            return;
        }

        DecodeChain(packet, destination);
    }

    private static void DecodeChain(ReadOnlySpan<byte> packet, Span<ControllerState> destination)
    {
        destination[..PbusEncoder.MaxPorts].Clear();
        var slot = 0;
        var offset = 0;
        while (offset < packet.Length && slot < PbusEncoder.MaxPorts)
        {
            byte id = packet[offset];
            if (id == 0xFF)
            {
                break;
            }

            if (id == 0x01 && offset + 2 < packet.Length && packet[offset + 1] == 0x7B && packet[offset + 2] == 0x08)
            {
                offset += 9;
                continue;
            }

            if (id == 0x49)
            {
                offset += 4;
                continue;
            }

            if (id == 0x4D)
            {
                offset += 5;
                continue;
            }

            if (id == 0xC0)
            {
                offset += 3;
                continue;
            }

            if ((id & 0xC0) != 0 && offset + 1 < packet.Length)
            {
                ControllerButton buttons = ControllerButton.None;
                buttons |= (id & 0x01) != 0 ? ControllerButton.A : ControllerButton.None;
                buttons |= (id & 0x02) != 0 ? ControllerButton.Left : ControllerButton.None;
                buttons |= (id & 0x04) != 0 ? ControllerButton.Right : ControllerButton.None;
                buttons |= (id & 0x08) != 0 ? ControllerButton.Up : ControllerButton.None;
                buttons |= (id & 0x10) != 0 ? ControllerButton.Down : ControllerButton.None;
                byte high = packet[offset + 1];
                buttons |= (high & 0x80) != 0 ? ControllerButton.B : ControllerButton.None;
                buttons |= (high & 0x40) != 0 ? ControllerButton.C : ControllerButton.None;
                buttons |= (high & 0x20) != 0 ? ControllerButton.P : ControllerButton.None;
                buttons |= (high & 0x10) != 0 ? ControllerButton.X : ControllerButton.None;
                buttons |= (high & 0x08) != 0 ? ControllerButton.R : ControllerButton.None;
                buttons |= (high & 0x04) != 0 ? ControllerButton.L : ControllerButton.None;
                destination[slot++] = new ControllerState(buttons);
                offset += 2;
                continue;
            }

            break;
        }
    }

    /// <summary>Decodes one controller slot.</summary>
    /// <param name="packet">Packet bytes.</param>
    /// <param name="slot">Zero-based controller slot, 0-7.</param>
    /// <returns>The controller state, or an empty state when the slot is absent.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the slot is outside 0-7.</exception>
    public static ControllerState DecodeController(ReadOnlySpan<byte> packet, int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, PbusEncoder.MaxPorts);
        if (packet.Length < 2 || packet[0] != 0x00 || packet[1] != 0x48)
        {
            return DecodeChainController(packet, slot);
        }

        if (slot >= PbusEncoder.MaxControllers)
        {
            // The legacy 16-byte packet has no slots 7-8.
            return ControllerState.Empty;
        }

        var offset = OffsetForSlot(slot);
        if (packet.Length < offset + 2)
        {
            return ControllerState.Empty;
        }

        var low = packet[offset];
        var high = packet[offset + 1];
        ControllerButton buttons = ControllerButton.None;
        buttons |= (low & 0x04) != 0 ? ControllerButton.L : ControllerButton.None;
        buttons |= (low & 0x08) != 0 ? ControllerButton.R : ControllerButton.None;
        buttons |= (low & 0x10) != 0 ? ControllerButton.X : ControllerButton.None;
        buttons |= (low & 0x20) != 0 ? ControllerButton.P : ControllerButton.None;
        buttons |= (low & 0x40) != 0 ? ControllerButton.C : ControllerButton.None;
        buttons |= (low & 0x80) != 0 ? ControllerButton.B : ControllerButton.None;
        buttons |= (high & 0x01) != 0 ? ControllerButton.A : ControllerButton.None;
        buttons |= (high & 0x02) != 0 ? ControllerButton.Left : ControllerButton.None;
        buttons |= (high & 0x04) != 0 ? ControllerButton.Right : ControllerButton.None;
        buttons |= (high & 0x08) != 0 ? ControllerButton.Up : ControllerButton.None;
        buttons |= (high & 0x10) != 0 ? ControllerButton.Down : ControllerButton.None;
        return new ControllerState(buttons);
    }

    private static ControllerState DecodeChainController(ReadOnlySpan<byte> packet, int slot)
    {
        var current = 0;
        var offset = 0;
        while (offset < packet.Length)
        {
            byte id = packet[offset];
            if (id == 0xFF)
            {
                break;
            }

            if (id == 0x01 && offset + 2 < packet.Length && packet[offset + 1] == 0x7B && packet[offset + 2] == 0x08)
            {
                offset += 9;
                continue;
            }

            if (id == 0x49)
            {
                offset += 4;
                continue;
            }

            if (id == 0x4D)
            {
                offset += 5;
                continue;
            }

            if (id == 0xC0)
            {
                offset += 3;
                continue;
            }

            if ((id & 0xC0) == 0 || offset + 1 >= packet.Length)
            {
                break;
            }

            if (current == slot)
            {
                ControllerButton buttons = ControllerButton.None;
                buttons |= (id & 0x01) != 0 ? ControllerButton.A : ControllerButton.None;
                buttons |= (id & 0x02) != 0 ? ControllerButton.Left : ControllerButton.None;
                buttons |= (id & 0x04) != 0 ? ControllerButton.Right : ControllerButton.None;
                buttons |= (id & 0x08) != 0 ? ControllerButton.Up : ControllerButton.None;
                buttons |= (id & 0x10) != 0 ? ControllerButton.Down : ControllerButton.None;
                byte high = packet[offset + 1];
                buttons |= (high & 0x80) != 0 ? ControllerButton.B : ControllerButton.None;
                buttons |= (high & 0x40) != 0 ? ControllerButton.C : ControllerButton.None;
                buttons |= (high & 0x20) != 0 ? ControllerButton.P : ControllerButton.None;
                buttons |= (high & 0x10) != 0 ? ControllerButton.X : ControllerButton.None;
                buttons |= (high & 0x08) != 0 ? ControllerButton.R : ControllerButton.None;
                buttons |= (high & 0x04) != 0 ? ControllerButton.L : ControllerButton.None;
                return new ControllerState(buttons);
            }

            current++;
            offset += 2;
        }

        return ControllerState.Empty;
    }

    /// <summary>Tests whether a slot reports the connected/power bit.</summary>
    /// <param name="packet">Packet bytes.</param>
    /// <param name="slot">Zero-based controller slot, 0-5.</param>
    /// <returns><see langword="true"/> when the slot's high byte has bit 7 set.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the slot is outside 0-5.</exception>
    public static bool IsConnected(ReadOnlySpan<byte> packet, int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, PbusEncoder.MaxPorts);
        if (packet.Length >= 2 && packet[0] == 0x00 && packet[1] == 0x48
            && slot >= PbusEncoder.MaxControllers)
        {
            // The legacy 16-byte packet has no slots 7-8.
            return false;
        }

        var offset = OffsetForSlot(slot);
        return packet.Length >= offset + 2 && (packet[offset + 1] & 0x80) != 0;
    }

    /// <summary>Formats a controller state using the names accepted by <see cref="InputScript"/>.</summary>
    /// <param name="state">Controller state.</param>
    /// <returns>A comma-separated button list, or <c>None</c>.</returns>
    public static string FormatButtons(ControllerState state)
    {
        if (state.Buttons == ControllerButton.None)
        {
            return "None";
        }

        var text = new StringBuilder();
        foreach (ControllerButton button in ButtonOrder)
        {
            if ((state.Buttons & button) == ControllerButton.None)
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.Append(',');
            }

            text.Append(button.ToString());
        }

        return text.ToString();
    }

    private static int OffsetForSlot(int slot)
    {
        return slot switch
        {
            0 => 2,
            1 => 4,
            2 => 6,
            3 => 8,
            4 => 10,
            _ => 14,
        };
    }
}
