using System;
using System.Collections.Generic;
using System.Numerics;
using FiveDo.Core;

namespace FiveDo.Cpu;

/// <summary>
/// The ARM60 interpreter, ported from FreeDOCore's <c>arm.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// The interpreter executes exactly one instruction per <see cref="Step"/> call and returns
/// the same cycle count as <c>_arm_Execute</c>. It preserves the reference's decode order,
/// including the C++ switch fallthrough, the banked-register model, the CLIO PC-adjust
/// quirk, the Anvil/Crash 'n Burn compatibility hacks, and the immediate data-processing
/// carry-out behaviour. The N-cycle class is selected by
/// <see cref="EmulatorTuning.ArmTiming"/>; the default remains FreeDO's N=4.
/// </para>
/// <para>
/// All mutable state is instance state; the CPU can be used by independent machines in
/// parallel. The bus is the only external dependency.
/// </para>
/// </remarks>
public sealed partial class Arm60 : ICpuBackend, IStateSection
{
    private const int Scycle = 1;
    private const int Icycle = 1;

    private static readonly int InstructionClassCount = Enum.GetValues<ArmInstructionClass>().Length;

    private readonly int _nCycle;

    private static readonly bool[] IsLogic =
    {
        true, true, false, false,
        false, false, false, false,
        true, true, false, false,
        true, true, true, true,
    };

    private readonly IAddressBus _bus;
    private readonly IRam? _ram;
    private readonly IInterruptSink? _interrupts;
    private readonly IPcAdjustSource? _pcAdjustSource;
    private readonly EmulatorTuning _tuning;
    private readonly CompatibilityState _compatibility;
    private readonly Arm60State _state = new();
    private readonly long[] _classInstructions = new long[InstructionClassCount];
    private readonly long[] _classCycles = new long[InstructionClassCount];
    private readonly Dictionary<uint, long> _swiCommentCounts = [];

    private int _cycles;
    private uint _carryOut;
    private long _instructionsExecuted;
    private long _cyclesExecuted;
    private long _swiInstructions;
    private long _swiHleInstructions;
    private uint _lastSwiComment;
    private int _ldmAddress;
    private int _ldmValue;
    private int _ldmInUse;

