namespace FiveDo.Madam;

/// <summary>
/// The result of decoding one CEL pixel.
/// </summary>
public readonly struct PixelDecodeResult
{
    /// <summary>Initializes a new instance of the <see cref="PixelDecodeResult"/> struct.</summary>
    /// <param name="pixel">Decoded 16-bit pixel.</param>
    /// <param name="amv">Attribute/AMV value.</param>
    /// <param name="transparent">Whether the pixel is transparent.</param>
    public PixelDecodeResult(ushort pixel, ushort amv, bool transparent)
    {
        Pixel = pixel;
        Amv = amv;
        Transparent = transparent;
    }

    /// <summary>Gets the decoded 16-bit pixel.</summary>
    public ushort Pixel { get; }

    /// <summary>Gets the attribute/AMV value.</summary>
    public ushort Amv { get; }

    /// <summary>Gets a value indicating whether the pixel is transparent after the background mask.</summary>
    public bool Transparent { get; }
}
