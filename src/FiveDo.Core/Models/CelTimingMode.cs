namespace FiveDo.Core;

/// <summary>
/// Selects how the MADAM CEL engine consumes emulated time.
/// </summary>
/// <remarks>
/// FreeDO renders a CEL list synchronously and immediately returns the FSM to idle, so the
/// CPU never observes CEL busy time. Opera instead draws the list at the next CPU tick and
/// keeps the CEL status register busy until an estimated deadline based on the number of
/// rendered pixels, walked texels, and list entries (about nine million pixels per second).
/// The default remains the FreeDO-compatible model so existing save states and golden
/// frames are unchanged.
/// </remarks>
public enum CelTimingMode
{
    /// <summary>FreeDO's synchronous draw with an immediate return to idle. This is the default.</summary>
    FreeDoCompatible = 0,

    /// <summary>Opera's deferred draw with a pixel/texel busy deadline.</summary>
    OperaBusy = 1,
}
