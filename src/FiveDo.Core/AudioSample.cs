namespace FiveDo.Core;

/// <summary>
/// One stereo audio sample produced by the DSP.
/// </summary>
/// <remarks>
/// The DSP emits a packed 32-bit value; the host interprets the channel order. The
/// <see cref="Packed"/> representation stores the left channel in the low 16 bits and the
/// right channel in the high 16 bits until M5 verifies the reference's exact channel order.
/// </remarks>
/// <param name="Left">Left channel, signed 16-bit.</param>
/// <param name="Right">Right channel, signed 16-bit.</param>
public readonly record struct AudioSample(short Left, short Right)
{
    /// <summary>Gets the packed 32-bit sample.</summary>
    public uint Packed => (ushort)Left | ((uint)(ushort)Right << 16);

    /// <summary>Creates a sample from a packed 32-bit value.</summary>
    /// <param name="packed">Packed sample value.</param>
    /// <returns>The unpacked sample.</returns>
    public static AudioSample FromPacked(uint packed)
    {
        return new AudioSample(unchecked((short)(packed & 0xFFFF)), unchecked((short)(packed >> 16)));
    }
}
