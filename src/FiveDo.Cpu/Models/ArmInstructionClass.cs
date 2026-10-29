

namespace FiveDo.Cpu;


/// <summary>
/// ARM instruction classes in FreeDOCore's decode order.
/// </summary>
public enum ArmInstructionClass
{
    /// <summary>MUL or MLA.</summary>
    Multiply,

    /// <summary>SWP or SWPB.</summary>
    Swap,

    /// <summary>Data processing, including MRS and MSR.</summary>
    DataProcessing,

    /// <summary>Undefined instruction vector.</summary>
    Undefined,

    /// <summary>LDR or STR.</summary>
    SingleDataTransfer,

    /// <summary>LDM or STM.</summary>
    BlockTransfer,

    /// <summary>B or BL.</summary>
    Branch,

    /// <summary>SWI.</summary>
    SoftwareInterrupt,

    /// <summary>Coprocessor or otherwise unhandled encoding.</summary>
    Coprocessor,
}
