using System.Runtime.InteropServices;

namespace FiveDo.Video;


/// <summary>An output rectangle in destination pixels.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Rectangle width.</param>
/// <param name="Height">Rectangle height.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ViewportRect(int X, int Y, int Width, int Height)
{
    /// <summary>Gets a value indicating whether the rectangle has a non-zero area.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
