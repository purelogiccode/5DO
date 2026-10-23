using System;
using ThreeDo.Core;

namespace ThreeDo.Dsp;

/// <summary>
/// A deterministic DSP stand-in used until the full DSP core lands in M5.
/// </summary>
/// <remarks>
/// The stub stores NMem/IMem writes so BIOS initialization sequences that program the DSP
/// can read their data back, and it implements the reference PRNG exactly. It does not
/// execute DSP instructions and always returns a silent sample from <see cref="Loop"/>.
/// </remarks>
public sealed class DspStub : IDsp
{
    private readonly ushort[] _nmem = new ushort[2048];
    private readonly ushort[] _imem = new ushort[1024];

    private uint _seed = 0xa5a5a5a5;
    private uint _semaphore;
    private bool _running;

    /// <summary>Gets a value indicating whether the DSP is running.</summary>
    public bool IsRunning => _running;

    /// <summary>Gets the current PRNG seed.</summary>
    public uint Seed => _seed;

    /// <inheritdoc />
    public void Reset()
    {
        Array.Clear(_nmem);
        Array.Clear(_imem);
        _seed = 0xa5a5a5a5;
        _semaphore = 0;
        _running = false;
    }

    /// <inheritdoc />
    public void SetRunning(bool running) => _running = running;

    /// <inheritdoc />
    public void WriteMemory(int address, ushort value)
    {
        // FreeDO: _dsp_WriteMemory masks to 0x3FF even though NMem has 2048 entries.
        _nmem[address & 0x3ff] = value;
    }

    /// <inheritdoc />
    public void WriteInstructionMemory(int address, ushort value) => _imem[address & 0x3ff] = value;

    /// <inheritdoc />
    public ushort ReadInstructionMemory(int address) => _imem[address & 0x3ff];

    /// <inheritdoc />
    public void ArmWriteSemaphore(uint value) => _semaphore = value;

    /// <inheritdoc />
    public uint ArmReadSemaphore() => _semaphore;

    /// <inheritdoc />
    public uint Loop() => 0;

    /// <inheritdoc />
    public uint FastRandom()
    {
        _seed = unchecked((69069 * _seed) + 1);
        return _seed & 0xffff;
    }
}
