using System;

namespace FiveDo.Input;

/// <summary>
/// The controller packet layout written to the PBUS queue.
/// </summary>
public enum PbusFormat
{
    /// <summary>
    /// 4DO's fixed 16-byte layout: a header followed by six two-byte joypad slots in
    /// wired order 1, 3, 2, 5, 4, 6. This is the historical 5DO default.
    /// </summary>
    Legacy = 0,

    /// <summary>
    /// The 3DO PBUS daisy-chain layout used by Opera: a sequence of self-identifying
    /// device records padded with <c>0xFF</c> to a four-byte boundary.
    /// </summary>
    OperaChain = 1,
}

/// <summary>
/// Encodes controller and peripheral states into the 3DO PBUS daisy-chain packet.
/// </summary>
/// <remarks>
/// Opera: <c>libopera/opera_pbus.c</c>. Device records are written in port order and the
/// packet is padded with <c>0xFF</c> to a four-byte boundary. A joypad is two bytes:
/// <c>[A][Left][Right][Up][Down][ID2][ID1][ID0]</c> followed by
/// <c>[Tail1][Tail0][L][R][X][P][C][B]</c>. The ID nibble is always non-zero, which
/// reserves the <c>00XX</c> pattern for the other device signatures.
/// </remarks>
public static class PbusChainEncoder
{
    /// <summary>Maximum packet size written by the encoder.</summary>
    public const int MaxPacketSize = 64;

    /// <summary>Terminator value used for unused packet bytes.</summary>
    public const byte PadByte = 0xFF;

    /// <summary>Encodes a chain of standard joypads.</summary>
    /// <param name="controllers">Controller states, player 1 first.</param>
    /// <param name="destination">Destination span of at least <see cref="MaxPacketSize"/> bytes.</param>
    /// <returns>The encoded packet length, including padding.</returns>
    /// <exception cref="ArgumentException">Thrown when the destination is too small.</exception>
    public static int Encode(ReadOnlySpan<ControllerState> controllers, Span<byte> destination)
    {
        if (destination.Length < MaxPacketSize)
        {
            throw new ArgumentException(
                $"The PBUS chain packet needs at least {MaxPacketSize} bytes.",
                nameof(destination));
        }

        var length = 0;
        var count = Math.Min(controllers.Length, PbusEncoder.MaxControllers);
        for (var i = 0; i < count; i++)
        {
            WriteJoypad(controllers[i], destination, ref length);
        }

        return Pad(destination, length);
    }

    /// <summary>Encodes a mixed device chain.</summary>
    /// <param name="devices">Per-port device selection and state.</param>
    /// <param name="destination">Destination span of at least <see cref="MaxPacketSize"/> bytes.</param>
    /// <returns>The encoded packet length, including padding.</returns>
    /// <exception cref="ArgumentException">Thrown when the destination is too small.</exception>
    public static int Encode(InputDeviceSet devices, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(devices);
        if (destination.Length < MaxPacketSize)
        {
            throw new ArgumentException(
                $"The PBUS chain packet needs at least {MaxPacketSize} bytes.",
                nameof(destination));
        }

        return devices.Encode(destination);
    }

    /// <summary>Encodes a mixed device chain from per-port device types.</summary>
    /// <param name="devices">Device type for each port.</param>
    /// <param name="destination">Destination span of at least <see cref="MaxPacketSize"/> bytes.</param>
    /// <param name="joypad">Joypad state applied to every joypad port.</param>
    /// <param name="flightstick">Flightstick state applied to every flightstick port.</param>
    /// <param name="mouse">Mouse state applied to every mouse port.</param>
    /// <param name="lightgun">Lightgun state applied to every lightgun port.</param>
    /// <param name="arcadeLightgun">Arcade lightgun state applied to every arcade lightgun port.</param>
    /// <param name="trackball">Trackball state applied to every trackball port.</param>
    /// <param name="trackballButtons">Orbatak button state applied to every trackball port.</param>
    /// <returns>The encoded packet length, including padding.</returns>
    /// <remarks>
    /// This overload is intended for tests and tools that describe a synthetic device
    /// chain. A frontend should use <see cref="InputDeviceSet"/> directly so each port
    /// can carry independent state.
    /// </remarks>
    public static int Encode(
        ReadOnlySpan<InputDeviceType> devices,
        Span<byte> destination,
        ControllerState joypad = default,
        FlightstickState flightstick = default,
        MouseState mouse = default,
        LightgunState lightgun = default,
        ArcadeLightgunState arcadeLightgun = default,
        OrbatakTrackballState trackball = default,
        OrbatakButtonsState trackballButtons = default)
    {
        var set = new InputDeviceSet();
        for (var i = 0; i < devices.Length && i < PbusEncoder.MaxControllers; i++)
        {
            set.SetDevice(i, devices[i]);
            set.SetJoypad(i, joypad);
            set.SetFlightstick(i, flightstick);
            set.SetMouse(i, mouse);
            set.SetLightgun(i, lightgun);
            set.SetArcadeLightgun(i, arcadeLightgun);
            set.SetTrackball(i, trackball);
            set.SetTrackballButtons(i, trackballButtons);
        }

        return Encode(set, destination);
    }

