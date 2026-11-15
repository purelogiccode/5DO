namespace NRGSharp;

/// <summary>Identifies the Nero NRG footer version.</summary>
public enum NrgVersion
{
    /// <summary>Nero Burning ROM 5.0 and earlier: <c>NERO</c> footer with 32-bit offsets.</summary>
    V1,

    /// <summary>Nero Burning ROM 5.5 and later: <c>NER5</c> footer with 64-bit offsets.</summary>
    V2,
}
