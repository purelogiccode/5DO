using System;
using FiveDo.Core;

namespace FiveDo.Input;

/// <summary>
/// Encodes controller states into the 16-byte PBUS packet read by MADAM at V1.
/// </summary>
/// <remarks>
/// 4DO: <c>JohnnyInputPlugin.GetPbusData</c> plus the low/high button-byte helpers. The packet
/// carries up to six controllers, but in the wired order 1, 3, 2, 5, 4, 6. Slots beyond the
/// connected controller count are zero; the high byte of an active controller always has
/// bit 7 set (power/connectivity).
/// </remarks>
public static class PbusEncoder
{
    /// <summary>Required destination size in bytes.</summary>
    public const int PacketSize = 16;

    /// <summary>Maximum number of controllers in a packet.</summary>
    public const int MaxControllers = 6;

    /// <summary>Encodes controller states into a PBUS packet.</summary>
    /// <param name="controllers">Controller states, player 1 first.</param>
    /// <param name="destination">Destination span of at least 16 bytes.</param>
    /// <exception cref="ArgumentException">Thrown when the destination is too small.</exception>
    public static void Encode(ReadOnlySpan<ControllerState> controllers, Span<byte> destination)
    {
        if (destination.Length < PacketSize)
        {
            throw new ArgumentException($"The PBUS packet needs {PacketSize} bytes.", nameof(destination));
        }

        destination[..PacketSize].Clear();
        destination[0] = 0x00;
        destination[1] = 0x48;
        destination[12] = 0x00;
        destination[13] = 0x80;

        var count = Math.Min(controllers.Length, MaxControllers);
        for (var slot = 0; slot < count; slot++)
        {
            var state = controllers[slot];
            var offset = slot switch
            {
                0 => 2,
                1 => 4,
                2 => 6,
                3 => 8,
                4 => 10,
                _ => 14,
            };

            destination[offset] = LowByte(state);
            destination[offset + 1] = HighByte(state);
        }
    }

    private static byte LowByte(ControllerState state)
    {
        var value = 0;
        value |= state.IsDown(ControllerButton.L) ? 0x04 : 0;
        value |= state.IsDown(ControllerButton.R) ? 0x08 : 0;
        value |= state.IsDown(ControllerButton.X) ? 0x10 : 0;
        value |= state.IsDown(ControllerButton.P) ? 0x20 : 0;
        value |= state.IsDown(ControllerButton.C) ? 0x40 : 0;
        value |= state.IsDown(ControllerButton.B) ? 0x80 : 0;
        return (byte)value;
    }

    private static byte HighByte(ControllerState state)
    {
        var value = 0;
        value |= state.IsDown(ControllerButton.A) ? 0x01 : 0;
        value |= state.IsDown(ControllerButton.Left) ? 0x02 : 0;
        value |= state.IsDown(ControllerButton.Right) ? 0x04 : 0;
        value |= state.IsDown(ControllerButton.Up) ? 0x08 : 0;
        value |= state.IsDown(ControllerButton.Down) ? 0x10 : 0;
        value |= 0x80;
        return (byte)value;
    }
}

/// <summary>
/// A mutable PBUS source suitable for a host frontend. The machine calls
/// <see cref="GetPbusData"/> once per frame from the emulation thread.
/// </summary>
public sealed class PbusSource : IPbusSource
{
    private readonly ControllerState[] _controllers = new ControllerState[PbusEncoder.MaxControllers];

    /// <summary>Gets the number of connected controllers.</summary>
    public int ControllerCount { get; private set; } = 1;

    /// <summary>Gets a controller state by zero-based player index.</summary>
    /// <param name="player">Player index, 0-5.</param>
    /// <returns>The controller state.</returns>
    public ControllerState this[int player]
    {
        get => _controllers[player];
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(player);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(player, PbusEncoder.MaxControllers);
            _controllers[player] = value;
            ControllerCount = Math.Max(ControllerCount, player + 1);
        }
    }

    /// <summary>Sets the connected controller count.</summary>
    /// <param name="count">Count, 1-6.</param>
    public void SetControllerCount(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, PbusEncoder.MaxControllers);
        ControllerCount = count;
    }

    /// <summary>Replaces all controller states.</summary>
    /// <param name="controllers">Controller states, player 1 first.</param>
    public void SetControllers(ReadOnlySpan<ControllerState> controllers)
    {
        _controllers.AsSpan().Clear();
        controllers[..Math.Min(controllers.Length, _controllers.Length)].CopyTo(_controllers);
        ControllerCount = Math.Clamp(controllers.Length, 1, PbusEncoder.MaxControllers);
    }

    /// <inheritdoc />
    public int GetPbusData(Span<byte> destination)
    {
        PbusEncoder.Encode(_controllers.AsSpan(0, ControllerCount), destination);
        return PbusEncoder.PacketSize;
    }
}
