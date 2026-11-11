using System;

namespace FiveDo.Video;

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
    /// <param name="pixelAspect">
    /// Height of one source row relative to a square output pixel. Defaults to 1. Woven
    /// 480i/576i frames use 0.5 so the displayed geometry stays stable and integer scaling
    /// is applied in display space.
    /// </param>
    /// <returns>The centered destination rectangle.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is not positive.</exception>
    public static ViewportRect Compute(
        int sourceWidth,
        int sourceHeight,
        int outputWidth,
        int outputHeight,
        RendererSettings settings,
        double pixelAspect = 1.0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceHeight, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(outputWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(outputHeight, 1);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        if (!double.IsFinite(pixelAspect) || pixelAspect <= 0)
        {
            pixelAspect = 1.0;
        }

        var logicalHeight = Math.Max(1, (int)Math.Round(sourceHeight * pixelAspect));

        if (settings.Aspect == AspectMode.Stretch)
        {
            return new ViewportRect(0, 0, outputWidth, outputHeight);
        }

        var targetAspect = settings.Aspect switch
        {
            AspectMode.FourThree => 4.0 / 3.0,
            AspectMode.FiveFour => 5.0 / 4.0,
            AspectMode.SixteenNine => 16.0 / 9.0,
            AspectMode.SixteenTen => 16.0 / 10.0,
            AspectMode.TwentyOneNine => 21.0 / 9.0,
            AspectMode.Custom => settings.CustomAspect,
            _ => (double)sourceWidth / logicalHeight,
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
            var scaleY = maxHeight / (double)logicalHeight;
            var scale = Math.Max(1, (int)Math.Floor(Math.Min(scaleX, scaleY)));
            var integerWidth = sourceWidth * scale;
            var integerHeight = logicalHeight * scale;
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
