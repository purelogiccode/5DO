namespace FiveDo.Core;

/// <summary>
/// Selects the guest ARM60 execution backend.
/// </summary>
public enum CpuBackendKind
{
    /// <summary>The reference interpreter. This is the default and the accuracy oracle.</summary>
    Interpreter = 0,

    /// <summary>
    /// The optional managed IL JIT. It compiles hot straight-line blocks into IL, verifies
    /// each instruction against the current code, and falls back to the interpreter for
    /// unsupported instructions or when dynamic code is unavailable.
    /// </summary>
    Jit = 1,
}