    /// <summary>Initializes a new instance of the <see cref="Arm60"/> class.</summary>
    /// <param name="bus">CPU address bus.</param>
    /// <param name="interrupts">Optional FIQ source. May be null in unit tests.</param>
    /// <param name="tuning">Machine tuning. Defaults to <see cref="EmulatorTuning.Default"/>.</param>
    /// <param name="compatibility">Optional shared compatibility state.</param>
    /// <param name="ram">Optional raw RAM accessor used by Portfolio OS SWI HLE.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bus"/> is null.</exception>
    public Arm60(
        IAddressBus bus,
        IInterruptSink? interrupts = null,
        EmulatorTuning? tuning = null,
        CompatibilityState? compatibility = null,
        IRam? ram = null)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        _ram = ram;
        _interrupts = interrupts;
        _pcAdjustSource = bus as IPcAdjustSource;
        _tuning = tuning ?? EmulatorTuning.Default;
        _tuning.Validate();
        _nCycle = _tuning.ArmTiming == ArmTimingMode.Arm60Datasheet ? 2 : 4;
        _compatibility = compatibility ?? new CompatibilityState { FixMode = _tuning.FixMode };
        SwiHleEnabled = _tuning.SwiHle;
        Reset();
    }

    /// <summary>Gets the architectural state.</summary>
    public Arm60State State => _state;

    /// <summary>Gets the machine tuning.</summary>
    public EmulatorTuning Tuning => _tuning;

    /// <summary>Gets the N-cycle length in ARM clock cycles selected by <see cref="EmulatorTuning.ArmTiming"/>.</summary>
    public int NCycle => _nCycle;

    /// <summary>
    /// Gets or sets a value indicating whether per-instruction-class cycle telemetry is collected.
    /// </summary>
    /// <remarks>
    /// Disabled by default so the hot interpreter loop stays unchanged. Enable it for
    /// cycle-class diagnostics and differential evidence; see <see cref="GetCycleClassUsage"/>.
    /// </remarks>
    public bool CycleClassTelemetryEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Portfolio OS fixed-point math SWIs are
    /// handled on the host. Opera: <c>opera_arm_swi_hle_set</c>.
    /// </summary>
    /// <remarks>
    /// The option is initialized from <see cref="EmulatorTuning.SwiHle"/> and has no effect
    /// unless an <see cref="IRam"/> accessor was supplied to the constructor. SWIs outside
    /// Opera's HLE set always trap to the guest vector.
    /// </remarks>
    public bool SwiHleEnabled { get; set; }

    /// <summary>Gets the number of SWIs executed by the guest, including HLE calls.</summary>
    /// <remarks>Telemetry only; cleared by <see cref="Reset"/> and not serialized.</remarks>
    public long SwiInstructions => _swiInstructions;

    /// <summary>Gets the number of SWIs handled by the Portfolio OS HLE.</summary>
    /// <remarks>Telemetry only; cleared by <see cref="Reset"/> and not serialized.</remarks>
    public long SwiHleInstructions => _swiHleInstructions;

    /// <summary>Gets the 24-bit comment of the most recent SWI.</summary>
    /// <remarks>Telemetry only; cleared by <see cref="Reset"/> and not serialized.</remarks>
    public uint LastSwiComment => _lastSwiComment;

    /// <summary>
    /// Gets or sets a value indicating whether per-comment SWI telemetry is collected.
    /// </summary>
    /// <remarks>
    /// Disabled by default so normal runs do not allocate a dictionary entry for every SWI.
    /// Enable it for HLE coverage diagnostics; see <see cref="GetSwiCommentUsage"/>.
    /// </remarks>
    public bool SwiCommentTelemetryEnabled { get; set; }

    /// <summary>Gets the SWI comment histogram collected since the last reset.</summary>
    /// <returns>Rows ordered by descending count, then ascending comment.</returns>
    /// <remarks>Returns an empty list when <see cref="SwiCommentTelemetryEnabled"/> is off.</remarks>
    public IReadOnlyList<ArmSwiCommentUsage> GetSwiCommentUsage()
    {
        var result = new ArmSwiCommentUsage[_swiCommentCounts.Count];
        var index = 0;
        foreach (KeyValuePair<uint, long> entry in _swiCommentCounts)
        {
            result[index++] = new ArmSwiCommentUsage(entry.Key, entry.Value);
        }

        Array.Sort(result, static (left, right) =>
        {
            var count = right.Count.CompareTo(left.Count);
            return count != 0 ? count : left.Comment.CompareTo(right.Comment);
        });
        return result;
    }

    /// <summary>Gets the current per-class instruction and cycle totals.</summary>
    /// <returns>One row per <see cref="ArmInstructionClass"/>, in enum order.</returns>
    /// <remarks>
    /// The snapshot allocates a small array. The counters are telemetry only, are not
    /// serialized, and are cleared by <see cref="Reset"/> and <see cref="LoadState(IStateReader)"/>
    /// but not by <see cref="SoftReset"/>.
    /// </remarks>
    public IReadOnlyList<ArmCycleClassUsage> GetCycleClassUsage()
    {
        var result = new ArmCycleClassUsage[InstructionClassCount];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new ArmCycleClassUsage(
                (ArmInstructionClass)i,
                _classInstructions[i],
                _classCycles[i]);
        }

        return result;
    }

    /// <summary>Clears the per-class cycle ledger without changing the CPU state.</summary>
    public void ResetCycleClassUsage()
    {
        Array.Clear(_classInstructions);
        Array.Clear(_classCycles);
    }

    /// <summary>Gets the shared compatibility state.</summary>
    public CompatibilityState Compatibility => _compatibility;

    /// <summary>Gets or sets the program counter (R15).</summary>
    public uint ProgramCounter
    {
        get => _state.ProgramCounter;
        set => _state.ProgramCounter = value;
    }

    /// <summary>Gets or sets the current program status register.</summary>
    /// <remarks>Writing a value with different mode bits switches the banked registers.</remarks>
    public uint Cpsr
    {
        get => _state.Cpsr;
        set => _state.SetCpsr(value);
    }

    /// <summary>Gets the number of instructions executed since the last reset.</summary>
    public long InstructionsExecuted => _instructionsExecuted;

    /// <summary>Gets the number of ARM clock cycles consumed since the last cold reset.</summary>
    /// <remarks>
    /// The counter is telemetry, not architectural state: it is not saved, is not advanced by
    /// <see cref="SoftReset"/>, and continues across save-state loads. CLIO uses it to stamp
    /// FIQ requests for <see cref="FiqLatencyTracker"/>.
    /// </remarks>
    public long CyclesExecuted => _cyclesExecuted;

    /// <summary>
    /// Gets or sets a callback invoked after a FIQ has been taken, with the cycle at which the
    /// vector was entered and the value written to <c>LR_fiq</c>.
    /// </summary>
    /// <remarks>
    /// The callback fires after the interrupted instruction has completed, matching FreeDO's
    /// <c>_arm_Execute</c> and Opera's <c>opera_arm_execute</c>. Vector entry charges no
    /// additional cycles in either reference.
    /// </remarks>
    public Action<long, uint>? FiqEntered { get; set; }

    /// <summary>Gets or sets a value indicating whether a fast interrupt is pending.</summary>
    public bool FiqPending
    {
        get => _state.FiqPending;
        set => _state.FiqPending = value;
    }

    /// <summary>Gets or sets a value indicating whether the second ROM bank is selected.</summary>
    public bool SecondRom
    {
        get => _state.SecondRom;
        set => _state.SecondRom = value;
    }

    /// <summary>Gets or sets a value indicating whether an unmapped access has been observed.</summary>
    public bool DataAbortPending
    {
        get => _state.DataAbortPending;
        set => _state.DataAbortPending = value;
    }

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Cpu;

    /// <summary>Gets the save-state section version.</summary>
    public ushort Version => 1;

    /// <summary>Selects the active ROM bank. FreeDO: <c>SelectROM</c>.</summary>
    /// <param name="bank">Zero for BIOS1, non-zero for BIOS2.</param>
    public void SelectRom(int bank)
    {
        _state.SecondRom = bank > 0;
    }

    /// <summary>Resets the CPU to the FreeDOCore power-on state.</summary>
    public void Reset()
    {
        _state.Reset();
        _cycles = 0;
        _carryOut = 0;
        _instructionsExecuted = 0;
        _cyclesExecuted = 0;
        _swiInstructions = 0;
        _swiHleInstructions = 0;
        _lastSwiComment = 0;
        _swiCommentCounts.Clear();
        _ldmAddress = 0;
        _ldmValue = 0;
        _ldmInUse = 0;
        ResetCycleClassUsage();
        ClearJitCache();
    }

    /// <summary>
    /// Applies a CLIO software reset without a cold CPU reset. Opera:
    /// <c>opera_arm_soft_reset</c>. The interrupted CPSR and next PC remain visible
    /// as SPSR_svc and R14_svc.
    /// </summary>
    public void SoftReset()
    {
        var returnCpsr = _state.Cpsr;
        var returnPc = _state.ProgramCounter;
        _cycles = 0;
        _carryOut = 0;
        _state.Spsr[ArmModes.Svc] = returnCpsr;
        _state.SetCpsr((returnCpsr & ~0x1fu) | 0x13u | 0x40u | 0x80u);
        _state.User[14] = returnPc;
        _state.User[15] = 0;
        _state.DataAbortPending = false;
        _state.FiqPending = false;
    }

    /// <summary>Executes exactly one instruction.</summary>
    /// <returns>The ARM clock cycles consumed by the instruction.</returns>
    public int Step()
    {
        Arm60State state = _state;
        if (_compatibility.BiosAnvil)
        {
            state.ProgramCounter += 64;
            _compatibility.BiosAnvil = false;
            _compatibility.IsAnvil = 1;
        }

        if (state.ProgramCounter == 0x94D60 &&
            state.User[0] == 0x113000 &&
            state.User[1] == 0x113000 &&
            !_compatibility.Cnbfix &&
            (_compatibility.FixMode & FixMode.BitTiming1) != FixMode.None)
        {
            state.ProgramCounter = 0x9E9CC;
            _compatibility.Cnbfix = true;
        }

        var instruction = _bus.ReadInstruction(state.ProgramCounter);
        var isException = instruction == 0xE5101810 && state.Cpsr == 0x80000093;
        state.ProgramCounter += 4;
        _cycles = -Scycle;
        _instructionsExecuted++;

        if (ArmDecoder.ConditionPassed(state.Cpsr, instruction >> 28) && !isException)
        {
            ExecuteInstruction(instruction);
        }

        if ((state.Cpsr & 0x40) == 0 && (state.FiqPending || (_interrupts?.NeedFiq ?? false)))
        {
            EnterFiq();
        }

        var cycles = -_cycles;
        if (CycleClassTelemetryEnabled)
        {
            var instructionClass = (int)ArmDecoder.Classify(instruction);
            _classInstructions[instructionClass]++;
            _classCycles[instructionClass] += cycles;
        }

        _cyclesExecuted += cycles;
        return cycles;
    }

    /// <summary>Reads a user-visible register (R0-R15).</summary>
    /// <param name="index">Register index, 0-15.</param>
    /// <returns>The register value.</returns>
    public uint ReadRegister(int index)
    {
        return _state[index];
    }

    /// <summary>Writes a user-visible register (R0-R15).</summary>
    /// <param name="index">Register index, 0-15.</param>
    /// <param name="value">Value to write.</param>
    public void WriteRegister(int index, uint value)
    {
        _state[index] = value;
    }

    /// <summary>Writes the CPU save-state section payload.</summary>
    /// <param name="writer">Destination writer.</param>
    public void SaveState(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        for (var i = 0; i < Arm60State.RegisterCount; i++)
        {
            writer.WriteUInt32(_state.User[i]);
        }

        for (var i = 0; i < _state.Cash.Length; i++)
        {
            writer.WriteUInt32(_state.Cash[i]);
        }

        WriteRegisters(writer, _state.Svc);
        WriteRegisters(writer, _state.Abt);
        WriteRegisters(writer, _state.Fiq);
        WriteRegisters(writer, _state.Irq);
        WriteRegisters(writer, _state.Und);
        WriteRegisters(writer, _state.Spsr);

        writer.WriteUInt32(_state.Cpsr);
        writer.WriteBoolean(_state.FiqPending);
        writer.WriteBoolean(_state.SecondRom);
        writer.WriteBoolean(_state.DataAbortPending);
        writer.WriteUInt32(_carryOut);
        writer.WriteInt32(_ldmAddress);
        writer.WriteInt32(_ldmValue);
        writer.WriteInt32(_ldmInUse);
    }

    /// <summary>Reads the CPU save-state section payload.</summary>
    /// <param name="reader">Source reader.</param>
    public void LoadState(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        for (var i = 0; i < Arm60State.RegisterCount; i++)
        {
            _state.User[i] = reader.ReadUInt32();
        }

        for (var i = 0; i < _state.Cash.Length; i++)
        {
            _state.Cash[i] = reader.ReadUInt32();
        }

        ReadRegisters(reader, _state.Svc);
        ReadRegisters(reader, _state.Abt);
        ReadRegisters(reader, _state.Fiq);
        ReadRegisters(reader, _state.Irq);
        ReadRegisters(reader, _state.Und);
        ReadRegisters(reader, _state.Spsr);

        _state.Cpsr = reader.ReadUInt32();
        _state.FiqPending = reader.ReadBoolean();
        _state.SecondRom = reader.ReadBoolean();
        _state.DataAbortPending = reader.ReadBoolean();
        _carryOut = reader.ReadUInt32();
        _ldmAddress = reader.ReadInt32();
        _ldmValue = reader.ReadInt32();
        _ldmInUse = reader.ReadInt32();
        _cycles = 0;
        _instructionsExecuted = 0;
        _swiInstructions = 0;
        _swiHleInstructions = 0;
        _lastSwiComment = 0;
        _swiCommentCounts.Clear();
        ResetCycleClassUsage();
        ClearJitCache();
    }

    /// <summary>Writes the CPU save-state section payload. FreeDO: <c>_arm_Save</c>.</summary>
    /// <param name="writer">Destination writer.</param>
    public void Save(IStateWriter writer)
    {
        SaveState(writer);
    }

    /// <summary>Reads the CPU save-state section payload. FreeDO: <c>_arm_Load</c>.</summary>
    /// <param name="reader">Source reader.</param>
    public void Load(IStateReader reader)
    {
        LoadState(reader);
    }

    private static void WriteRegisters(IStateWriter writer, uint[] registers)
    {
        for (var i = 0; i < registers.Length; i++)
        {
            writer.WriteUInt32(registers[i]);
        }
    }

    private static void ReadRegisters(IStateReader reader, uint[] registers)
    {
        for (var i = 0; i < registers.Length; i++)
        {
            registers[i] = reader.ReadUInt32();
        }
    }

    private void ExecuteInstruction(uint instruction)
    {
        switch ((instruction >> 24) & 0xf)
        {
            case 0x0:
                if (TryMultiply(instruction))
                {
                    break;
                }

                goto case 0x1;
            case 0x1:
                if (TrySwap(instruction))
                {
                    break;
                }

                goto case 0x2;
            case 0x2:
            case 0x3:
                if ((instruction & ArmDecoder.DataProcessingMask) != 0x90)
                {
                    ExecuteDataProcessing(instruction);
                    break;
                }

                goto case 0x6;
            case 0x6:
            case 0x7:
                if ((instruction & ArmDecoder.UndefinedMask) == ArmDecoder.UndefinedSignature)
                {
                    EnterUndefined();
                    break;
                }

                goto case 0x4;
            case 0x4:
            case 0x5:
                if ((instruction & ArmDecoder.SingleDataTransferMask) != ArmDecoder.SingleDataTransferMask)
                {
                    ExecuteSingleDataTransfer(instruction);
                    break;
                }

                // FreeDOCore loops back to the Undefine label for this malformed encoding.
                // Trap it as an undefined instruction instead of hanging the emulator.
                EnterUndefined();
                break;
            case 0x8:
            case 0x9:
                ExecuteBlockTransfer(instruction);
                break;
            case 0xa:
            case 0xb:
                ExecuteBranch(instruction);
                break;
            case 0xf:
                EnterSwi(instruction);
                break;
            default:
                EnterUndefined();
                break;
        }
    }

    private bool TryMultiply(uint instruction)
    {
        if ((instruction & ArmDecoder.MultiplyMask) != ArmDecoder.MultiplySignature)
        {
            return false;
        }

        Arm60State state = _state;
        var cycles = ((CalcBits(state.User[(int)((instruction >> 8) & 0xf)]) + 5) >> 1) - 1;
        _cycles -= cycles > 16 ? 16 : (int)cycles;

        uint result;
        var destination = (int)((instruction >> 16) & 0xf);
        if (destination == (int)(instruction & 0xf))
        {
            if ((instruction & (1u << 21)) != 0)
            {
                state.ProgramCounter += 8;
                result = state.User[(int)((instruction >> 12) & 0xf)];
                state.ProgramCounter -= 8;
            }
            else
            {
                result = 0;
            }
        }
        else
        {
            if ((instruction & (1u << 21)) != 0)
            {
                result = unchecked(state.User[(int)(instruction & 0xf)] * state.User[(int)((instruction >> 8) & 0xf)]);
                state.ProgramCounter += 8;
                result = unchecked(result + state.User[(int)((instruction >> 12) & 0xf)]);
                state.ProgramCounter -= 8;
            }
            else
            {
                result = unchecked(state.User[(int)(instruction & 0xf)] * state.User[(int)((instruction >> 8) & 0xf)]);
            }
        }

        if ((instruction & (1u << 20)) != 0)
        {
            ArmAlu.SetZn(state, result);
        }

        state.User[destination] = result;
        return true;
    }

    private bool TrySwap(uint instruction)
    {
        if ((instruction & ArmDecoder.SwapMask) != ArmDecoder.SwapSignature)
        {
            return false;
        }

        Arm60State state = _state;
        state.ProgramCounter += 4;
        var address = state.User[(int)((instruction >> 16) & 0xf)];
        state.ProgramCounter += 4;

        if ((instruction & (1u << 22)) != 0)
        {
            var temporary = _bus.Read8(address);
            WriteByte(address, state.User[(int)(instruction & 0xf)]);
            state.ProgramCounter -= 8;
            state.User[(int)((instruction >> 12) & 0xf)] = temporary;
        }
        else
        {
            var temporary = _bus.Read32(address);
            WriteWord(address, state.User[(int)(instruction & 0xf)]);
            state.ProgramCounter -= 8;
            if ((address & 3) != 0)
            {
                temporary = ArmBarrelShifter.RotateRight(temporary, (int)(address & 3) * 8);
            }

            state.User[(int)((instruction >> 12) & 0xf)] = temporary;
        }

        _cycles -= (2 * _nCycle) + Icycle;
        return true;
    }

    private void ExecuteDataProcessing(uint instruction)
    {
        Arm60State state = _state;
        var programCounter = state.ProgramCounter;
        state.ProgramCounter += 4;

        uint op1;
        uint op2;
        if ((instruction & (1u << 25)) != 0)
        {
            // Opera: ARM_SHIFT_NSC(op2, rotate, ARM_SHIFT_TYPE_ROR). A rotate of
            // zero preserves the old carry; a non-zero rotate produces bit 31 of
            // the rotated result. The old FreeDO path rotated the value but left
            // the shifter carry stale, so MOVS/TSTS with a rotated immediate could
            // set CPSR.C from an unrelated previous instruction.
            var immediate = instruction & 0xff;
            var rotate = (int)((instruction >> 7) & 0x1e);
            op2 = rotate == 0
                ? immediate
                : ArmBarrelShifter.RotateRight(immediate, rotate);
            _carryOut = rotate == 0
                ? (state.Cpsr >> 29) & 1u
                : (immediate >> (rotate - 1)) & 1u;
            op1 = state.User[(int)((instruction >> 16) & 0xf)];
        }
        else if ((instruction & 0x0ff0u) == 0)
        {
            // LSL #0: the most common register form. Skip the barrel-shifter call;
            // the carry-out is the old carry.
            op2 = state.User[(int)(instruction & 0xf)];
            op1 = state.User[(int)((instruction >> 16) & 0xf)];
            _carryOut = (state.Cpsr >> 29) & 1u;
        }
        else
        {
            var shiftType = (byte)((instruction >> 5) & 3);
            byte shift;
            if ((instruction & (1u << 4)) != 0)
            {
                var shiftRegister = (int)((instruction >> 8) & 0xf);
                shift = (byte)(state.User[shiftRegister] & 0xff);
                state.ProgramCounter += 4;
                op2 = state.User[(int)(instruction & 0xf)];
                op1 = state.User[(int)((instruction >> 16) & 0xf)];
                _cycles -= Icycle;
            }
            else
            {
                shift = (byte)((instruction >> 7) & 0x1f);
                if (shift == 0)
                {
                    if (shiftType != 0)
                    {
                        if (shiftType == 3)
                        {
                            shiftType++;
                        }
                        else
                        {
                            shift = 32;
                        }
                    }
                }

                op2 = state.User[(int)(instruction & 0xf)];
                op1 = state.User[(int)((instruction >> 16) & 0xf)];
            }

            op2 = ArmBarrelShifter.ShiftNoCarry(state, op2, shift, shiftType, out _carryOut);
        }

        state.ProgramCounter = programCounter;

        var opcode = (instruction >> 20) & 0x1f;
        if ((opcode & 1) == 0 && opcode is not (16 or 18 or 20 or 22))
        {
            // Flag-preserving operations are the overwhelmingly common case. Apply
            // them directly instead of calling the generic ARM_ALU dispatch.
            var carry = (state.Cpsr >> 29) & 1u;
            uint result = opcode switch
            {
                0 => op1 & op2,
                2 => op1 ^ op2,
                4 => unchecked(op1 - op2),
                6 => unchecked(op2 - op1),
                8 => unchecked(op1 + op2),
                10 => unchecked(op1 + op2 + carry),
                12 => unchecked(op1 - op2 - (carry ^ 1u)),
                14 => unchecked(op2 - op1 - (carry ^ 1u)),
                24 => op1 | op2,
                26 => op2,
                28 => op1 & ~op2,
                30 => ~op2,
                _ => 0u,
            };

            var destination = (int)((instruction >> 12) & 0xf);
            state.User[destination] = result;
            if (destination == 15)
            {
                _cycles -= Icycle + _nCycle;
            }

            return;
        }

        if ((instruction & (1u << 20)) != 0 && IsLogic[(int)((instruction >> 21) & 0xf)])
        {
            state.SetC(_carryOut);
        }

        if (ArmAlu.Execute(state, instruction, opcode, op1, op2))
        {
            return;
        }

        if (((instruction >> 12) & 0xf) == 0xf)
        {
            if ((instruction & (1u << 20)) != 0)
            {
                state.SetCpsr(state.CurrentSpsr);
            }

            _cycles -= Icycle + _nCycle;
        }
    }

    private void ExecuteSingleDataTransfer(uint instruction)
    {
        Arm60State state = _state;
        var programCounter = state.ProgramCounter;
        state.ProgramCounter += 4;

        uint offset;
        if ((instruction & (1u << 25)) != 0)
        {
            var shiftType = (byte)((instruction >> 5) & 3);
            byte shift;
            if ((instruction & (1u << 4)) != 0)
            {
                var shiftRegister = (int)((instruction >> 8) & 0xf);
                shift = (byte)(state.User[shiftRegister] & 0xff);
                state.ProgramCounter += 4;
            }
            else
            {
                shift = (byte)((instruction >> 7) & 0x1f);
                if (shift == 0)
                {
                    if (shiftType != 0)
                    {
                        if (shiftType == 3)
                        {
                            shiftType++;
                        }
                        else
                        {
                            shift = 32;
                        }
                    }
                }
            }

            offset = ArmBarrelShifter.ShiftNoCarry(
                state,
                state.User[(int)(instruction & 0xf)],
                shift,
                shiftType,
                out _carryOut);
        }
        else
        {
            offset = instruction & 0xfff;
        }

        var transferBase = state.User[(int)((instruction >> 16) & 0xf)];
        var baseAddress = transferBase;
        if ((instruction & (1u << 23)) == 0)
        {
            offset = unchecked(0u - offset);
        }

        if ((instruction & (1u << 24)) != 0)
        {
            transferBase = unchecked(transferBase + offset);
            baseAddress = transferBase;
        }
        else
        {
            baseAddress = unchecked(baseAddress + offset);
        }

        var destination = (int)((instruction >> 12) & 0xf);
        if ((instruction & (1u << 20)) != 0)
        {
            uint value;
            if ((instruction & (1u << 22)) != 0)
            {
                value = ReadByte(transferBase);
            }
            else
            {
                value = _bus.Read32(transferBase);
                var rotate = transferBase & 3;
                if (rotate != 0)
                {
                    value = ArmBarrelShifter.RotateRight(value, (int)rotate * 8);
                }
            }

            if (destination == 15)
            {
                _cycles -= Scycle + _nCycle;
            }

            _cycles -= _nCycle + Icycle;
            state.ProgramCounter = programCounter;

            if ((instruction & (1u << 21)) != 0 || (instruction & (1u << 24)) == 0)
            {
                state.Load((int)((instruction >> 16) & 0xf), baseAddress);
            }

            if ((instruction & (1u << 21)) != 0 && (instruction & (1u << 24)) == 0)
            {
                state.LoadUser(destination, value);
            }
            else
            {
                state.Load(destination, value);
            }
        }
        else
        {
            uint value;
            if ((instruction & (1u << 21)) != 0 && (instruction & (1u << 24)) == 0)
            {
                value = state.ReadUser(destination);
            }
            else
            {
                value = state.User[destination];
            }

            state.ProgramCounter = programCounter;
            _cycles -= -Scycle + (2 * _nCycle);

            if ((instruction & (1u << 22)) != 0)
            {
                WriteByte(transferBase, value);
            }
            else
            {
                WriteWord(transferBase, value);
            }

            if ((instruction & (1u << 21)) != 0 || (instruction & (1u << 24)) == 0)
            {
                state.Load((int)((instruction >> 16) & 0xf), baseAddress);
            }
        }
    }

    private void ExecuteBlockTransfer(uint instruction)
    {
        Arm60State state = _state;
        var register = (int)((instruction >> 16) & 0xf);
        var baseAddress = register == 15 ? state.User[15] + 8 : state.User[register];

        if ((instruction & (1u << 20)) != 0)
        {
            if ((instruction & 0x8000) != 0)
            {
                _cycles -= Scycle + _nCycle;
            }

            LoadMultiple(instruction, baseAddress, register);
        }
        else
        {
            StoreMultiple(instruction, baseAddress, register);
        }
    }

    private void LoadMultiple(uint instruction, uint baseAddress, int register)
    {
        Arm60State state = _state;
        var list = instruction & 0xffff;
        var count = CountBits(list);
        uint basePointer;
        switch ((instruction >> 23) & 3)
        {
            case 0:
                baseAddress = unchecked(baseAddress - ((uint)count << 2));
                basePointer = baseAddress + 4;
                break;
            case 1:
                basePointer = baseAddress;
                baseAddress = unchecked(baseAddress + ((uint)count << 2));
                break;
            case 2:
                baseAddress = unchecked(baseAddress - ((uint)count << 2));
                basePointer = baseAddress;
                break;
            default:
                basePointer = baseAddress + 4;
                baseAddress = unchecked(baseAddress + ((uint)count << 2));
                break;
        }

        if ((instruction & (1u << 22)) != 0 && (instruction & 0x8000) == 0)
        {
            if ((instruction & (1u << 21)) != 0)
            {
                state.LoadUser(register, baseAddress);
            }

            while (list != 0)
            {
                var index = BitOperations.TrailingZeroCount(list);
                list &= list - 1;
                var value = ReadWord(basePointer);
                state.LoadUser(index, value);
                basePointer += 4;
            }
        }
        else
        {
            if ((instruction & (1u << 21)) != 0)
            {
                state.Load(register, baseAddress);
            }

            while (list != 0)
            {
                var index = BitOperations.TrailingZeroCount(list);
                list &= list - 1;
                var value = ReadWord(basePointer);
                if (value == 0xF1000 &&
                    index == 1 &&
                    state.User[2] != 0xF0000 &&
                    !_compatibility.Cnbfix &&
                    (_compatibility.FixMode & FixMode.BitTiming1) != FixMode.None)
                {
                    value += 0x1000;
                }

                if (_ldmInUse == 1 && (basePointer & 0x1FFFFF) != 0)
                {
                    if (basePointer == unchecked((uint)_ldmAddress))
                    {
                        _ldmInUse = 0;
                    }

                    if (value != (uint)_ldmValue &&
                        value == 0xEFE54 &&
                        index == 4 &&
                        !_compatibility.Cnbfix &&
                        (_compatibility.FixMode & FixMode.BitTiming1) != FixMode.None)
                    {
                        value -= 0xF;
                    }
                }

                state.User[index] = value;
                basePointer += 4;
            }

            if ((instruction & (1u << 22)) != 0)
            {
                var mode = ArmModes.Table[state.Cpsr & 0x1f];
                if (mode != ArmModes.User && mode < state.Spsr.Length)
                {
                    state.SetCpsr(state.Spsr[mode]);
                }
            }
        }

        _cycles -= ((count - 1) * Scycle) + _nCycle + Icycle;
    }

    private void StoreMultiple(uint instruction, uint baseAddress, int register)
    {
        Arm60State state = _state;
        var count = CountBits(instruction & 0xffff);
        var list = instruction & 0x7fff;
        uint basePointer;
        switch ((instruction >> 23) & 3)
        {
            case 0:
                baseAddress = unchecked(baseAddress - ((uint)count << 2));
                basePointer = baseAddress + 4;
                break;
            case 1:
                basePointer = baseAddress;
                baseAddress = unchecked(baseAddress + ((uint)count << 2));
                break;
            case 2:
                baseAddress = unchecked(baseAddress - ((uint)count << 2));
                basePointer = baseAddress;
                break;
            default:
                basePointer = baseAddress + 4;
                baseAddress = unchecked(baseAddress + ((uint)count << 2));
                break;
        }

        var writebackMask = (1u << register) - 1;
        if ((instruction & (1u << 22)) != 0)
        {
            if ((instruction & (1u << 21)) != 0 && (instruction & writebackMask) != 0)
            {
                state.LoadUser(register, baseAddress);
            }

            while (list != 0)
            {
                var index = BitOperations.TrailingZeroCount(list);
                list &= list - 1;
                WriteWord(basePointer, state.ReadUser(index));
                basePointer += 4;
            }

            if ((instruction & (1u << 21)) != 0)
            {
                state.LoadUser(register, baseAddress);
            }
        }
        else
        {
            if ((instruction & (1u << 21)) != 0 && (instruction & writebackMask) != 0)
            {
                state.Load(register, baseAddress);
            }

            while (list != 0)
            {
                var index = BitOperations.TrailingZeroCount(list);
                list &= list - 1;
                var value = state.User[index];
                WriteWord(basePointer, value);
                if ((basePointer & 0x1FFFFF) != 0)
                {
                    _ldmAddress = unchecked((int)basePointer);
                    _ldmValue = unchecked((int)value);
                    _ldmInUse = 1;
                }

                basePointer += 4;
            }

            if ((instruction & (1u << 21)) != 0)
            {
                state.Load(register, baseAddress);
            }
        }

        if ((instruction & 0x8000) != 0)
        {
            WriteWord(basePointer, state.User[15] + 8);
        }

        _cycles -= ((count - 2) * Scycle) + _nCycle + _nCycle;
    }

    private void ExecuteBranch(uint instruction)
    {
        Arm60State state = _state;
        if ((instruction & (1u << 24)) != 0)
        {
            state.Load(14, state.ProgramCounter);
        }

        var offset = (instruction & 0xffffff) | ((instruction & 0x800000) != 0 ? 0xff000000u : 0u);
        state.ProgramCounter = unchecked(state.ProgramCounter + (offset << 2) + 4);
        _cycles -= Scycle + _nCycle;
    }

    private void EnterSwi(uint instruction)
    {
        _cycles -= Scycle + _nCycle;
        _swiInstructions++;
        _lastSwiComment = instruction & 0x00FFFFFF;
        if (SwiCommentTelemetryEnabled)
        {
            _swiCommentCounts.TryGetValue(_lastSwiComment, out var commentCount);
            _swiCommentCounts[_lastSwiComment] = commentCount + 1;
        }

        // Opera subtracts the SWI cycles before attempting HLE and leaves the next PC in
        // place when the call is handled. Unknown comments fall through to the guest vector.
        if (SwiHleEnabled
            && _ram is not null
            && ArmSwiHle.TryExecute(instruction, _state.User, _ram))
        {
            _swiHleInstructions++;
            return;
        }

        Arm60State state = _state;
        state.Spsr[ArmModes.Table[0x13]] = state.Cpsr;
        state.SetI(true);
        state.SetMode(0x13);
        state.Load(14, state.ProgramCounter);
        state.ProgramCounter = 0x00000008;
    }

    private void EnterUndefined()
    {
        Arm60State state = _state;
        state.Spsr[ArmModes.Table[0x1b]] = state.Cpsr;
        state.SetI(true);
        state.SetMode(0x1b);
        state.Load(14, state.ProgramCounter);
        state.ProgramCounter = 0x00000004;
        _cycles -= Scycle + _nCycle;
    }

    private void EnterFiq()
    {
        Arm60State state = _state;
        state.FiqPending = false;
        state.Spsr[ArmModes.Table[0x11]] = state.Cpsr;
        state.SetF(true);
        state.SetI(true);
        state.SetMode(0x11);
        state.Load(14, state.ProgramCounter + 4);
        state.ProgramCounter = 0x0000001c;
        FiqEntered?.Invoke(_cyclesExecuted + (-_cycles), state.User[14]);
    }

    private uint ReadWord(uint address)
    {
        return _bus.Read32(address);
    }

    private byte ReadByte(uint address)
    {
        return _bus.Read8(address);
    }

    private void WriteWord(uint address, uint value)
    {
        _bus.Write32(address, value);
        if (_pcAdjustSource is not null && _pcAdjustSource.ConsumePcAdjustment())
        {
            _state.ProgramCounter += 4;
        }
    }

    private void WriteByte(uint address, uint value)
    {
        _bus.Write8(address, (byte)value);
    }

    private static int CountBits(uint value)
    {
        return BitOperations.PopCount(value);
    }

    private static uint CalcBits(uint number)
    {
        if (number == 0)
        {
            return 1;
        }

        uint result;
        if ((number >> 16) != 0)
        {
            number >>= 16;
            result = 16;
        }
        else
        {
            result = 0;
        }

        if ((number >> 8) != 0)
        {
            number >>= 8;
            result += 8;
        }

        if ((number >> 4) != 0)
        {
            number >>= 4;
            result += 4;
        }

        if ((number >> 2) != 0)
        {
            number >>= 2;
            result += 2;
        }

        if ((number >> 1) != 0)
        {
            result += 2;
        }
        else if (number != 0)
        {
            result++;
        }

        return result;
    }
}
