namespace FiveDo.Core;

/// <summary>
/// Pixel layout of a CPU frame buffer.
/// </summary>
public enum PixelFormat
{
    /// <summary>Blue, green, red, alpha, in memory order (little-endian 0xAARRGGBB).</summary>
    Bgra8888 = 0,

    /// <summary>Red, green, blue, alpha, in memory order (little-endian 0xAABBGGRR).</summary>
    Rgba8888 = 1,
}
