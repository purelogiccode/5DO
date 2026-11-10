namespace FiveDo.Host;

/// <summary>Selects the pattern drawn in the void area around the presented image.</summary>
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
}
