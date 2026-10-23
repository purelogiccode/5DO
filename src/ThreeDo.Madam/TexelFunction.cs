namespace ThreeDo.Madam;

/// <summary>
/// CEL rasterizer path selected by <see cref="CelGeometry.TryInitVisual"/>.
/// </summary>
/// <remarks>
/// Values match FreeDO's <c>TEXEL_FUN_NUMBER</c> global.
/// </remarks>
public enum TexelFunction
{
    /// <summary>Axis-aligned line mapping. FreeDO: <c>TEXEL_FUN_NUMBER == 0</c>.</summary>
    Line = 0,

    /// <summary>Scaled mapping. FreeDO: <c>TEXEL_FUN_NUMBER == 1</c>.</summary>
    Scale = 1,

    /// <summary>Arbitrary quad mapping. FreeDO: <c>TEXEL_FUN_NUMBER == 2</c>.</summary>
    Arbitrary = 2,
}
