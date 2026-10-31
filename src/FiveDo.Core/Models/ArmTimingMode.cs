namespace FiveDo.Core;

/// <summary>
/// Selects the ARM60 memory-cycle class used by the interpreter.
/// </summary>
/// <remarks>
/// ARM instruction timings are expressed as N/S/I/C cycle classes. The length of an
/// N cycle is a property of the memory system, not of the ARM60 core. FreeDO models
/// an N cycle as four ARM clock cycles; Opera uses the ARM60 datasheet/SDK value of
/// two cycles for the uncached 3DO bus. The default remains the FreeDO-compatible
/// value so existing save states and golden frames are unchanged.
/// </remarks>
public enum ArmTimingMode
{
    /// <summary>FreeDO's <c>Ncycle = 4</c> baseline. This is the default.</summary>
    FreeDoCompatible = 0,

    /// <summary>ARM60 datasheet/SDK <c>Ncycle = 2</c>, as used by Opera.</summary>
    Arm60Datasheet = 1,
}