    /// <summary>Writes one joypad record.</summary>
    /// <param name="state">Controller state.</param>
    /// <param name="destination">Destination span.</param>
    /// <param name="length">Current packet length.</param>
    public static void WriteJoypad(ControllerState state, Span<byte> destination, ref int length)
    {
        destination[length++] = (byte)(
            0x80
            | (state.IsDown(ControllerButton.Down) ? 0x10 : 0)
            | (state.IsDown(ControllerButton.Up) ? 0x08 : 0)
            | (state.IsDown(ControllerButton.Right) ? 0x04 : 0)
            | (state.IsDown(ControllerButton.Left) ? 0x02 : 0)
            | (state.IsDown(ControllerButton.A) ? 0x01 : 0));
        destination[length++] = (byte)(
            (state.IsDown(ControllerButton.B) ? 0x80 : 0)
            | (state.IsDown(ControllerButton.C) ? 0x40 : 0)
            | (state.IsDown(ControllerButton.P) ? 0x20 : 0)
            | (state.IsDown(ControllerButton.X) ? 0x10 : 0)
            | (state.IsDown(ControllerButton.R) ? 0x08 : 0)
            | (state.IsDown(ControllerButton.L) ? 0x04 : 0));
    }

    /// <summary>Writes one flightstick record (nine bytes).</summary>
    /// <param name="state">Flightstick state.</param>
    /// <param name="destination">Destination span.</param>
    /// <param name="length">Current packet length.</param>
    public static void WriteFlightstick(FlightstickState state, Span<byte> destination, ref int length)
    {
        var x = Position10(state.Horizontal);
        var y = Position10(state.Vertical);
        var z = Position10(state.Twist);

        destination[length++] = 0x01;
        destination[length++] = 0x7B;
        destination[length++] = 0x08;
        destination[length++] = (byte)(x >> 2);
        destination[length++] = (byte)(((x & 0x003) << 6) | ((y & 0x3F0) >> 4));
        destination[length++] = (byte)(((y & 0x00F) << 4) | ((z & 0x3C0) >> 6));
        destination[length++] = (byte)(((z & 0x03F) << 2) | 0x02);
        destination[length++] = (byte)(
            (state.Fire ? 0x80 : 0)
            | (state.IsDown(ControllerButton.A) ? 0x40 : 0)
            | (state.IsDown(ControllerButton.B) ? 0x20 : 0)
            | (state.IsDown(ControllerButton.C) ? 0x10 : 0)
            | (state.IsDown(ControllerButton.Up) ? 0x08 : 0)
            | (state.IsDown(ControllerButton.Down) ? 0x04 : 0)
            | (state.IsDown(ControllerButton.Right) ? 0x02 : 0)
            | (state.IsDown(ControllerButton.Left) ? 0x01 : 0));
        destination[length++] = (byte)(
            (state.IsDown(ControllerButton.P) ? 0x80 : 0)
            | (state.IsDown(ControllerButton.X) ? 0x40 : 0)
            | (state.IsDown(ControllerButton.L) ? 0x20 : 0)
            | (state.IsDown(ControllerButton.R) ? 0x10 : 0));
    }

    /// <summary>Writes one mouse record (four bytes).</summary>
    /// <param name="state">Mouse state with deltas in the 10-bit range.</param>
    /// <param name="destination">Destination span.</param>
    /// <param name="length">Current packet length.</param>
    public static void WriteMouse(MouseState state, Span<byte> destination, ref int length)
    {
        destination[length++] = 0x49;
        destination[length++] = (byte)(
            (state.Left ? 0x80 : 0)
            | (state.Middle ? 0x40 : 0)
            | (state.Right ? 0x20 : 0)
            | (state.Shift ? 0x10 : 0)
            | ((state.DeltaY & 0x3C0) >> 6));
        destination[length++] = (byte)(
            ((state.DeltaY & 0x03F) << 2)
            | ((state.DeltaX & 0x300) >> 8));
        destination[length++] = (byte)(state.DeltaX & 0xFF);
    }

