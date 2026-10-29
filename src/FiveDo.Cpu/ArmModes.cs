using System;

namespace FiveDo.Cpu;

/// <summary>
/// ARM processor mode constants and the FreeDOCore mode lookup table.
/// </summary>
internal static class ArmModes
{
    internal const byte User = 0;
    internal const byte Fiq = 1;
    internal const byte Irq = 2;
    internal const byte Svc = 3;
    internal const byte Abt = 4;
    internal const byte Und = 5;
    internal const byte Unknown = 0xff;

    internal static readonly byte[] Table =
    {
        Unknown, Unknown, Unknown, Unknown,
        Unknown, Unknown, Unknown, Unknown,
        Unknown, Unknown, Unknown, Unknown,
        Unknown, Unknown, Unknown, Unknown,
        User, Fiq, Irq, Svc,
        Unknown, Unknown, Unknown, Abt,
        Unknown, Unknown, Unknown, Und,
        Unknown, Unknown, Unknown, Unknown,
    };
}
