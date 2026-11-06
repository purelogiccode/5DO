namespace FiveDo.Video;

/// <summary>How consecutive 3DO fields are combined for presentation.</summary>
public enum DeinterlaceMode
{
    /// <summary>Detect interlaced content and weave it; otherwise present each field unchanged.</summary>
    Auto,

    /// <summary>Present every field unchanged. This is the historical 5DO behavior.</summary>
    Bob,

    /// <summary>Combine two fields into a doubled-height frame (480i from 240p fields).</summary>
    Weave,

    /// <summary>Average the current field with the previous field into one 240-line frame.</summary>
    Blend,
}