    /// <summary>Writes one lightgun record (five bytes).</summary>
    /// <param name="state">Normalized lightgun state.</param>
    /// <param name="destination">Destination span.</param>
    /// <param name="length">Current packet length.</param>
    public static void WriteLightgun(LightgunState state, Span<byte> destination, ref int length)
    {
        uint counter;
        byte hitCount;
        var trigger = state.Trigger;
        if (state.Reload)
        {
            counter = 0;
            trigger = true;
            hitCount = 0;
        }
        else
        {
            counter = LightgunCounter(state.X, state.Y);
            hitCount = 0x1F;
        }

        destination[length++] = 0x4D;
        destination[length++] = (byte)(
            (trigger ? 0x80 : 0)
            | (state.Option ? 0x08 : 0)
            | (int)((counter & 0x80000) >> 19));
        destination[length++] = (byte)((counter & 0x7F800) >> 11);
        destination[length++] = (byte)((counter & 0x007F8) >> 3);
        destination[length++] = (byte)(((counter & 0x00007) << 5) | hitCount);
    }

    /// <summary>Writes one arcade lightgun record (five bytes).</summary>
    /// <param name="state">Normalized arcade lightgun state.</param>
    /// <param name="destination">Destination span.</param>
    /// <param name="length">Current packet length.</param>
    public static void WriteArcadeLightgun(ArcadeLightgunState state, Span<byte> destination, ref int length)
    {
        var x = state.Holster ? 0 : Math.Clamp(state.X, 0, 319);
        var y = state.Holster ? 0 : Math.Clamp(state.Y, 0, 239);
        var hitCount = state.Holster ? (byte)0 : (byte)0x1F;
        var counter = LightgunCounter(x, y);

        destination[length++] = 0x4D;
        destination[length++] = (byte)(
            (state.Trigger ? 0x80 : 0)
            | (state.Service ? 0x40 : 0)
            | (state.Coins ? 0x20 : 0)
            | (state.Start ? 0x10 : 0)
            | (state.Holster ? 0x08 : 0)
            | (int)((counter & 0x80000) >> 19));
        destination[length++] = (byte)((counter & 0x7F800) >> 11);
        destination[length++] = (byte)((counter & 0x007F8) >> 3);
        destination[length++] = (byte)(((counter & 0x00007) << 5) | hitCount);
    }

    /// <summary>Writes one Orbatak trackball record (four bytes).</summary>
    /// <param name="state">Trackball state with deltas in the -24..24 range.</param>
    /// <param name="destination">Destination span.</param>
    /// <param name="length">Current packet length.</param>
    public static void WriteOrbatakTrackball(
        OrbatakTrackballState state,
        Span<byte> destination,
        ref int length)
    {
        destination[length++] = 0x49;
        destination[length++] = (byte)((state.DeltaY & 0x3C0) >> 6);
        destination[length++] = (byte)(
            ((state.DeltaY & 0x03F) << 2)
            | ((state.DeltaX & 0x300) >> 8));
        destination[length++] = (byte)(state.DeltaX & 0xFF);
    }

    /// <summary>Writes one Orbatak cabinet-button record (three bytes).</summary>
    /// <param name="state">Cabinet buttons.</param>
    /// <param name="destination">Destination span.</param>
    /// <param name="length">Current packet length.</param>
    public static void WriteOrbatakButtons(
        OrbatakButtonsState state,
        Span<byte> destination,
        ref int length)
    {
        destination[length++] = 0xC0;
        destination[length++] = 0;
        destination[length++] = (byte)(
            (state.CoinP1 ? 0x20 : 0)
            | (state.CoinP2 ? 0x10 : 0)
            | (state.StartP1 ? 0x02 : 0)
            | (state.StartP2 ? 0x04 : 0)
            | (state.Service ? 0x01 : 0));
    }

    /// <summary>Converts a signed 16-bit axis position to the PBUS 10-bit position.</summary>
    /// <param name="position">Axis position, -32768 to 32767.</param>
    /// <returns>A value from 0 to 1023.</returns>
    public static int Position10(int position)
    {
        var value = (Math.Clamp(position, -32768, 32767) + 32768) / 64;
        return Math.Clamp(value, 0, 1023);
    }

    /// <summary>Computes the lightgun scan counter for a pixel position.</summary>
    /// <param name="x">Horizontal position, clamped to 0-319.</param>
    /// <param name="y">Vertical position, clamped to 0-239.</param>
    /// <returns>The 20-bit scan counter.</returns>
    public static uint LightgunCounter(int x, int y)
    {
        x = Math.Clamp(x, 0, 319);
        y = Math.Clamp(y, 0, 239);
        var counter = ((y * 12707) + ((x * 1030 * 10) / 320)) / 10;
        return (uint)(counter + 12835) & 0x000FFFFF;
    }

    /// <summary>Pads the packet with <see cref="PadByte"/> to a four-byte boundary.</summary>
    /// <param name="destination">Destination span.</param>
    /// <param name="length">Current packet length.</param>
    /// <returns>The padded length.</returns>
    public static int Pad(Span<byte> destination, int length)
    {
        do
        {
            destination[length++] = PadByte;
        }
        while ((length & 3) != 0);

        return length;
    }
}
