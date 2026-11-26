namespace FiveDo.Host;

/// <summary>Quality preset for MP4 video recording.</summary>
public enum VideoRecordingQuality
{
    /// <summary>Smaller files and a softer image.</summary>
    Low,

    /// <summary>Balanced quality and size (the default).</summary>
    Medium,

    /// <summary>Larger files and a sharper image.</summary>
    High,
}
