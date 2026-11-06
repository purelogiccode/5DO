namespace FiveDo.Cli;

/// <summary>
/// One deterministic machine sample taken at a requested frame boundary.
/// </summary>
/// <param name="Frame">Completed frame count.</param>
/// <param name="Pc">ARM program counter after the frame.</param>
/// <param name="Cpsr">ARM current program status register after the frame.</param>
/// <param name="Instructions">ARM instructions retired after the frame.</param>
/// <param name="FrameHash">FNV-1a hash of the assembled BGRA8888 frame.</param>
/// <param name="Samples">DSP samples produced after the frame.</param>
/// <param name="AudioHash">Cumulative PCM hash, when audio hashing is enabled.</param>
/// <param name="StateHash">Save-state hash, when <c>--state-hash</c> is enabled.</param>
/// <param name="CdStatus">CD-ROM drive status byte.</param>
/// <param name="CdPoll">CD-ROM poll byte.</param>
/// <param name="CdCommandHash">Cumulative hash of guest CD command opcodes.</param>
/// <param name="CdCommandCount">Number of guest CD commands issued.</param>
/// <param name="DspPc">DSP program counter.</param>
/// <param name="DspSema">Packed DSP semaphore status and data.</param>
/// <param name="DspSeed">DSP PRNG state.</param>
/// <param name="CelBusyCycles">Remaining CEL busy cycles in the Opera timing mode.</param>
internal sealed record CliCheckpointResult(
    long Frame,
    uint Pc,
    uint Cpsr,
    long Instructions,
    uint FrameHash,
    long Samples,
    uint? AudioHash,
    uint? StateHash,
    byte CdStatus,
    byte CdPoll,
    uint CdCommandHash,
    int CdCommandCount,
    uint DspPc,
    uint DspSema,
    uint DspSeed,
    ulong CelBusyCycles);
