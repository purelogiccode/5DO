
namespace FiveDo.Video;


/// <summary>Output aspect-ratio policy.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "The public setting is named Integer scaling.")]
public enum AspectMode
{
    /// <summary>Integer scale with letterboxing.</summary>
    Integer,

    /// <summary>4:3 aspect with letterboxing.</summary>
    FourThree,

    /// <summary>5:4 aspect with letterboxing.</summary>
    FiveFour,

    /// <summary>16:9 aspect with letterboxing.</summary>
    SixteenNine,

    /// <summary>16:10 aspect with letterboxing.</summary>
    SixteenTen,

    /// <summary>21:9 aspect with letterboxing.</summary>
    TwentyOneNine,

    /// <summary>Stretch to the output rectangle.</summary>
    Stretch,

    /// <summary>Explicit custom aspect ratio.</summary>
    Custom,
}
