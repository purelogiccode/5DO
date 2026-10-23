using System;
using ThreeDo.Core;

namespace ThreeDo.Cpu;

/// <summary>
/// The ARM60 interpreter, ported from FreeDOCore's <c>arm.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// The interpreter executes exactly one instruction per <see cref="Step"/> call and returns
/// the same cycle count as <c>_arm_Execute</c>. It preserves the reference's decode order,
/// including the C++ switch fallthrough, the banked-register model, the CLIO PC-adjust
/// quirk, the Anvil/Crash 'n Burn compatibility hacks, and the immediate data-processing
/// carry-out behaviour.
/// </para>
/// <para>
/// All mutable state is instance state; the CPU can be used by independent machines in
/// parallel. The bus is the only external dependency.
/// </para>
/// </remarks>
public sealed class Arm60 : ICpuBackend, IStateSection
{
    private const int Ncycle = 4;
    private const int Scycle = 1;
    private const int Icycle = 1;

    private static readonly bool[] IsLogic =
    {
        true, true, false, false,
        false, false, false, false,
        true, true, false, false,
        true, true, true, true,
    };

    private readonly IAddressBus _bus;
    private readonly IInterruptSink? _interrupts;
    private readonly EmulatorTuning _tuning;
    private readonly CompatibilityState _compatibility;
    private readonly Arm60State _state = new();

    private int _cycles;
    private uint _carryOut;
    private long _instructionsExecuted;
    private int _ldmAddress;
    private int _ldmValue;
    private int _ldmInUse;

