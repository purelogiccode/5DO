namespace FiveDo.Host.Osd;

/// <summary>
/// Anchor position for the on-screen display overlay.
/// </summary>
public enum OsdPosition
{
    /// <summary>Top-left corner.</summary>
    TopLeft = 0,

    /// <summary>Top edge, centered.</summary>
    TopCenter,

    /// <summary>Top-right corner.</summary>
    TopRight,

    /// <summary>Bottom-left corner.</summary>
    BottomLeft,

    /// <summary>Bottom edge, centered.</summary>
    BottomCenter,

    /// <summary>Bottom-right corner.</summary>
    BottomRight,
}
