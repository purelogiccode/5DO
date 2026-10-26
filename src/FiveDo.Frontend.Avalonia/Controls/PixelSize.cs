using System;

namespace FiveDo.Frontend.Avalonia.Controls;

/// <summary>
/// Converts Avalonia logical bounds and a render scaling to physical pixels.
/// </summary>
/// <remarks>
/// The OpenGL surface must be sized in physical pixels. Avalonia reports
/// <c>Bounds</c> in logical units and <c>TopLevel.RenderScaling</c> as the current
/// display scale, so a 150% desktop must render at 1.5x the logical size rather
/// than downscaling a logical-size surface.
/// </remarks>
internal static class PixelSize
{
    /// <summary>Computes a physical pixel size from logical bounds and a scale factor.</summary>
    /// <param name="width">Logical width.</param>
    /// <param name="height">Logical height.</param>
    /// <param name="scaling">Render scaling (1.0 = 100%).</param>
    /// <returns>The physical width and height, each at least one pixel.</returns>
    public static (int Width, int Height) Compute(double width, double height, double scaling)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || !double.IsFinite(scaling) || scaling <= 0)
        {
            return (1, 1);
        }

        var physicalWidth = Math.Max(1, (int)Math.Round(width * scaling, MidpointRounding.AwayFromZero));
        var physicalHeight = Math.Max(1, (int)Math.Round(height * scaling, MidpointRounding.AwayFromZero));
        return (physicalWidth, physicalHeight);
    }
}