    /// <summary>Initializes a new instance of the <see cref="Arm60"/> class.</summary>
    /// <param name="bus">CPU address bus.</param>
    /// <param name="interrupts">Optional FIQ source. May be null in unit tests.</param>
    /// <param name="tuning">Machine tuning. Defaults to <see cref="EmulatorTuning.Default"/>.</param>
    /// <param name="compatibility">Optional shared compatibility state.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bus"/> is null.</exception>
    public Arm60(
        IAddressBus bus,
        IInterruptSink? interrupts = null,
        EmulatorTuning? tuning = null,
        CompatibilityState? compatibility = null)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        _interrupts = interrupts;
        _tuning = tuning ?? EmulatorTuning.Default;
        _tuning.Validate();
        _compatibility = compatibility ?? new CompatibilityState { FixMode = _tuning.FixMode };
        Reset();
    }

    /// <summary>Gets the architectural state.</summary>
    public Arm60State State => _state;

    /// <summary>Gets the machine tuning.</summary>
    public EmulatorTuning Tuning => _tuning;

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
    public void SelectRom(int bank) => _state.SecondRom = bank > 0;

    /// <summary>Resets the CPU to the FreeDOCore power-on state.</summary>
    public void Reset()
    {
        _state.Reset();
        _cycles = 0;
        _carryOut = 0;
        _instructionsExecuted = 0;
        _ldmAddress = 0;
        _ldmValue = 0;
        _ldmInUse = 0;
    }

    /// <summary>Executes exactly one instruction.</summary>
    /// <returns>The ARM clock cycles consumed by the instruction.</returns>
    public int Step()
    {
        var state = _state;
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
            (_compatibility.FixMode & FixMode.BitTiming1) != 0)
        {
            state.ProgramCounter = 0x9E9CC;
            _compatibility.Cnbfix = true;
        }

        var instruction = _bus.Read32(state.ProgramCounter);
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

        return -_cycles;
    }

    /// <summary>Reads a user-visible register (R0-R15).</summary>
    /// <param name="index">Register index, 0-15.</param>
    /// <returns>The register value.</returns>
    public uint ReadRegister(int index) => _state[index];

    /// <summary>Writes a user-visible register (R0-R15).</summary>
    /// <param name="index">Register index, 0-15.</param>
    /// <param name="value">Value to write.</param>
    public void WriteRegister(int index, uint value) => _state[index] = value;

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
    }

    /// <summary>Writes the CPU save-state section payload. FreeDO: <c>_arm_Save</c>.</summary>
    /// <param name="writer">Destination writer.</param>
    public void Save(IStateWriter writer) => SaveState(writer);

    /// <summary>Reads the CPU save-state section payload. FreeDO: <c>_arm_Load</c>.</summary>
    /// <param name="reader">Source reader.</param>
    public void Load(IStateReader reader) => LoadState(reader);

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
                EnterSwi();
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

        var state = _state;
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

        var state = _state;
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

        _cycles -= (2 * Ncycle) + Icycle;
        return true;
    }

    private void ExecuteDataProcessing(uint instruction)
    {
        var state = _state;
        var programCounter = state.ProgramCounter;
        state.ProgramCounter += 4;

        uint op1;
        uint op2;
        if ((instruction & (1u << 25)) != 0)
        {
            op2 = instruction & 0xff;
            var rotate = (int)((instruction >> 7) & 0x1e);
            if (rotate != 0)
            {
                op2 = ArmBarrelShifter.RotateRight(op2, rotate);
            }

            op1 = state.User[(int)((instruction >> 16) & 0xf)];
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

        if ((instruction & (1u << 20)) != 0 && IsLogic[(int)((instruction >> 21) & 0xf)])
        {
            state.SetC(_carryOut);
        }

        if (ArmAlu.Execute(state, instruction, (instruction >> 20) & 0x1f, op1, op2))
        {
            return;
        }

        if (((instruction >> 12) & 0xf) == 0xf)
        {
            if ((instruction & (1u << 20)) != 0)
            {
                state.SetCpsr(state.CurrentSpsr);
            }

            _cycles -= Icycle + Ncycle;
        }
    }

    private void ExecuteSingleDataTransfer(uint instruction)
    {
        var state = _state;
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
                value = _bus.Read8(transferBase);
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
                _cycles -= Scycle + Ncycle;
            }

            _cycles -= Ncycle + Icycle;
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
            _cycles -= -Scycle + (2 * Ncycle);

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
        var state = _state;
        var register = (int)((instruction >> 16) & 0xf);
        var baseAddress = register == 15 ? state.User[15] + 8 : state.User[register];

        if ((instruction & (1u << 20)) != 0)
        {
            if ((instruction & 0x8000) != 0)
            {
                _cycles -= Scycle + Ncycle;
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
        var state = _state;
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

            uint index = 0;
            while (list != 0)
            {
                if ((list & 1) != 0)
                {
                    var value = ReadWord(basePointer);
                    state.LoadUser((int)index, value);
                    basePointer += 4;
                }

                index++;
                list >>= 1;
            }
        }
        else
        {
            if ((instruction & (1u << 21)) != 0)
            {
                state.Load(register, baseAddress);
            }

            uint index = 0;
            while (list != 0)
            {
                if ((list & 1) != 0)
                {
                    var value = ReadWord(basePointer);
                    if (value == 0xF1000 &&
                        index == 1 &&
                        state.User[2] != 0xF0000 &&
                        !_compatibility.Cnbfix &&
                        (_compatibility.FixMode & FixMode.BitTiming1) != 0)
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
                            (_compatibility.FixMode & FixMode.BitTiming1) != 0)
                        {
                            value -= 0xF;
                        }
                    }

                    state.User[(int)index] = value;
                    basePointer += 4;
                }

                index++;
                list >>= 1;
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

        _cycles -= ((count - 1) * Scycle) + Ncycle + Icycle;
    }

    private void StoreMultiple(uint instruction, uint baseAddress, int register)
    {
        var state = _state;
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

            uint index = 0;
            while (list != 0)
            {
                if ((list & 1) != 0)
                {
                    WriteWord(basePointer, state.ReadUser((int)index));
                    basePointer += 4;
                }

                index++;
                list >>= 1;
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

            uint index = 0;
            while (list != 0)
            {
                if ((list & 1) != 0)
                {
                    var value = state.User[(int)index];
                    WriteWord(basePointer, value);
                    if ((basePointer & 0x1FFFFF) != 0)
                    {
                        _ldmAddress = unchecked((int)basePointer);
                        _ldmValue = unchecked((int)value);
                        _ldmInUse = 1;
                    }

                    basePointer += 4;
                }

                index++;
                list >>= 1;
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

        _cycles -= ((count - 2) * Scycle) + Ncycle + Ncycle;
    }

    private void ExecuteBranch(uint instruction)
    {
        var state = _state;
        if ((instruction & (1u << 24)) != 0)
        {
            state.Load(14, state.ProgramCounter);
        }

        var offset = (instruction & 0xffffff) | ((instruction & 0x800000) != 0 ? 0xff000000u : 0u);
        state.ProgramCounter = unchecked(state.ProgramCounter + (offset << 2) + 4);
        _cycles -= Scycle + Ncycle;
    }

    private void EnterSwi()
    {
        var state = _state;
        state.Spsr[ArmModes.Table[0x13]] = state.Cpsr;
        state.SetI(true);
        state.SetMode(0x13);
        state.Load(14, state.ProgramCounter);
        state.ProgramCounter = 0x00000008;
        _cycles -= Scycle + Ncycle;
    }

    private void EnterUndefined()
    {
        var state = _state;
        state.Spsr[ArmModes.Table[0x1b]] = state.Cpsr;
        state.SetI(true);
        state.SetMode(0x1b);
        state.Load(14, state.ProgramCounter);
        state.ProgramCounter = 0x00000004;
        _cycles -= Scycle + Ncycle;
    }

    private void EnterFiq()
    {
        var state = _state;
        state.FiqPending = false;
        state.Spsr[ArmModes.Table[0x11]] = state.Cpsr;
        state.SetF(true);
        state.SetI(true);
        state.SetMode(0x11);
        state.Load(14, state.ProgramCounter + 4);
        state.ProgramCounter = 0x0000001c;
    }

    private uint ReadWord(uint address) => _bus.Read32(address);

    private void WriteWord(uint address, uint value)
    {
        _bus.Write32(address, value);
        if (_bus is IPcAdjustSource source && source.ConsumePcAdjustment())
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
        value = (value & 0x5555u) + ((value >> 1) & 0x5555u);
        value = (value & 0x3333u) + ((value >> 2) & 0x3333u);
        value = (value & 0xffu) + (value >> 8);
        value = (value & 0xfu) + (value >> 4);
        return (int)value;
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
            number >>= 1;
            result += 2;
        }
        else if (number != 0)
        {
            result++;
        }

        return result;
    }
}
