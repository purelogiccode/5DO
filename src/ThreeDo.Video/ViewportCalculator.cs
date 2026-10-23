using System;

namespace ThreeDo.Video;

/// <summary>An output rectangle in destination pixels.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Rectangle width.</param>
/// <param name="Height">Rectangle height.</param>
public readonly record struct ViewportRect(int X, int Y, int Width, int Height)
{
    /// <summary>Gets a value indicating whether the rectangle has a non-zero area.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>
/// Computes the presented image rectangle for the configured aspect and integer-scaling policy.
/// </summary>
public static class ViewportCalculator
{
    /// <summary>Computes the destination rectangle for a source frame.</summary>
    /// <param name="sourceWidth">Source width.</param>
    /// <param name="sourceHeight">Source height.</param>
    /// <param name="outputWidth">Output width.</param>
    /// <param name="outputHeight">Output height.</param>
    /// <param name="settings">Renderer settings.</param>
    /// <returns>The centered destination rectangle.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is not positive.</exception>
    public static ViewportRect Compute(
        int sourceWidth,
        int sourceHeight,
        int outputWidth,
        int outputHeight,
        RendererSettings settings)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceHeight, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(outputWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(outputHeight, 1);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        if (settings.Aspect == AspectMode.Stretch)
        {
            return new ViewportRect(0, 0, outputWidth, outputHeight);
        }

        var targetAspect = settings.Aspect switch
        {
            AspectMode.FourThree => 4.0 / 3.0,
            AspectMode.Custom => settings.CustomAspect,
            _ => (double)sourceWidth / sourceHeight,
        };

        var maxWidth = outputWidth;
        var maxHeight = (int)Math.Round(outputWidth / targetAspect);
        if (maxHeight > outputHeight)
        {
            maxHeight = outputHeight;
            maxWidth = (int)Math.Round(outputHeight * targetAspect);
        }

        maxWidth = Math.Max(1, maxWidth);
        maxHeight = Math.Max(1, maxHeight);

        if (settings.Aspect == AspectMode.Integer || (settings.IntegerScale && settings.Aspect == AspectMode.FourThree))
        {
            var scaleX = maxWidth / (double)sourceWidth;
            var scaleY = maxHeight / (double)sourceHeight;
            var scale = Math.Max(1, (int)Math.Floor(Math.Min(scaleX, scaleY)));
            var integerWidth = sourceWidth * scale;
            var integerHeight = sourceHeight * scale;
            if (integerWidth <= maxWidth && integerHeight <= maxHeight)
            {
                maxWidth = integerWidth;
                maxHeight = integerHeight;
            }
        }

        var x = (outputWidth - maxWidth) / 2;
        var y = (outputHeight - maxHeight) / 2;
        return new ViewportRect(x, y, maxWidth, maxHeight);
    }
}
