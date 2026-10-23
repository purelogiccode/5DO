namespace ThreeDo.Core;

/// <summary>
/// The architectural surface of a guest CPU backend.
/// </summary>
/// <remarks>
/// <para>
/// The ARM60 interpreter is the default and reference backend. An optional managed IL JIT
/// can be added later behind the same contract; it must produce bit-identical architectural
/// state and cycle counts.
/// </para>
/// <para>
/// Save states are written through the explicit <see cref="IStateWriter"/> format rather than
/// by copying object layouts.
/// </para>
/// </remarks>
public interface ICpuBackend
{
    /// <summary>Gets or sets the program counter (R15).</summary>
    uint ProgramCounter { get; set; }

    /// <summary>Gets or sets the current program status register.</summary>
    uint Cpsr { get; set; }

    /// <summary>Gets the number of instructions executed since the backend was created.</summary>
    long InstructionsExecuted { get; }

    /// <summary>Resets the architectural state to the FreeDOCore power-on values.</summary>
    void Reset();

    /// <summary>Executes exactly one instruction.</summary>
    /// <returns>The number of ARM clock cycles consumed by the instruction.</returns>
    int Step();

    /// <summary>Reads a user-visible register (R0-R15).</summary>
    /// <param name="index">Register index, 0-15.</param>
    /// <returns>The register value.</returns>
    uint ReadRegister(int index);

    /// <summary>Writes a user-visible register (R0-R15).</summary>
    /// <param name="index">Register index, 0-15.</param>
    /// <param name="value">Value to write.</param>
    void WriteRegister(int index, uint value);

    /// <summary>Writes the CPU save-state section payload.</summary>
    /// <param name="writer">Destination writer.</param>
    void SaveState(IStateWriter writer);

    /// <summary>Reads the CPU save-state section payload.</summary>
    /// <param name="reader">Source reader.</param>
    void LoadState(IStateReader reader);
}
