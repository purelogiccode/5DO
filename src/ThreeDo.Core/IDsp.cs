namespace ThreeDo.Core;

/// <summary>
/// The audio DSP as seen by CLIO and the machine frame loop.
/// </summary>
/// <remarks>
/// FreeDO: the <c>_dsp_*</c> functions called from <c>Clio.cpp</c> and <c>_3do_sys.cpp</c>.
/// The full DSP implementation arrives in M5; this contract lets CLIO and the frame loop be
/// wired and tested first.
/// </remarks>
public interface IDsp
{
    /// <summary>Resets the DSP core. FreeDO: <c>_dsp_Reset</c>.</summary>
    void Reset();

    /// <summary>Starts or stops DSP execution. FreeDO: <c>_dsp_SetRunning</c>.</summary>
    /// <param name="running"><see langword="true"/> to run.</param>
    void SetRunning(bool running);

    /// <summary>Writes one 16-bit word to DSP data memory. FreeDO: <c>_dsp_WriteMemory</c>.</summary>
    /// <param name="address">DSP data-memory address.</param>
    /// <param name="value">Word value.</param>
    void WriteMemory(int address, ushort value);

    /// <summary>Writes one 16-bit word to DSP instruction memory. FreeDO: <c>_dsp_WriteIMem</c>.</summary>
    /// <param name="address">DSP instruction-memory address.</param>
    /// <param name="value">Word value.</param>
    void WriteInstructionMemory(int address, ushort value);

    /// <summary>Reads one 16-bit word from DSP instruction memory. FreeDO: <c>_dsp_ReadIMem</c>.</summary>
    /// <param name="address">DSP instruction-memory address.</param>
    /// <returns>The word value.</returns>
    ushort ReadInstructionMemory(int address);

    /// <summary>Writes the ARM-to-DSP semaphore. FreeDO: <c>_dsp_ARMwrite2sema4</c>.</summary>
    /// <param name="value">Semaphore value.</param>
    void ArmWriteSemaphore(uint value);

    /// <summary>Reads the ARM-to-DSP semaphore. FreeDO: <c>_dsp_ARMread2sema4</c>.</summary>
    /// <returns>Semaphore value.</returns>
    uint ArmReadSemaphore();

    /// <summary>Executes the DSP until it sleeps. FreeDO: <c>_dsp_Loop</c>.</summary>
    /// <returns>The packed stereo sample produced by this call.</returns>
    uint Loop();

    /// <summary>Advances the DSP PRNG and returns the low 16 bits. FreeDO: <c>fastrand</c>.</summary>
    /// <returns>A pseudo-random 16-bit value.</returns>
    uint FastRandom();
}
