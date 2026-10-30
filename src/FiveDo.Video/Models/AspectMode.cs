
namespace FiveDo.Video;


/// <summary>Output aspect-ratio policy.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "The public setting is named Integer scaling.")]
public enum AspectMode
{
    /// <summary>Integer scale with letterboxing.</summary>
    Integer,

    /// <summary>4:3 aspect with letterboxing.</summary>
    FourThree,

    /// <summary>Stretch to the output rectangle.</summary>
    Stretch,

    /// <summary>Explicit custom aspect ratio.</summary>
    Custom,
}
