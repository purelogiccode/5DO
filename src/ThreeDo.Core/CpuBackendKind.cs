namespace ThreeDo.Core;

/// <summary>
/// Selects the guest ARM60 execution backend.
/// </summary>
public enum CpuBackendKind
{
    /// <summary>The reference interpreter. This is the default and the accuracy oracle.</summary>
    Interpreter = 0,

    /// <summary>
    /// The optional managed IL JIT. Reserved for M12; selecting it before the JIT exists is
    /// rejected by <see cref="EmulatorTuning.Validate"/>.
    /// </summary>
    Jit = 1,
}
