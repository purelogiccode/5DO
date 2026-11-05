using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using FiveDo.Core;

namespace FiveDo.Cpu;

/// <summary>
/// Managed IL JIT for the ARM60 interpreter.
/// </summary>
/// <remarks>
/// <para>
/// The JIT compiles straight-line basic blocks into <see cref="DynamicMethod"/> delegates.
/// It is an optional execution strategy for <see cref="CpuBackendKind.Jit"/>; the
/// interpreter remains the accuracy oracle. Every compiled block is verified against the
/// current instruction words before it runs, stores terminate a block, and unsupported
/// instructions fall back to <see cref="Step"/>. Those rules keep self-modifying code,
/// memory-mapped I/O, SWIs, mode changes, and multi-register transfers on the reference
/// path.
/// </para>
/// <para>
/// The JIT uses the same architectural state, cycle counters, FIQ path, and save-state
/// sections as the interpreter. The block cache is host-only and is never serialized.
/// </para>
/// </remarks>
public sealed partial class Arm60
{
    private const int JitMaxBlockInstructions = 16;
    private const int JitMaxCachedBlocks = 65536;
    private const int JitCompileThreshold = 8192;
    private const int JitStaleBlock = int.MinValue;

    private static readonly FieldInfo JitStateField =
        typeof(Arm60).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo JitUserField =
        typeof(Arm60State).GetField("User", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo JitInterruptsField =
        typeof(Arm60).GetField("_interrupts", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo JitCyclesField =
        typeof(Arm60).GetField("_cycles", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo JitCyclesExecutedField =
        typeof(Arm60).GetField("_cyclesExecuted", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo JitInstructionsField =
        typeof(Arm60).GetField("_instructionsExecuted", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo JitCpsrGet =
        typeof(Arm60State).GetProperty(nameof(Arm60State.Cpsr))!.GetGetMethod()!;

    private static readonly MethodInfo JitFiqPendingGet =
        typeof(Arm60State).GetProperty(nameof(Arm60State.FiqPending))!.GetGetMethod()!;

    private static readonly MethodInfo JitNeedFiqGet =
        typeof(IInterruptSink).GetProperty(nameof(IInterruptSink.NeedFiq))!.GetGetMethod()!;

    private static readonly MethodInfo JitConditionPassed =
        typeof(ArmDecoder).GetMethod(
            nameof(ArmDecoder.ConditionPassed),
            BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo JitGetCarry =
        typeof(ArmAlu).GetMethod("GetCarry", BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo JitSetC =
        typeof(Arm60State).GetMethod(
            "SetC",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            [typeof(bool)],
            null)!;

    private static readonly MethodInfo JitSetZn =
        typeof(ArmAlu).GetMethod("SetZn", BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo JitSetCv =
        typeof(ArmAlu).GetMethod("SetCv", BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo JitSetCvSub =
        typeof(ArmAlu).GetMethod("SetCvSub", BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo JitRotateRight =
        typeof(ArmBarrelShifter).GetMethod("RotateRight", BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo JitLoadUser =
        typeof(Arm60State).GetMethod("LoadUser", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo JitReadUser =
        typeof(Arm60State).GetMethod("ReadUser", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo JitEnterFiq =
        typeof(Arm60).GetMethod("EnterFiq", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo JitReadWord =
        typeof(Arm60).GetMethod("ReadWord", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo JitReadByte =
        typeof(Arm60).GetMethod("ReadByte", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo JitWriteWord =
        typeof(Arm60).GetMethod("WriteWord", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo JitWriteByte =
        typeof(Arm60).GetMethod("WriteByte", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly Dictionary<uint, JitBlock> _jitBlocks = [];
    private readonly Dictionary<uint, int> _jitHits = [];
    private readonly HashSet<uint> _jitBadBlocks = [];
    private readonly JitBlock?[] _jitFastCache = new JitBlock?[4096];
    private bool _jitCompileFailed;
    private long _jitBlocksCompiled;
    private long _jitBlocksExecuted;
    private long _jitCompileTicks;
    private long _jitCompiledDp;
    private long _jitCompiledLoad;
    private long _jitCompiledStore;
    private long _jitCompiledBranch;
    private string? _jitLastFailure;

    private delegate int JitBlockDelegate(Arm60 cpu, int remainingBatch);

    private enum JitInstructionKind
    {
        DataProcessing,
        Load,
        Store,
        Branch,
    }

    /// <summary>Gets the number of cached JIT blocks.</summary>
    public int JitBlockCount => _jitBlocks.Count;

    /// <summary>Gets the number of blocks compiled since the last cache clear.</summary>
    public long JitBlocksCompiled => _jitBlocksCompiled;

    /// <summary>Gets the number of block invocations since the last cache clear.</summary>
    public long JitBlocksExecuted => _jitBlocksExecuted;

    /// <summary>Gets a value indicating whether compilation failed and the CPU fell back to the interpreter.</summary>
    public bool JitCompileFailed => _jitCompileFailed;

    /// <summary>Gets the last JIT compilation or invocation failure, or <see langword="null"/>.</summary>
    public string? JitLastFailure => _jitLastFailure;

    /// <summary>Gets the wall-clock time spent compiling JIT blocks.</summary>
    public TimeSpan JitCompileTime => Stopwatch.GetElapsedTime(0, _jitCompileTicks);

    /// <summary>Gets the number of data-processing instructions in compiled blocks.</summary>
    public long JitCompiledDataProcessing => _jitCompiledDp;

    /// <summary>Gets the number of load instructions in compiled blocks.</summary>
    public long JitCompiledLoads => _jitCompiledLoad;

    /// <summary>Gets the number of store instructions in compiled blocks.</summary>
    public long JitCompiledStores => _jitCompiledStore;

    /// <summary>Gets the number of branch instructions in compiled blocks.</summary>
    public long JitCompiledBranches => _jitCompiledBranch;

    /// <summary>
    /// Runs one JIT block, or one interpreter instruction when the block cannot be compiled.
    /// </summary>
    /// <param name="cyclesSinceEvent">
    /// Cycles already accumulated in the current 16-cycle frame-loop batch. The block
    /// stops when adding its own cycles would cross the same boundary the interpreter uses.
    /// </param>
    /// <returns>The ARM clock cycles consumed by the executed instructions.</returns>
    /// <remarks>
    /// The machine calls this instead of <see cref="Step"/> when the JIT backend is selected.
    /// The block returns as soon as the accumulated batch would reach 16, matching the frame
    /// loop's batching threshold, and after every branch or store.
    /// </remarks>
    public int RunJitBlock(int cyclesSinceEvent)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported
            || _tuning.CpuBackend != CpuBackendKind.Jit
            || CycleClassTelemetryEnabled
            || _compatibility.BiosAnvil
            || _jitCompileFailed)
        {
            return Step();
        }

        var remainingBatch = Math.Max(16 - cyclesSinceEvent, 1);
        var total = 0;
        while (total < remainingBatch)
        {
            uint pc = _state.ProgramCounter;
            var index = FastIndex(pc);
            JitBlock? block = _jitFastCache[index];
            if (block is null || block.Pc != pc)
            {
                if (_jitBadBlocks.Contains(pc))
                {
                    total += Step();
                    break;
                }

                if (!_jitBlocks.TryGetValue(pc, out block))
                {
                    // Compile only blocks that are actually hot. BIOS decompression and
                    // one-shot boot paths touch thousands of addresses; compiling them
                    // costs far more than interpreting them once or twice.
                    _jitHits.TryGetValue(pc, out var hits);
                    if (hits + 1 < JitCompileThreshold)
                    {
                        if (_jitHits.Count >= JitMaxCachedBlocks)
                        {
                            _jitHits.Clear();
                        }

                        _jitHits[pc] = hits + 1;
                        total += Step();
                        break;
                    }

                    _jitHits.Remove(pc);
                    block = TryCompileBlock(pc);
                    if (block is null)
                    {
                        _jitBadBlocks.Add(pc);
                        total += Step();
                        break;
                    }

                    if (_jitBlocks.Count >= JitMaxCachedBlocks)
                    {
                        _jitBlocks.Clear();
                        Array.Clear(_jitFastCache);
                    }

                    _jitBlocks[pc] = block;
                    _jitBlocksCompiled++;
                }

                _jitFastCache[index] = block;
            }

            _jitBlocksExecuted++;
            try
            {
                int cycles = block.Execute(this, remainingBatch - total);
                if (cycles == JitStaleBlock)
                {
                    // The first instruction changed after the block was compiled.
                    _jitBlocks.Remove(pc);
                    _jitFastCache[index] = null;
                    total += Step();
                    break;
                }

                total += cycles;
                if (block.EndsWithStore || cycles <= 0)
                {
                    break;
                }
            }
            catch (Exception exception) when (exception is InvalidProgramException
                                                  or MethodAccessException
                                                  or BadImageFormatException
                                                  or TypeLoadException
                                                  or NotSupportedException)
            {
                total += FailJitBlock(pc, exception);
                break;
            }
        }

        return total;
    }

    private static int FastIndex(uint pc) => (int)((pc >> 2) & 0xfff);

    /// <summary>Runs one JIT block at a batch boundary.</summary>
    /// <returns>The ARM clock cycles consumed by the executed instructions.</returns>
    public int RunJitBlock() => RunJitBlock(0);

    private int FailJitBlock(uint pc, Exception exception)
    {
        _jitBadBlocks.Add(pc);
        _jitBlocks.Remove(pc);
        _jitFastCache[FastIndex(pc)] = null;
        _jitLastFailure = $"0x{pc:X8}: {exception.GetType().Name}: {exception.Message}";
        return Step();
    }

    private void ClearJitCache()
    {
        _jitBlocks.Clear();
        _jitHits.Clear();
        _jitBadBlocks.Clear();
        Array.Clear(_jitFastCache);
        _jitBlocksCompiled = 0;
        _jitBlocksExecuted = 0;
        _jitCompileTicks = 0;
        _jitCompiledDp = 0;
        _jitCompiledLoad = 0;
        _jitCompiledStore = 0;
        _jitCompiledBranch = 0;
        _jitCompileFailed = false;
        _jitLastFailure = null;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A JIT compilation failure must fall back to the interpreter instead of stopping emulation.")]
    private JitBlock? TryCompileBlock(uint pc)
    {
        try
        {
            var instructions = new List<uint>(JitMaxBlockInstructions);
            var estimatedCycles = 0;
            var lastKind = JitInstructionKind.DataProcessing;
            for (var i = 0; i < JitMaxBlockInstructions; i++)
            {
                uint instructionPc = unchecked(pc + ((uint)i * 4));
                if (instructionPc == 0x94D60)
                {
                    // Crash 'n Burn's LDM/PC fix is applied by the interpreter.
                    break;
                }

                uint instruction = _bus.Read32(instructionPc);
                if (instruction == 0xE5101810)
                {
                    // The FreeDO condition-bypass exception encoding.
                    break;
                }

                if (!TryClassifyJit(instruction, out JitInstructionKind kind, out var cost, out var terminator))
                {
                    break;
                }

                instructions.Add(instruction);
                lastKind = kind;
                switch (kind)
                {
                    case JitInstructionKind.DataProcessing:
                        _jitCompiledDp++;
                        break;
                    case JitInstructionKind.Load:
                        _jitCompiledLoad++;
                        break;
                    case JitInstructionKind.Store:
                        _jitCompiledStore++;
                        break;
                    case JitInstructionKind.Branch:
                        _jitCompiledBranch++;
                        break;
                }

                estimatedCycles += cost;
                if (terminator || estimatedCycles >= 16)
                {
                    break;
                }
            }

            if (instructions.Count == 0)
            {
                return null;
            }

            long compileStart = Stopwatch.GetTimestamp();
            JitBlockDelegate execute = CompileBlock(pc, instructions);
            _jitCompileTicks += Stopwatch.GetTimestamp() - compileStart;
            return new JitBlock(pc, lastKind == JitInstructionKind.Store, execute);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _jitCompileFailed = true;
            return null;
        }
    }

    private static bool TryClassifyJit(
        uint instruction,
        out JitInstructionKind kind,
        out int cost,
        out bool terminator)
    {
        kind = JitInstructionKind.DataProcessing;
        cost = 1;
        terminator = false;

        var high = (instruction >> 24) & 0xf;

        if (high == 0 && (instruction & ArmDecoder.MultiplyMask) == ArmDecoder.MultiplySignature)
        {
            return false;
        }

        if ((high is 0 or 1) && (instruction & ArmDecoder.SwapMask) == ArmDecoder.SwapSignature)
        {
            return false;
        }

        if (high <= 3)
        {
            if ((instruction & ArmDecoder.DataProcessingMask) == 0x90)
            {
                return false;
            }

            var opcode = (instruction >> 20) & 0x1f;
            if (opcode is 16 or 18 or 20 or 22)
            {
                // MRS/MSR read or write the status registers and may change the banked mode.
                return false;
            }

            if (((instruction >> 12) & 0xf) == 15)
            {
                // A PC write or SPSR restore changes control flow or the mode.
                return false;
            }

            if ((instruction & (1u << 25)) == 0 && (instruction & (1u << 4)) != 0)
            {
                // Register-specified shifts stay on the interpreter path for now.
                return false;
            }

            kind = JitInstructionKind.DataProcessing;
            cost = 1;
            return true;
        }

        if (high is 4 or 5 or 6 or 7)
        {
            if (high is 6 or 7 && (instruction & ArmDecoder.UndefinedMask) == ArmDecoder.UndefinedSignature)
            {
                return false;
            }

            if ((instruction & ArmDecoder.SingleDataTransferMask) == ArmDecoder.SingleDataTransferMask)
            {
                return false;
            }

            if ((instruction & (1u << 25)) != 0)
            {
                // Register-offset addressing stays on the interpreter path.
                return false;
            }

            if (((instruction >> 12) & 0xf) == 15)
            {
                return false;
            }

            var rn = (int)((instruction >> 16) & 0xf);
            var load = (instruction & (1u << 20)) != 0;
            var preIndex = (instruction & (1u << 24)) != 0;
            var writeback = (instruction & (1u << 21)) != 0;
            if (rn == 15 && (writeback || !preIndex))
            {
                // A post-indexed PC base would write PC from the writeback path.
                return false;
            }

            kind = load ? JitInstructionKind.Load : JitInstructionKind.Store;
            cost = load ? 6 : 8;
            terminator = !load;
            return true;
        }

        if (high is 0xa or 0xb)
        {
            kind = JitInstructionKind.Branch;
            cost = 6;
            terminator = true;
            return true;
        }

        return false;
    }

    private static JitBlockDelegate CompileBlock(uint pc, List<uint> instructions)
    {
        var method = new DynamicMethod(
            $"ArmJit_{pc:X8}",
            typeof(int),
            [typeof(Arm60), typeof(int)],
            typeof(Arm60),
            skipVisibility: true);
        ILGenerator il = method.GetILGenerator();

        LocalBuilder state = il.DeclareLocal(typeof(Arm60State));
        LocalBuilder total = il.DeclareLocal(typeof(int));
        LocalBuilder cycles = il.DeclareLocal(typeof(int));

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, JitStateField);
        il.Emit(OpCodes.Stloc, state);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc, total);

        for (var i = 0; i < instructions.Count; i++)
        {
            EmitInstruction(
                il,
                state,
                total,
                cycles,
                instructions[i],
                unchecked(pc + ((uint)i * 4)),
                isFirst: i == 0);
        }

        il.Emit(OpCodes.Ldloc, total);
        il.Emit(OpCodes.Ret);
        return method.CreateDelegate<JitBlockDelegate>();
    }

    private static void EmitInstruction(
        ILGenerator il,
        LocalBuilder state,
        LocalBuilder total,
        LocalBuilder cycles,
        uint instruction,
        uint instructionPc,
        bool isFirst)
    {
        _ = TryClassifyJit(instruction, out JitInstructionKind kind, out var cost, out var terminator);

        // Self-modifying code can change an instruction while an earlier one executes.
        // Verify each instruction immediately before it runs. A first-instruction
        // mismatch returns the stale sentinel; later mismatches return the cycles
        // already committed and the next call recompiles.
        Label unchanged = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4, unchecked((int)instructionPc));
        il.Emit(OpCodes.Call, JitReadWord);
        il.Emit(OpCodes.Ldc_I4, unchecked((int)instruction));
        il.Emit(OpCodes.Beq, unchanged);
        if (isFirst)
        {
            il.Emit(OpCodes.Ldc_I4, JitStaleBlock);
        }
        else
        {
            il.Emit(OpCodes.Ldloc, total);
        }

        il.Emit(OpCodes.Ret);
        il.MarkLabel(unchanged);

        // Step() increments PC, charges one S-cycle, and counts the instruction before
        // evaluating the condition code. Keep that order so failed conditions still cost
        // one cycle.
        EmitPcIncrement(il, state);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_M1);
        il.Emit(OpCodes.Stfld, JitCyclesField);
        EmitInstructionCountIncrement(il);

        var condition = instruction >> 28;
        var always = condition == 0xe;
        Label skipSemantics = il.DefineLabel();
        if (!always)
        {
            il.Emit(OpCodes.Ldloc, state);
            il.Emit(OpCodes.Callvirt, JitCpsrGet);
            il.Emit(OpCodes.Ldc_I4, (int)condition);
            il.Emit(OpCodes.Call, JitConditionPassed);
            il.Emit(OpCodes.Brfalse, skipSemantics);
        }

        if (cost != 1)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, -cost);
            il.Emit(OpCodes.Stfld, JitCyclesField);
        }

        switch (kind)
        {
            case JitInstructionKind.DataProcessing:
                EmitDataProcessing(il, state, instruction);
                break;
            case JitInstructionKind.Load:
            case JitInstructionKind.Store:
                EmitSingleDataTransfer(il, state, instruction);
                break;
            case JitInstructionKind.Branch:
                EmitBranch(il, state, instruction);
                break;
            default:
                throw new InvalidOperationException($"Unknown JIT instruction kind {kind}.");
        }

        if (!always)
        {
            il.MarkLabel(skipSemantics);
        }

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, JitCyclesField);
        il.Emit(OpCodes.Neg);
        il.Emit(OpCodes.Stloc, cycles);

        Label takeFiq = il.DefineLabel();
        Label noFiq = il.DefineLabel();
        EmitFiqCheck(il, state, takeFiq, noFiq);
        il.MarkLabel(takeFiq);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, JitEnterFiq);
        EmitAddCycles(il, total, cycles);
        il.Emit(OpCodes.Ldloc, total);
        il.Emit(OpCodes.Ret);
        il.MarkLabel(noFiq);

        EmitAddCycles(il, total, cycles);

        if (terminator)
        {
            il.Emit(OpCodes.Ldloc, total);
            il.Emit(OpCodes.Ret);
            return;
        }

        // Preserve the frame loop's 16-cycle batching threshold. The caller passes the
        // cycles already accumulated since the last event, so the block stops exactly
        // where the interpreter's per-instruction loop would.
        Label belowBatch = il.DefineLabel();
        il.Emit(OpCodes.Ldloc, total);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Blt, belowBatch);
        il.Emit(OpCodes.Ldloc, total);
        il.Emit(OpCodes.Ret);
        il.MarkLabel(belowBatch);
    }

    private static void EmitPcIncrement(ILGenerator il, LocalBuilder state)
    {
        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Ldfld, JitUserField);
        il.Emit(OpCodes.Ldc_I4, 15);
        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Ldfld, JitUserField);
        il.Emit(OpCodes.Ldc_I4, 15);
        il.Emit(OpCodes.Ldelem_U4);
        il.Emit(OpCodes.Ldc_I4_4);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stelem_I4);
    }

    private static void EmitInstructionCountIncrement(ILGenerator il)
    {
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Ldfld, JitInstructionsField);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Conv_I8);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stfld, JitInstructionsField);
    }

    private static void EmitAddCycles(ILGenerator il, LocalBuilder total, LocalBuilder cycles)
    {
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Ldfld, JitCyclesExecutedField);
        il.Emit(OpCodes.Ldloc, cycles);
        il.Emit(OpCodes.Conv_I8);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stfld, JitCyclesExecutedField);

        il.Emit(OpCodes.Ldloc, total);
        il.Emit(OpCodes.Ldloc, cycles);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, total);
    }

    private static void EmitFiqCheck(ILGenerator il, LocalBuilder state, Label takeFiq, Label noFiq)
    {
        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Callvirt, JitCpsrGet);
        il.Emit(OpCodes.Ldc_I4_S, 0x40);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Brtrue, noFiq);

        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Callvirt, JitFiqPendingGet);
        il.Emit(OpCodes.Brtrue, takeFiq);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, JitInterruptsField);
        il.Emit(OpCodes.Brfalse, noFiq);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, JitInterruptsField);
        il.Emit(OpCodes.Callvirt, JitNeedFiqGet);
        il.Emit(OpCodes.Brfalse, noFiq);
    }

    private static void EmitLoadUserRegister(
        ILGenerator il,
        LocalBuilder state,
        int register,
        LocalBuilder destination)
    {
        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Ldfld, JitUserField);
        il.Emit(OpCodes.Ldc_I4, register);
        il.Emit(OpCodes.Ldelem_U4);
        il.Emit(OpCodes.Stloc, destination);
    }

    private static void EmitStoreUserRegister(
        ILGenerator il,
        LocalBuilder state,
        int register,
        LocalBuilder value)
    {
        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Ldfld, JitUserField);
        il.Emit(OpCodes.Ldc_I4, register);
        il.Emit(OpCodes.Ldloc, value);
        il.Emit(OpCodes.Stelem_I4);
    }

    private static void EmitLoadCarry(ILGenerator il, LocalBuilder state, LocalBuilder destination)
    {
        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Call, JitGetCarry);
        il.Emit(OpCodes.Stloc, destination);
    }

    private static void EmitSetCarryFromValue(ILGenerator il, LocalBuilder state, LocalBuilder carry)
    {
        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Ldloc, carry);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Cgt_Un);
        il.Emit(OpCodes.Call, JitSetC);
    }

    private static void EmitSetZn(ILGenerator il, LocalBuilder state, LocalBuilder value)
    {
        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Ldloc, value);
        il.Emit(OpCodes.Call, JitSetZn);
    }

    private static void EmitSetCv(
        ILGenerator il,
        LocalBuilder state,
        LocalBuilder result,
        LocalBuilder op1,
        LocalBuilder op2)
    {
        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Ldloc, result);
        il.Emit(OpCodes.Ldloc, op1);
        il.Emit(OpCodes.Ldloc, op2);
        il.Emit(OpCodes.Call, JitSetCv);
    }

    private static void EmitSetCvSub(
        ILGenerator il,
        LocalBuilder state,
        LocalBuilder result,
        LocalBuilder op1,
        LocalBuilder op2)
    {
        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Ldloc, result);
        il.Emit(OpCodes.Ldloc, op1);
        il.Emit(OpCodes.Ldloc, op2);
        il.Emit(OpCodes.Call, JitSetCvSub);
    }

    private static void EmitCarryBit(ILGenerator il, LocalBuilder source, LocalBuilder carry, int bit)
    {
        il.Emit(OpCodes.Ldloc, source);
        il.Emit(OpCodes.Ldc_I4, bit);
        il.Emit(OpCodes.Shr_Un);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Stloc, carry);
    }

    private static void EmitBinary(
        ILGenerator il,
        LocalBuilder left,
        LocalBuilder right,
        LocalBuilder destination,
        OpCode operation)
    {
        il.Emit(OpCodes.Ldloc, left);
        il.Emit(OpCodes.Ldloc, right);
        il.Emit(operation);
        il.Emit(OpCodes.Stloc, destination);
    }

    private static void EmitDataProcessing(ILGenerator il, LocalBuilder state, uint instruction)
    {
        var op1 = il.DeclareLocal(typeof(uint));
        var op2 = il.DeclareLocal(typeof(uint));
        var source = il.DeclareLocal(typeof(uint));
        var carry = il.DeclareLocal(typeof(uint));
        var result = il.DeclareLocal(typeof(uint));
        var temp = il.DeclareLocal(typeof(uint));

        var rd = (int)((instruction >> 12) & 0xf);
        var rn = (int)((instruction >> 16) & 0xf);
        var opcode = (instruction >> 20) & 0x1f;
        var baseOpcode = (int)(opcode & 0x1e);
        var setFlags = (opcode & 1) != 0;

        EmitLoadUserRegister(il, state, rn, op1);
        if (rn == 15)
        {
            // ExecuteDataProcessing increments PC a second time before reading operands,
            // so Rn=R15 observes PC+8.
            il.Emit(OpCodes.Ldloc, op1);
            il.Emit(OpCodes.Ldc_I4_4);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, op1);
        }

        if ((instruction & (1u << 25)) != 0)
        {
            var immediate = instruction & 0xff;
            var rotate = (int)((instruction >> 7) & 0x1e);
            uint value = ArmBarrelShifter.RotateRight(immediate, rotate);
            il.Emit(OpCodes.Ldc_I4, unchecked((int)value));
            il.Emit(OpCodes.Stloc, op2);
            if (rotate == 0)
            {
                EmitLoadCarry(il, state, carry);
            }
            else
            {
                il.Emit(OpCodes.Ldc_I4, (int)((immediate >> (rotate - 1)) & 1));
                il.Emit(OpCodes.Stloc, carry);
            }
        }
        else
        {
            var rm = (int)(instruction & 0xf);
            EmitLoadUserRegister(il, state, rm, source);
            if (rm == 15)
            {
                il.Emit(OpCodes.Ldloc, source);
                il.Emit(OpCodes.Ldc_I4_4);
                il.Emit(OpCodes.Add);
                il.Emit(OpCodes.Stloc, source);
            }

            var shiftType = (int)((instruction >> 5) & 3);
            var shift = (int)((instruction >> 7) & 0x1f);
            EmitImmediateShift(il, state, source, op2, carry, temp, shiftType, shift);
        }

        if (setFlags && IsLogicOpcode(baseOpcode))
        {
            EmitSetCarryFromValue(il, state, carry);
        }

        switch (baseOpcode)
        {
            case 0:
            case 16:
                EmitBinary(il, op1, op2, result, OpCodes.And);
                break;
            case 2:
            case 18:
                EmitBinary(il, op1, op2, result, OpCodes.Xor);
                break;
            case 4:
            case 20:
                EmitBinary(il, op1, op2, result, OpCodes.Sub);
                break;
            case 6:
                EmitBinary(il, op2, op1, result, OpCodes.Sub);
                break;
            case 8:
            case 22:
                EmitBinary(il, op1, op2, result, OpCodes.Add);
                break;
            case 10:
                EmitBinary(il, op1, op2, result, OpCodes.Add);
                EmitLoadCarry(il, state, temp);
                EmitBinary(il, result, temp, result, OpCodes.Add);
                break;
            case 12:
                EmitBinary(il, op1, op2, result, OpCodes.Sub);
                EmitLoadCarry(il, state, temp);
                il.Emit(OpCodes.Ldloc, temp);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Xor);
                il.Emit(OpCodes.Stloc, temp);
                EmitBinary(il, result, temp, result, OpCodes.Sub);
                break;
            case 14:
                EmitBinary(il, op2, op1, result, OpCodes.Sub);
                EmitLoadCarry(il, state, temp);
                il.Emit(OpCodes.Ldloc, temp);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Xor);
                il.Emit(OpCodes.Stloc, temp);
                EmitBinary(il, result, temp, result, OpCodes.Sub);
                break;
            case 24:
                EmitBinary(il, op1, op2, result, OpCodes.Or);
                break;
            case 26:
                il.Emit(OpCodes.Ldloc, op2);
                il.Emit(OpCodes.Stloc, result);
                break;
            case 28:
                il.Emit(OpCodes.Ldloc, op2);
                il.Emit(OpCodes.Not);
                il.Emit(OpCodes.Stloc, temp);
                EmitBinary(il, op1, temp, result, OpCodes.And);
                break;
            case 30:
                il.Emit(OpCodes.Ldloc, op2);
                il.Emit(OpCodes.Not);
                il.Emit(OpCodes.Stloc, result);
                break;
            default:
                throw new InvalidOperationException($"Unsupported JIT data-processing opcode 0x{opcode:X2}.");
        }

        var flagOnly = baseOpcode is 16 or 18 or 20 or 22;
        if (!flagOnly)
        {
            EmitStoreUserRegister(il, state, rd, result);
        }

        if (!setFlags)
        {
            return;
        }

        switch (baseOpcode)
        {
            case 16:
            case 18:
                EmitSetZn(il, state, result);
                break;
            case 20:
                EmitSetCvSub(il, state, result, op1, op2);
                EmitSetZn(il, state, result);
                break;
            case 22:
                EmitSetCv(il, state, result, op1, op2);
                EmitSetZn(il, state, result);
                break;
            case 4:
            case 12:
                EmitSetZn(il, state, result);
                EmitSetCvSub(il, state, result, op1, op2);
                break;
            case 6:
            case 14:
                EmitSetZn(il, state, result);
                EmitSetCvSub(il, state, result, op2, op1);
                break;
            case 8:
            case 10:
                EmitSetZn(il, state, result);
                EmitSetCv(il, state, result, op1, op2);
                break;
            default:
                EmitSetZn(il, state, result);
                break;
        }
    }

    private static bool IsLogicOpcode(int baseOpcode)
    {
        return baseOpcode is 0 or 2 or 16 or 18 or 24 or 26 or 28 or 30;
    }

    private static void EmitImmediateShift(
        ILGenerator il,
        LocalBuilder state,
        LocalBuilder source,
        LocalBuilder destination,
        LocalBuilder carry,
        LocalBuilder temp,
        int shiftType,
        int shift)
    {
        if (shiftType == 0 && shift == 0)
        {
            il.Emit(OpCodes.Ldloc, source);
            il.Emit(OpCodes.Stloc, destination);
            EmitLoadCarry(il, state, carry);
            return;
        }

        if (shiftType == 1 && shift == 0)
        {
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Stloc, destination);
            EmitCarryBit(il, source, carry, 31);
            return;
        }

        if (shiftType == 2 && shift == 0)
        {
            il.Emit(OpCodes.Ldloc, source);
            il.Emit(OpCodes.Ldc_I4, 31);
            il.Emit(OpCodes.Shr);
            il.Emit(OpCodes.Stloc, temp);
            il.Emit(OpCodes.Ldloc, temp);
            il.Emit(OpCodes.Stloc, destination);
            EmitCarryBit(il, source, carry, 31);
            return;
        }

        if (shiftType == 3 && shift == 0)
        {
            // RRX: rotate right through the old carry.
            EmitLoadCarry(il, state, temp);
            il.Emit(OpCodes.Ldloc, source);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Shr_Un);
            il.Emit(OpCodes.Ldloc, temp);
            il.Emit(OpCodes.Ldc_I4, 31);
            il.Emit(OpCodes.Shl);
            il.Emit(OpCodes.Or);
            il.Emit(OpCodes.Stloc, destination);
            il.Emit(OpCodes.Ldloc, source);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.And);
            il.Emit(OpCodes.Stloc, carry);
            return;
        }

        switch (shiftType)
        {
            case 0:
                il.Emit(OpCodes.Ldloc, source);
                il.Emit(OpCodes.Ldc_I4, shift);
                il.Emit(OpCodes.Shl);
                break;
            case 1:
                il.Emit(OpCodes.Ldloc, source);
                il.Emit(OpCodes.Ldc_I4, shift);
                il.Emit(OpCodes.Shr_Un);
                break;
            case 2:
                il.Emit(OpCodes.Ldloc, source);
                il.Emit(OpCodes.Ldc_I4, shift);
                il.Emit(OpCodes.Shr);
                break;
            default:
                il.Emit(OpCodes.Ldloc, source);
                il.Emit(OpCodes.Ldc_I4, shift & 31);
                il.Emit(OpCodes.Call, JitRotateRight);
                break;
        }

        il.Emit(OpCodes.Stloc, destination);
        EmitCarryBit(il, source, carry, shiftType == 0 ? 32 - shift : shift - 1);
    }

    private static void EmitSingleDataTransfer(ILGenerator il, LocalBuilder state, uint instruction)
    {
        var load = (instruction & (1u << 20)) != 0;
        var byteAccess = (instruction & (1u << 22)) != 0;
        var add = (instruction & (1u << 23)) != 0;
        var preIndex = (instruction & (1u << 24)) != 0;
        var writeback = (instruction & (1u << 21)) != 0;
        var rn = (int)((instruction >> 16) & 0xf);
        var rd = (int)((instruction >> 12) & 0xf);
        uint offset = instruction & 0xfff;
        if (!add)
        {
            offset = 0u - offset;
        }

        var address = il.DeclareLocal(typeof(uint));
        var baseAddress = il.DeclareLocal(typeof(uint));
        var value = il.DeclareLocal(typeof(uint));
        var temp = il.DeclareLocal(typeof(uint));

        EmitLoadUserRegister(il, state, rn, address);
        if (rn == 15)
        {
            // The interpreter's single-data-transfer path increments PC a second time
            // before reading the base register, so a PC-relative access sees PC+8.
            il.Emit(OpCodes.Ldloc, address);
            il.Emit(OpCodes.Ldc_I4_4);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, address);
        }

        il.Emit(OpCodes.Ldloc, address);
        il.Emit(OpCodes.Stloc, baseAddress);

        if (preIndex)
        {
            il.Emit(OpCodes.Ldloc, address);
            il.Emit(OpCodes.Ldc_I4, unchecked((int)offset));
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, address);
            il.Emit(OpCodes.Ldloc, address);
            il.Emit(OpCodes.Stloc, baseAddress);
        }
        else
        {
            il.Emit(OpCodes.Ldloc, baseAddress);
            il.Emit(OpCodes.Ldc_I4, unchecked((int)offset));
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, baseAddress);
        }

        if (load)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloc, address);
            il.Emit(OpCodes.Call, byteAccess ? JitReadByte : JitReadWord);
            if (byteAccess)
            {
                il.Emit(OpCodes.Conv_U4);
            }

            il.Emit(OpCodes.Stloc, value);

            if (!byteAccess)
            {
                il.Emit(OpCodes.Ldloc, address);
                il.Emit(OpCodes.Ldc_I4_3);
                il.Emit(OpCodes.And);
                il.Emit(OpCodes.Stloc, temp);
                Label noRotate = il.DefineLabel();
                il.Emit(OpCodes.Ldloc, temp);
                il.Emit(OpCodes.Brfalse, noRotate);
                il.Emit(OpCodes.Ldloc, value);
                il.Emit(OpCodes.Ldloc, temp);
                il.Emit(OpCodes.Ldc_I4_8);
                il.Emit(OpCodes.Mul);
                il.Emit(OpCodes.Call, JitRotateRight);
                il.Emit(OpCodes.Stloc, value);
                il.MarkLabel(noRotate);
            }

            if ((writeback || !preIndex) && rn != 15)
            {
                EmitStoreUserRegister(il, state, rn, baseAddress);
            }

            if (writeback && !preIndex)
            {
                il.Emit(OpCodes.Ldloc, state);
                il.Emit(OpCodes.Ldc_I4, rd);
                il.Emit(OpCodes.Ldloc, value);
                il.Emit(OpCodes.Call, JitLoadUser);
            }
            else
            {
                EmitStoreUserRegister(il, state, rd, value);
            }

            return;
        }

        if (writeback && !preIndex)
        {
            il.Emit(OpCodes.Ldloc, state);
            il.Emit(OpCodes.Ldc_I4, rd);
            il.Emit(OpCodes.Call, JitReadUser);
            il.Emit(OpCodes.Stloc, value);
        }
        else
        {
            EmitLoadUserRegister(il, state, rd, value);
        }

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, address);
        il.Emit(OpCodes.Ldloc, value);
        il.Emit(OpCodes.Call, byteAccess ? JitWriteByte : JitWriteWord);

        if ((writeback || !preIndex) && rn != 15)
        {
            EmitStoreUserRegister(il, state, rn, baseAddress);
        }
    }

    private static void EmitBranch(ILGenerator il, LocalBuilder state, uint instruction)
    {
        if ((instruction & (1u << 24)) != 0)
        {
            il.Emit(OpCodes.Ldloc, state);
            il.Emit(OpCodes.Ldfld, JitUserField);
            il.Emit(OpCodes.Ldc_I4, 14);
            il.Emit(OpCodes.Ldloc, state);
            il.Emit(OpCodes.Ldfld, JitUserField);
            il.Emit(OpCodes.Ldc_I4, 15);
            il.Emit(OpCodes.Ldelem_U4);
            il.Emit(OpCodes.Stelem_I4);
        }

        var offset = (int)(instruction & 0xffffff);
        if ((offset & 0x800000) != 0)
        {
            offset |= unchecked((int)0xff000000);
        }

        offset <<= 2;

        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Ldfld, JitUserField);
        il.Emit(OpCodes.Ldc_I4, 15);
        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Ldfld, JitUserField);
        il.Emit(OpCodes.Ldc_I4, 15);
        il.Emit(OpCodes.Ldelem_U4);
        il.Emit(OpCodes.Ldc_I4, offset);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Ldc_I4_4);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stelem_I4);
    }

    private sealed class JitBlock
    {
        internal JitBlock(uint pc, bool endsWithStore, JitBlockDelegate execute)
        {
            Pc = pc;
            EndsWithStore = endsWithStore;
            Execute = execute;
        }

        internal uint Pc { get; }

        internal bool EndsWithStore { get; }

        internal JitBlockDelegate Execute { get; }
    }
}
























