namespace FiveDo.Video;

/// <summary>Selects the pattern drawn in the void area around the presented image.</summary>
/// <remarks>
/// The shell renders these patterns around the software presenter, and the GPU presenters
/// use the same selection for their letterbox/pillarbox bars. The pattern is presentation
/// state only; it never changes emulation, audio, frame hashes, or save states.
/// </remarks>
public enum VoidPattern
{
    /// <summary>A single solid color.</summary>
    Solid,

    /// <summary>A two-tone checkerboard.</summary>
    Checker,

    /// <summary>A thin grid.</summary>
    Grid,

    /// <summary>Horizontal scanlines.</summary>
    Scanline,

    /// <summary>The 4DO-style blue diagonal gradient.</summary>
    FourDO,

    /// <summary>A grid of soft raised bumps.</summary>
    Bumps,

    /// <summary>Brushed-metal horizontal streaks.</summary>
    Metal,
}
