using FiveDo.Core;

namespace FiveDo.Video;

/// <summary>
/// Selects the resolution used for the final presentation pass.
/// </summary>
/// <remarks>
/// This is output upscaling, not internal CEL rendering. The emulated frame stays at its
/// native resolution; the selected value controls the size of the filtered image that the
/// host presents or scales into the window.
/// </remarks>
public enum OutputResolution
{
    /// <summary>Match the physical pixel size of the presentation window.</summary>
    Window,

    /// <summary>Native 320x240 output.</summary>
    Native,

    /// <summary>2x output (640x480).</summary>
    Scale2X,

    /// <summary>3x output (960x720).</summary>
    Scale3X,

    /// <summary>4x output (1280x960).</summary>
    Scale4X,

    /// <summary>720p output (1280x720).</summary>
    Hd720,

    /// <summary>1080p output (1920x1080).</summary>
    FullHd1080,

    /// <summary>1440p output (2560x1440).</summary>
    Qhd1440,

    /// <summary>4K UHD output (3840x2160).</summary>
    Uhd2160,
}

/// <summary>
/// Maps <see cref="OutputResolution"/> values to pixel dimensions.
/// </summary>
public static class OutputResolutionSizes
{
    /// <summary>Gets the fixed pixel size for a resolution preset.</summary>
    /// <param name="resolution">Resolution preset.</param>
    /// <param name="width">Output width in pixels.</param>
    /// <param name="height">Output height in pixels.</param>
    /// <returns>
    /// <see langword="true"/> when the preset has a fixed size; <see langword="false"/> when
    /// the output should follow the window size.
    /// </returns>
    public static bool TryGetSize(OutputResolution resolution, out int width, out int height)
    {
        switch (resolution)
        {
            case OutputResolution.Native:
                width = EmulatorConstants.VideoWidth;
                height = EmulatorConstants.VideoHeight;
                return true;
            case OutputResolution.Scale2X:
                width = 640;
                height = 480;
                return true;
            case OutputResolution.Scale3X:
                width = 960;
                height = 720;
                return true;
            case OutputResolution.Scale4X:
                width = 1280;
                height = 960;
                return true;
            case OutputResolution.Hd720:
                width = 1280;
                height = 720;
                return true;
            case OutputResolution.FullHd1080:
                width = 1920;
                height = 1080;
                return true;
            case OutputResolution.Qhd1440:
                width = 2560;
                height = 1440;
                return true;
            case OutputResolution.Uhd2160:
                width = 3840;
                height = 2160;
                return true;
            default:
                width = 0;
                height = 0;
                return false;
        }
    }
}
