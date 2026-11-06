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
/// interpreter remains the accuracy oracle. Blocks are invalidated exactly when a guest
/// write overlaps their instruction bytes, so the hot path does not verify every
/// instruction word on every run. Loads, stores, branches, SWIs, mode changes, register
/// shifts, and unsupported instructions fall back to <see cref="Step"/> as before.
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
    private const int JitCompileThreshold = 4096;
    private const int JitPageShift = 8;

    private static readonly FieldInfo JitInterruptsField =
        typeof(Arm60).GetField("_interrupts", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo JitRamField =
        typeof(Arm60).GetField("_ram", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo JitReturnToMachineField =
        typeof(Arm60).GetField("_jitReturnToMachine", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo JitCpsrSet =
        typeof(Arm60State).GetProperty(nameof(Arm60State.Cpsr))!.GetSetMethod(nonPublic: true)!;

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

    private static readonly MethodInfo JitRotateRight =
        typeof(ArmBarrelShifter).GetMethod("RotateRight", BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo JitShiftNoCarry =
        typeof(ArmBarrelShifter).GetMethod(
            nameof(ArmBarrelShifter.ShiftNoCarry),
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            [typeof(uint), typeof(byte), typeof(byte), typeof(uint), typeof(uint).MakeByRefType()],
            modifiers: null)!;

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

    private static readonly MethodInfo JitRamRead32 =
        typeof(IRam).GetMethod(nameof(IRam.Read32))!;

    private static readonly MethodInfo JitRamWrite32 =
        typeof(IRam).GetMethod(nameof(IRam.Write32))!;

    private static readonly MethodInfo JitRomOverlayGet =
        typeof(IRam).GetProperty(nameof(IRam.RomOverlay))!.GetGetMethod()!;

    private readonly Dictionary<uint, JitBlock> _jitBlocks = [];
    private readonly List<JitBlock>?[] _jitPages = new List<JitBlock>?[1 << 16];
    private readonly uint[] _jitHitTags = new uint[8192];
    private readonly int[] _jitHitCounts = new int[8192];
    private readonly HashSet<uint> _jitBadBlocks = [];
    private readonly JitBlock?[] _jitFastCache = new JitBlock?[8192];
    private bool _jitCompileFailed;
    private bool _jitReturnToMachine;
    private long _jitBlocksCompiled;
    private long _jitBlocksExecuted;
    private long _jitCompileTicks;
    private long _jitCompiledDp;
    private long _jitCompiledLoad;
    private long _jitCompiledStore;
    private long _jitCompiledBranch;
    private long _jitFallbackSteps;
    private long _jitStaticBlockInstructions;
    private long _jitInvalidations;
    private long _jitBadBlockHits;
    private long _jitThresholdHits;
    private long _jitCompileNullHits;
    private string? _jitLastFailure;

    private delegate int JitBlockDelegate(
        Arm60 cpu,
        Arm60State state,
        uint[] user,
        ref uint pc,
        ref uint cpsr,
        ref int cycles,
        ref int instructions,
        int remainingBatch);

    private enum JitInstructionKind
    {
        DataProcessing,
        Load,
        Store,
        Branch,
    }

    private readonly record struct JitInstruction(
        uint Word,
        JitInstructionKind Kind,
        int Cost,
        bool Terminator);

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

    /// <summary>Gets the number of interpreter fallback steps taken from the JIT loop.</summary>
    public long JitFallbackSteps => _jitFallbackSteps;

    /// <summary>Gets the static instruction count of every executed JIT block entry.</summary>
    public long JitStaticBlockInstructions => _jitStaticBlockInstructions;

    /// <summary>Gets the number of compiled blocks removed by a guest write.</summary>
    public long JitInvalidations => _jitInvalidations;

    /// <summary>Gets fallback counts by reason.</summary>
    public long JitBadBlockHits => _jitBadBlockHits;

    /// <summary>Gets threshold fallback count.</summary>
    public long JitThresholdHits => _jitThresholdHits;

    /// <summary>Gets compile-null fallback count.</summary>
    public long JitCompileNullHits => _jitCompileNullHits;

    /// <summary>
    /// Invalidates compiled blocks whose instruction bytes overlap a guest RAM write.
    /// </summary>
    /// <param name="offset">Byte offset in the RAM backing array.</param>
    /// <param name="size">Access size in bytes.</param>
    /// <remarks>
    /// The memory subsystem calls this for every RAM write, including DMA, CEL, and
    /// cheat-engine writes. Writes outside compiled code do not disturb the cache.
    /// </remarks>
    public void NotifyRamWrite(uint offset, int size)
    {
        if (size <= 0)
        {
            return;
        }

        uint end = offset + (uint)size;
        var firstPage = (int)(offset >> JitPageShift);
        var lastPage = (int)((end - 1) >> JitPageShift);
        if (firstPage >= _jitPages.Length)
        {
            return;
        }

        if (lastPage >= _jitPages.Length)
        {
            lastPage = _jitPages.Length - 1;
        }

        for (var page = firstPage; page <= lastPage; page++)
        {
            List<JitBlock>? blocks = _jitPages[page];
            if (blocks is null)
            {
                continue;
            }

            for (var index = blocks.Count - 1; index >= 0; index--)
            {
                JitBlock block = blocks[index];
                if (block.Pc < end && offset < block.CodeEnd)
                {
                    _jitInvalidations++;
                    RemoveBlock(block);
                }
            }
        }
    }

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
    /// PC, CPSR, cycle, and instruction counters live in a context shared by every block in
    /// the call, so a chain of blocks does not reload or flush the architectural state between
    /// them. The loop stops at the same 16-cycle boundary as the interpreter and on branches,
    /// stores, FIQs, or MMIO traffic.
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
        uint pc = _state.ProgramCounter;
        uint cpsr = _state.Cpsr;
        int cycles = 0;
        int instructions = 0;
        _jitReturnToMachine = false;

        while (cycles < remainingBatch)
        {
            var index = FastIndex(pc);
            JitBlock? block = _jitFastCache[index];
            if (block is null || block.Pc != pc)
            {
                if (_jitBadBlocks.Contains(pc))
                {
                    _jitBadBlockHits++;
                    NoteFallback();
                    return FlushAndStep(ref pc, ref cpsr, ref cycles, ref instructions);
                }

                if (!_jitBlocks.TryGetValue(pc, out block))
                {
                    // Compile only blocks that are actually hot. BIOS decompression and
                    // one-shot boot paths touch thousands of addresses; compiling them
                    // costs far more than interpreting them once or twice. A direct-mapped
                    // saturating counter avoids the old dictionary's global clear, which
                    // could keep a hot loop below the threshold when cold code filled it.
                    var hitIndex = FastIndex(pc);
                    if (_jitHitTags[hitIndex] != pc)
                    {
                        _jitHitTags[hitIndex] = pc;
                        _jitHitCounts[hitIndex] = 1;
                        _jitThresholdHits++;
                        NoteFallback();
                        return FlushAndStep(ref pc, ref cpsr, ref cycles, ref instructions);
                    }

                    if (++_jitHitCounts[hitIndex] < JitCompileThreshold)
                    {
                        _jitThresholdHits++;
                        NoteFallback();
                        return FlushAndStep(ref pc, ref cpsr, ref cycles, ref instructions);
                    }

                    _jitHitCounts[hitIndex] = 0;
                    block = TryCompileBlock(pc);
                    if (block is null)
                    {
                        _jitBadBlocks.Add(pc);
                        _jitCompileNullHits++;
                        NoteFallback();
                        return FlushAndStep(ref pc, ref cpsr, ref cycles, ref instructions);
                    }

                    if (_jitBlocks.Count >= JitMaxCachedBlocks)
                    {
                        ClearBlockCache();
                    }

                    RegisterBlock(block);
                    _jitBlocksCompiled++;
                }

                _jitFastCache[index] = block;
            }

            _jitBlocksExecuted++;
            _jitStaticBlockInstructions += block.InstructionCount;
            uint beforePc = pc;
            int beforeCycles = cycles;
            try
            {
                int blockCycles = block.Execute(
                    this,
                    _state,
                    _state.User,
                    ref pc,
                    ref cpsr,
                    ref cycles,
                    ref instructions,
                    remainingBatch);
                if (_jitReturnToMachine)
                {
                    _jitReturnToMachine = false;
                    int result = cycles;
                    FlushJitContext(ref pc, ref cpsr, ref cycles, ref instructions);
                    return result;
                }

                if (cycles == beforeCycles && pc == beforePc)
                {
                    _jitLastFailure = $"No progress at 0x{pc:X8}.";
                    _jitBadBlocks.Add(pc);
                    FlushJitContext(ref pc, ref cpsr, ref cycles, ref instructions);
                    return cycles + Step();
                }

                if (blockCycles <= 0 && cycles == 0)
                {
                    // A compiled block must always make progress. If it does not, fall
                    // back to one interpreter instruction instead of returning zero and
                    // spinning the frame loop.
                    FlushJitContext(ref pc, ref cpsr, ref cycles, ref instructions);
                    return Step();
                }
            }
            catch (Exception exception) when (exception is InvalidProgramException
                                                  or MethodAccessException
                                                  or BadImageFormatException
                                                  or TypeLoadException
                                                  or NotSupportedException)
            {
                int result = cycles;
                FlushJitContext(ref pc, ref cpsr, ref cycles, ref instructions);
                return result + FailJitBlock(pc, exception);
            }
        }

        int total = cycles;
        FlushJitContext(ref pc, ref cpsr, ref cycles, ref instructions);
        return total;
    }

    private int FlushAndStep(ref uint pc, ref uint cpsr, ref int cycles, ref int instructions)
    {
        int total = cycles;
        FlushJitContext(ref pc, ref cpsr, ref cycles, ref instructions);
        return total + Step();
    }

    private void FlushJitContext(ref uint pc, ref uint cpsr, ref int cycles, ref int instructions)
    {
        _state.ProgramCounter = pc;
        _state.Cpsr = cpsr;
        _cyclesExecuted += cycles;
        _instructionsExecuted += instructions;
        _cycles = 0;
    }

    private static int FastIndex(uint pc) => (int)((pc >> 2) & 0x1fff);

    private void NoteFallback()
    {
        _jitFallbackSteps++;
    }

    /// <summary>Runs one JIT block at a batch boundary.</summary>
    /// <returns>The ARM clock cycles consumed by the executed instructions.</returns>
    public int RunJitBlock() => RunJitBlock(0);

    private int FailJitBlock(uint pc, Exception exception)
    {
        _jitBadBlocks.Add(pc);
        if (_jitBlocks.TryGetValue(pc, out JitBlock? block))
        {
            RemoveBlock(block);
        }

        _jitLastFailure = $"0x{pc:X8}: {exception.GetType().Name}: {exception.Message}";
        return Step();
    }

    private void ClearJitCache()
    {
        ClearBlockCache();
        Array.Clear(_jitHitTags);
        Array.Clear(_jitHitCounts);
        _jitBadBlocks.Clear();
        _jitBlocksCompiled = 0;
        _jitBlocksExecuted = 0;
        _jitCompileTicks = 0;
        _jitCompiledDp = 0;
        _jitCompiledLoad = 0;
        _jitCompiledStore = 0;
        _jitCompiledBranch = 0;
        _jitFallbackSteps = 0;
        _jitStaticBlockInstructions = 0;
        _jitInvalidations = 0;
        _jitBadBlockHits = 0;
        _jitThresholdHits = 0;
        _jitCompileNullHits = 0;
        _jitCompileFailed = false;
        _jitLastFailure = null;
    }

    private void ClearBlockCache()
    {
        _jitBlocks.Clear();
        Array.Clear(_jitPages);
        _jitReturnToMachine = false;
        Array.Clear(_jitFastCache);
    }

    private void RegisterBlock(JitBlock block)
    {
        _jitBlocks[block.Pc] = block;
        var firstPage = (int)(block.Pc >> JitPageShift);
        var lastPage = (int)((block.CodeEnd - 1) >> JitPageShift);
        if (firstPage >= _jitPages.Length)
        {
            // Code above the RAM window is ROM and can never be modified.
            return;
        }

        if (lastPage >= _jitPages.Length)
        {
            lastPage = _jitPages.Length - 1;
        }

        for (var page = firstPage; page <= lastPage; page++)
        {
            List<JitBlock>? blocks = _jitPages[page];
            if (blocks is null)
            {
                blocks = [];
                _jitPages[page] = blocks;
            }

            blocks.Add(block);
        }
    }

    private void RemoveBlock(JitBlock block)
    {
        _jitBlocks.Remove(block.Pc);
        var index = FastIndex(block.Pc);
        if (ReferenceEquals(_jitFastCache[index], block))
        {
            _jitFastCache[index] = null;
        }

        var firstPage = (int)(block.Pc >> JitPageShift);
        var lastPage = (int)((block.CodeEnd - 1) >> JitPageShift);
        if (firstPage >= _jitPages.Length)
        {
            return;
        }

        if (lastPage >= _jitPages.Length)
        {
            lastPage = _jitPages.Length - 1;
        }

        for (var page = firstPage; page <= lastPage; page++)
        {
            List<JitBlock>? blocks = _jitPages[page];
            if (blocks is null)
            {
                continue;
            }

            blocks.Remove(block);
            if (blocks.Count == 0)
            {
                _jitPages[page] = null;
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A JIT compilation failure must fall back to the interpreter instead of stopping emulation.")]
    private JitBlock? TryCompileBlock(uint pc)
    {
        try
        {
            var instructions = new List<JitInstruction>(JitMaxBlockInstructions);
            var estimatedCycles = 0;
            for (var i = 0; i < JitMaxBlockInstructions; i++)
            {
                uint instructionPc = unchecked(pc + ((uint)i * 4));
                if (instructionPc == 0x94D60)
                {
                    // Crash 'n Burn's LDM/PC fix is applied by the interpreter.
                    break;
                }

                uint instruction = _bus.ReadInstruction(instructionPc);
                if (instruction == 0xE5101810)
                {
                    // The FreeDO condition-bypass exception encoding.
                    break;
                }

                if (!TryClassifyJit(instruction, out JitInstructionKind kind, out var cost, out var terminator))
                {
                    break;
                }

                instructions.Add(new JitInstruction(instruction, kind, cost, terminator));
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
            uint codeEnd = unchecked(pc + ((uint)instructions.Count * 4));
            var ramSize = _ram?.Layout.RamSize ?? 0;
            JitBlockDelegate execute = CompileBlock(pc, codeEnd, ramSize, instructions);
            _jitCompileTicks += Stopwatch.GetTimestamp() - compileStart;
            return new JitBlock(pc, codeEnd, execute, instructions.Count);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _jitCompileFailed = true;
            return null;
        }
    }

    private bool TryClassifyJit(
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
                // Register-specified shifts take an extra internal cycle.
                kind = JitInstructionKind.DataProcessing;
                cost = 1 + Icycle;
                return true;
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
            cost = load ? _nCycle + 2 : 2 * _nCycle;
            terminator = false;
            return true;
        }

        if (high is 0xa or 0xb)
        {
            kind = JitInstructionKind.Branch;
            cost = _nCycle + 2;
            // Conditional branches stay in the block when they fall through; an
            // unconditional branch always transfers control to another block.
            terminator = (instruction >> 28) == 0xe;
            return true;
        }

        return false;
    }

    private static JitBlockDelegate CompileBlock(
        uint pc,
        uint codeEnd,
        int ramSize,
        List<JitInstruction> instructions)
    {
        var method = new DynamicMethod(
            $"ArmJit_{pc:X8}",
            typeof(int),
            [
                typeof(Arm60),
                typeof(Arm60State),
                typeof(uint[]),
                typeof(uint).MakeByRefType(),
                typeof(uint).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(int),
            ],
            typeof(Arm60),
            skipVisibility: true);
        ILGenerator il = method.GetILGenerator();

        LocalBuilder state = il.DeclareLocal(typeof(Arm60State));
        LocalBuilder user = il.DeclareLocal(typeof(uint[]));
        LocalBuilder pcLocal = il.DeclareLocal(typeof(uint));
        LocalBuilder cpsr = il.DeclareLocal(typeof(uint));
        LocalBuilder cycles = il.DeclareLocal(typeof(int));
        LocalBuilder instructionsExecuted = il.DeclareLocal(typeof(int));
        LocalBuilder forceExit = il.DeclareLocal(typeof(int));

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stloc, state);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Stloc, user);
        il.Emit(OpCodes.Ldarg_3);
        il.Emit(OpCodes.Ldind_U4);
        il.Emit(OpCodes.Stloc, pcLocal);
        il.Emit(OpCodes.Ldarg_S, (byte)4);
        il.Emit(OpCodes.Ldind_U4);
        il.Emit(OpCodes.Stloc, cpsr);
        il.Emit(OpCodes.Ldarg_S, (byte)5);
        il.Emit(OpCodes.Ldind_I4);
        il.Emit(OpCodes.Stloc, cycles);
        il.Emit(OpCodes.Ldarg_S, (byte)6);
        il.Emit(OpCodes.Ldind_I4);
        il.Emit(OpCodes.Stloc, instructionsExecuted);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc, forceExit);

        for (var i = 0; i < instructions.Count; i++)
        {
            EmitInstruction(
                il,
                state,
                user,
                pcLocal,
                cpsr,
                cycles,
                instructionsExecuted,
                forceExit,
                instructions[i],
                pc,
                codeEnd,
                ramSize,
                isLast: i == instructions.Count - 1);
        }

        EmitStoreContext(il, pcLocal, cpsr, cycles, instructionsExecuted);
        il.Emit(OpCodes.Ldloc, cycles);
        il.Emit(OpCodes.Ret);
        return method.CreateDelegate<JitBlockDelegate>();
    }

    private static void EmitInstruction(
        ILGenerator il,
        LocalBuilder state,
        LocalBuilder user,
        LocalBuilder pc,
        LocalBuilder cpsr,
        LocalBuilder cycles,
        LocalBuilder instructionsExecuted,
        LocalBuilder forceExit,
        JitInstruction instruction,
        uint blockStart,
        uint blockEnd,
        int ramSize,
        bool isLast)
    {
        // Step() counts the instruction before evaluating its condition and charges one
        // S-cycle even when the condition fails.
        il.Emit(OpCodes.Ldloc, instructionsExecuted);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, instructionsExecuted);

        il.Emit(OpCodes.Ldloc, cycles);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, cycles);

        var condition = (int)(instruction.Word >> 28);
        var always = condition == 0xe;
        Label skipSemantics = default;
        if (!always)
        {
            skipSemantics = il.DefineLabel();
            il.Emit(OpCodes.Ldloc, cpsr);
            il.Emit(OpCodes.Ldc_I4, condition);
            il.Emit(OpCodes.Call, JitConditionPassed);
            il.Emit(OpCodes.Brfalse, skipSemantics);
        }

        if (instruction.Cost > 1)
        {
            il.Emit(OpCodes.Ldloc, cycles);
            il.Emit(OpCodes.Ldc_I4, instruction.Cost - 1);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, cycles);
        }

        switch (instruction.Kind)
        {
            case JitInstructionKind.DataProcessing:
                EmitDataProcessing(il, state, user, pc, cpsr, instruction.Word);
                break;
            case JitInstructionKind.Load:
            case JitInstructionKind.Store:
                EmitSingleDataTransfer(
                    il,
                    state,
                    user,
                    pc,
                    cpsr,
                    forceExit,
                    instruction.Word,
                    blockStart,
                    blockEnd,
                    ramSize);
                break;
            case JitInstructionKind.Branch:
                EmitBranch(il, state, user, pc, instruction.Word);
                EmitSetForceExit(il, forceExit);
                break;
            default:
                throw new InvalidOperationException($"Unknown JIT instruction kind {instruction.Kind}.");
        }

        if (instruction.Kind == JitInstructionKind.Branch)
        {
            if (!always)
            {
                Label branchDone = il.DefineLabel();
                il.Emit(OpCodes.Br, branchDone);
                il.MarkLabel(skipSemantics);
                EmitAddConstant(il, pc, 4);
                il.MarkLabel(branchDone);
            }
        }
        else
        {
            if (!always)
            {
                il.MarkLabel(skipSemantics);
            }

            EmitAddConstant(il, pc, 4);
        }

        // Preserve the interpreter's post-instruction FIQ check.
        Label takeFiq = il.DefineLabel();
        Label noFiq = il.DefineLabel();
        EmitFiqCheck(il, state, cpsr, takeFiq, noFiq);
        il.MarkLabel(takeFiq);
        EmitStoreState(il, state, user, pc, cpsr);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, JitEnterFiq);
        il.Emit(OpCodes.Ldc_I4, 0x1c);
        il.Emit(OpCodes.Stloc, pc);
        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Callvirt, JitCpsrGet);
        il.Emit(OpCodes.Stloc, cpsr);
        EmitStoreContext(il, pc, cpsr, cycles, instructionsExecuted);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Stfld, JitReturnToMachineField);
        il.Emit(OpCodes.Ldloc, cycles);
        il.Emit(OpCodes.Ret);
        il.MarkLabel(noFiq);

        Label noForcedExit = il.DefineLabel();
        il.Emit(OpCodes.Ldloc, forceExit);
        il.Emit(OpCodes.Brfalse, noForcedExit);
        EmitStoreContext(il, pc, cpsr, cycles, instructionsExecuted);
        il.Emit(OpCodes.Ldloc, cycles);
        il.Emit(OpCodes.Ret);
        il.MarkLabel(noForcedExit);

        if (isLast)
        {
            EmitStoreContext(il, pc, cpsr, cycles, instructionsExecuted);
            il.Emit(OpCodes.Ldloc, cycles);
            il.Emit(OpCodes.Ret);
            return;
        }

        // Stop at the frame loop's 16-cycle batching boundary.
        Label belowBatch = il.DefineLabel();
        il.Emit(OpCodes.Ldloc, cycles);
        il.Emit(OpCodes.Ldarg_S, (byte)7);
        il.Emit(OpCodes.Blt, belowBatch);
        EmitStoreContext(il, pc, cpsr, cycles, instructionsExecuted);
        il.Emit(OpCodes.Ldloc, cycles);
        il.Emit(OpCodes.Ret);
        il.MarkLabel(belowBatch);
    }

    private static void EmitStoreContext(
        ILGenerator il,
        LocalBuilder pc,
        LocalBuilder cpsr,
        LocalBuilder cycles,
        LocalBuilder instructionsExecuted)
    {
        il.Emit(OpCodes.Ldarg_3);
        il.Emit(OpCodes.Ldloc, pc);
        il.Emit(OpCodes.Stind_I4);
        il.Emit(OpCodes.Ldarg_S, (byte)4);
        il.Emit(OpCodes.Ldloc, cpsr);
        il.Emit(OpCodes.Stind_I4);
        il.Emit(OpCodes.Ldarg_S, (byte)5);
        il.Emit(OpCodes.Ldloc, cycles);
        il.Emit(OpCodes.Stind_I4);
        il.Emit(OpCodes.Ldarg_S, (byte)6);
        il.Emit(OpCodes.Ldloc, instructionsExecuted);
        il.Emit(OpCodes.Stind_I4);
    }

    private static void EmitStoreState(
        ILGenerator il,
        LocalBuilder state,
        LocalBuilder user,
        LocalBuilder pc,
        LocalBuilder cpsr)
    {
        EmitStoreUserRegister(il, user, 15, pc);

        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Ldloc, cpsr);
        il.Emit(OpCodes.Callvirt, JitCpsrSet);
    }

    private static void EmitFiqCheck(
        ILGenerator il,
        LocalBuilder state,
        LocalBuilder cpsr,
        Label takeFiq,
        Label noFiq)
    {
        il.Emit(OpCodes.Ldloc, cpsr);
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

    private static void EmitAddConstant(ILGenerator il, LocalBuilder local, int value)
    {
        il.Emit(OpCodes.Ldloc, local);
        il.Emit(OpCodes.Ldc_I4, value);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, local);
    }

    private static void EmitLoadUserRegister(
        ILGenerator il,
        LocalBuilder user,
        int register,
        LocalBuilder destination)
    {
        il.Emit(OpCodes.Ldloc, user);
        il.Emit(OpCodes.Ldc_I4, register);
        il.Emit(OpCodes.Ldelem_U4);
        il.Emit(OpCodes.Stloc, destination);
    }

    private static void EmitStoreUserRegister(
        ILGenerator il,
        LocalBuilder user,
        int register,
        LocalBuilder value)
    {
        il.Emit(OpCodes.Ldloc, user);
        il.Emit(OpCodes.Ldc_I4, register);
        il.Emit(OpCodes.Ldloc, value);
        il.Emit(OpCodes.Stelem_I4);
    }

    private static void EmitStoreUserConstant(
        ILGenerator il,
        LocalBuilder user,
        int register,
        int value)
    {
        il.Emit(OpCodes.Ldloc, user);
        il.Emit(OpCodes.Ldc_I4, register);
        il.Emit(OpCodes.Ldc_I4, value);
        il.Emit(OpCodes.Stelem_I4);
    }

    private static void EmitLoadUserRelative(
        ILGenerator il,
        LocalBuilder user,
        int register,
        LocalBuilder pc,
        int delta,
        LocalBuilder destination)
    {
        if (register == 15)
        {
            // Within a compiled block the architectural R15 is the local instruction
            // address plus the interpreter's PC offset; user[15] is only written at exits.
            il.Emit(OpCodes.Ldloc, pc);
            il.Emit(OpCodes.Ldc_I4, delta);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, destination);
            return;
        }

        EmitLoadUserRegister(il, user, register, destination);
    }

    private static void EmitCpsrCarry(ILGenerator il, LocalBuilder cpsr, LocalBuilder destination)
    {
        il.Emit(OpCodes.Ldloc, cpsr);
        il.Emit(OpCodes.Ldc_I4, 29);
        il.Emit(OpCodes.Shr_Un);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Stloc, destination);
    }

    private static void EmitSetCarryFromCarry(ILGenerator il, LocalBuilder cpsr, LocalBuilder carry)
    {
        il.Emit(OpCodes.Ldloc, cpsr);
        il.Emit(OpCodes.Ldc_I4, unchecked((int)0xdfffffffu));
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldloc, carry);
        il.Emit(OpCodes.Ldc_I4, 29);
        il.Emit(OpCodes.Shl);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Stloc, cpsr);
    }

    private static void EmitSetZn(ILGenerator il, LocalBuilder cpsr, LocalBuilder value)
    {
        Label nonZero = il.DefineLabel();
        Label done = il.DefineLabel();

        il.Emit(OpCodes.Ldloc, value);
        il.Emit(OpCodes.Brtrue, nonZero);

        il.Emit(OpCodes.Ldloc, cpsr);
        il.Emit(OpCodes.Ldc_I4, 0x3fffffff);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldc_I4, 0x40000000);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Stloc, cpsr);
        il.Emit(OpCodes.Br, done);

        il.MarkLabel(nonZero);
        il.Emit(OpCodes.Ldloc, cpsr);
        il.Emit(OpCodes.Ldc_I4, 0x3fffffff);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldloc, value);
        il.Emit(OpCodes.Ldc_I4, unchecked((int)0x80000000u));
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Stloc, cpsr);

        il.MarkLabel(done);
    }

    private static void EmitSetCv(
        ILGenerator il,
        LocalBuilder cpsr,
        LocalBuilder result,
        LocalBuilder op1,
        LocalBuilder op2)
    {
        LocalBuilder carry = il.DeclareLocal(typeof(uint));
        LocalBuilder overflow = il.DeclareLocal(typeof(uint));

        // carry = ((op1 & op2) | (~result & (op1 | op2))) & 0x80000000
        il.Emit(OpCodes.Ldloc, op1);
        il.Emit(OpCodes.Ldloc, op2);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldloc, result);
        il.Emit(OpCodes.Not);
        il.Emit(OpCodes.Ldloc, op1);
        il.Emit(OpCodes.Ldloc, op2);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Ldc_I4, unchecked((int)0x80000000u));
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldc_I4_2);
        il.Emit(OpCodes.Shr_Un);
        il.Emit(OpCodes.Stloc, carry);

        // overflow = ((op1 & (op2 & ~result)) | (~op1 & ~op2 & result)) & 0x80000000
        il.Emit(OpCodes.Ldloc, op1);
        il.Emit(OpCodes.Ldloc, op2);
        il.Emit(OpCodes.Ldloc, result);
        il.Emit(OpCodes.Not);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldloc, op1);
        il.Emit(OpCodes.Not);
        il.Emit(OpCodes.Ldloc, op2);
        il.Emit(OpCodes.Not);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldloc, result);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Ldc_I4, unchecked((int)0x80000000u));
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldc_I4_3);
        il.Emit(OpCodes.Shr_Un);
        il.Emit(OpCodes.Stloc, overflow);

        EmitCombineCv(il, cpsr, carry, overflow);
    }

    private static void EmitSetCvSub(
        ILGenerator il,
        LocalBuilder cpsr,
        LocalBuilder result,
        LocalBuilder op1,
        LocalBuilder op2)
    {
        LocalBuilder carry = il.DeclareLocal(typeof(uint));
        LocalBuilder overflow = il.DeclareLocal(typeof(uint));

        // carry = ((op1 & ~op2) | (~result & (op1 | ~op2))) & 0x80000000
        il.Emit(OpCodes.Ldloc, op1);
        il.Emit(OpCodes.Ldloc, op2);
        il.Emit(OpCodes.Not);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldloc, result);
        il.Emit(OpCodes.Not);
        il.Emit(OpCodes.Ldloc, op1);
        il.Emit(OpCodes.Ldloc, op2);
        il.Emit(OpCodes.Not);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Ldc_I4, unchecked((int)0x80000000u));
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldc_I4_2);
        il.Emit(OpCodes.Shr_Un);
        il.Emit(OpCodes.Stloc, carry);

        // overflow = ((op1 & (~op2 & ~result)) | (~op1 & op2 & result)) & 0x80000000
        il.Emit(OpCodes.Ldloc, op1);
        il.Emit(OpCodes.Ldloc, op2);
        il.Emit(OpCodes.Not);
        il.Emit(OpCodes.Ldloc, result);
        il.Emit(OpCodes.Not);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldloc, op1);
        il.Emit(OpCodes.Not);
        il.Emit(OpCodes.Ldloc, op2);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldloc, result);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Ldc_I4, unchecked((int)0x80000000u));
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldc_I4_3);
        il.Emit(OpCodes.Shr_Un);
        il.Emit(OpCodes.Stloc, overflow);

        EmitCombineCv(il, cpsr, carry, overflow);
    }

    private static void EmitCombineCv(
        ILGenerator il,
        LocalBuilder cpsr,
        LocalBuilder carry,
        LocalBuilder overflow)
    {
        il.Emit(OpCodes.Ldloc, cpsr);
        il.Emit(OpCodes.Ldc_I4, unchecked((int)0xcfffffffu));
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldloc, carry);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Ldloc, overflow);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Stloc, cpsr);
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

    private static void EmitDataProcessing(
        ILGenerator il,
        LocalBuilder state,
        LocalBuilder user,
        LocalBuilder pc,
        LocalBuilder cpsr,
        uint instruction)
    {
        var op1 = il.DeclareLocal(typeof(uint));
        var op2 = il.DeclareLocal(typeof(uint));
        var source = il.DeclareLocal(typeof(uint));
        var shiftAmount = il.DeclareLocal(typeof(uint));
        var carry = il.DeclareLocal(typeof(uint));
        var result = il.DeclareLocal(typeof(uint));
        var temp = il.DeclareLocal(typeof(uint));

        var rd = (int)((instruction >> 12) & 0xf);
        var rn = (int)((instruction >> 16) & 0xf);
        var opcode = (instruction >> 20) & 0x1f;
        var baseOpcode = (int)(opcode & 0x1e);
        var setFlags = (opcode & 1) != 0;

        EmitLoadUserRelative(il, user, rn, pc, 8, op1);

        if ((instruction & (1u << 25)) != 0)
        {
            var immediate = instruction & 0xff;
            var rotate = (int)((instruction >> 7) & 0x1e);
            uint value = ArmBarrelShifter.RotateRight(immediate, rotate);
            il.Emit(OpCodes.Ldc_I4, unchecked((int)value));
            il.Emit(OpCodes.Stloc, op2);
            if (rotate == 0)
            {
                EmitCpsrCarry(il, cpsr, carry);
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
            EmitLoadUserRelative(il, user, rm, pc, 8, source);

            var shiftType = (int)((instruction >> 5) & 3);
            if ((instruction & (1u << 4)) != 0)
            {
                // Register-specified shift: the low byte of Rs is the count. The
                // interpreter adds one internal cycle for this form.
                var rs = (int)((instruction >> 8) & 0xf);
                EmitLoadUserRelative(il, user, rs, pc, 8, shiftAmount);
                il.Emit(OpCodes.Ldloc, source);
                il.Emit(OpCodes.Ldloc, shiftAmount);
                il.Emit(OpCodes.Ldc_I4, 0xff);
                il.Emit(OpCodes.And);
                il.Emit(OpCodes.Conv_U1);
                il.Emit(OpCodes.Ldc_I4, shiftType);
                il.Emit(OpCodes.Conv_U1);
                EmitCpsrCarry(il, cpsr, temp);
                il.Emit(OpCodes.Ldloc, temp);
                il.Emit(OpCodes.Ldloca, carry);
                il.Emit(OpCodes.Call, JitShiftNoCarry);
                il.Emit(OpCodes.Stloc, op2);
            }
            else
            {
                var shift = (int)((instruction >> 7) & 0x1f);
                EmitImmediateShift(il, cpsr, source, op2, carry, temp, shiftType, shift);
            }
        }

        if (setFlags && IsLogicOpcode(baseOpcode))
        {
            EmitSetCarryFromCarry(il, cpsr, carry);
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
                EmitCpsrCarry(il, cpsr, temp);
                EmitBinary(il, result, temp, result, OpCodes.Add);
                break;
            case 12:
                EmitBinary(il, op1, op2, result, OpCodes.Sub);
                EmitCpsrCarry(il, cpsr, temp);
                il.Emit(OpCodes.Ldloc, temp);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Xor);
                il.Emit(OpCodes.Stloc, temp);
                EmitBinary(il, result, temp, result, OpCodes.Sub);
                break;
            case 14:
                EmitBinary(il, op2, op1, result, OpCodes.Sub);
                EmitCpsrCarry(il, cpsr, temp);
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
            EmitStoreUserRegister(il, user, rd, result);
        }

        if (!setFlags)
        {
            return;
        }

        switch (baseOpcode)
        {
            case 16:
            case 18:
                EmitSetZn(il, cpsr, result);
                break;
            case 20:
                EmitSetCvSub(il, cpsr, result, op1, op2);
                EmitSetZn(il, cpsr, result);
                break;
            case 22:
                EmitSetCv(il, cpsr, result, op1, op2);
                EmitSetZn(il, cpsr, result);
                break;
            case 4:
            case 12:
                EmitSetZn(il, cpsr, result);
                EmitSetCvSub(il, cpsr, result, op1, op2);
                break;
            case 6:
            case 14:
                EmitSetZn(il, cpsr, result);
                EmitSetCvSub(il, cpsr, result, op2, op1);
                break;
            case 8:
            case 10:
                EmitSetZn(il, cpsr, result);
                EmitSetCv(il, cpsr, result, op1, op2);
                break;
            default:
                EmitSetZn(il, cpsr, result);
                break;
        }
    }

    private static bool IsLogicOpcode(int baseOpcode)
    {
        return baseOpcode is 0 or 2 or 16 or 18 or 24 or 26 or 28 or 30;
    }

    private static void EmitImmediateShift(
        ILGenerator il,
        LocalBuilder cpsr,
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
            EmitCpsrCarry(il, cpsr, carry);
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
            il.Emit(OpCodes.Stloc, destination);
            EmitCarryBit(il, source, carry, 31);
            return;
        }

        if (shiftType == 3 && shift == 0)
        {
            // RRX: rotate right through the old carry.
            EmitCpsrCarry(il, cpsr, temp);
            il.Emit(OpCodes.Ldloc, source);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Shr_Un);
            il.Emit(OpCodes.Ldloc, temp);
            il.Emit(OpCodes.Ldc_I4, 31);
            il.Emit(OpCodes.Shl);
            il.Emit(OpCodes.Or);
            il.Emit(OpCodes.Stloc, destination);
            EmitCarryBit(il, source, carry, 0);
            return;
        }

        switch (shiftType)
        {
            case 0:
                il.Emit(OpCodes.Ldloc, source);
                il.Emit(OpCodes.Ldc_I4, shift);
                il.Emit(OpCodes.Shl);
                il.Emit(OpCodes.Stloc, destination);
                EmitCarryBit(il, source, carry, 32 - shift);
                return;
            case 1:
                il.Emit(OpCodes.Ldloc, source);
                il.Emit(OpCodes.Ldc_I4, shift);
                il.Emit(OpCodes.Shr_Un);
                il.Emit(OpCodes.Stloc, destination);
                EmitCarryBit(il, source, carry, shift - 1);
                return;
            case 2:
                il.Emit(OpCodes.Ldloc, source);
                il.Emit(OpCodes.Ldc_I4, shift);
                il.Emit(OpCodes.Shr);
                il.Emit(OpCodes.Stloc, destination);
                EmitCarryBit(il, source, carry, shift - 1);
                return;
            default:
                il.Emit(OpCodes.Ldloc, source);
                il.Emit(OpCodes.Ldc_I4, shift);
                il.Emit(OpCodes.Shr_Un);
                il.Emit(OpCodes.Ldloc, source);
                il.Emit(OpCodes.Ldc_I4, 32 - shift);
                il.Emit(OpCodes.Shl);
                il.Emit(OpCodes.Or);
                il.Emit(OpCodes.Stloc, destination);
                EmitCarryBit(il, source, carry, shift - 1);
                return;
        }
    }

    private static void EmitCarryBit(
        ILGenerator il,
        LocalBuilder source,
        LocalBuilder carry,
        int bit)
    {
        il.Emit(OpCodes.Ldloc, source);
        il.Emit(OpCodes.Ldc_I4, bit);
        il.Emit(OpCodes.Shr_Un);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Stloc, carry);
    }

    private static void EmitSingleDataTransfer(
        ILGenerator il,
        LocalBuilder state,
        LocalBuilder user,
        LocalBuilder pc,
        LocalBuilder cpsr,
        LocalBuilder forceExit,
        uint instruction,
        uint blockStart,
        uint blockEnd,
        int ramSize)
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

        EmitLoadUserRelative(il, user, rn, pc, 8, address);

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
            if (!byteAccess && ramSize > 0)
            {
                Label slowLoad = il.DefineLabel();
                Label loadDone = il.DefineLabel();
                EmitRamFastPathTest(il, address, ramSize, slowLoad);

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, JitRamField);
                il.Emit(OpCodes.Ldloc, address);
                il.Emit(OpCodes.Ldc_I4, -4);
                il.Emit(OpCodes.And);
                il.Emit(OpCodes.Callvirt, JitRamRead32);
                il.Emit(OpCodes.Stloc, value);
                il.Emit(OpCodes.Br, loadDone);

                il.MarkLabel(slowLoad);
                EmitBusWordLoad(il, address, value);
                il.MarkLabel(loadDone);
            }
            else
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldloc, address);
                il.Emit(OpCodes.Call, byteAccess ? JitReadByte : JitReadWord);
                if (byteAccess)
                {
                    il.Emit(OpCodes.Conv_U4);
                }

                il.Emit(OpCodes.Stloc, value);
            }

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
                EmitStoreUserRegister(il, user, rn, baseAddress);
            }

            if (writeback && !preIndex)
            {
                EmitSyncCpsr(il, state, cpsr);
                il.Emit(OpCodes.Ldloc, state);
                il.Emit(OpCodes.Ldc_I4, rd);
                il.Emit(OpCodes.Ldloc, value);
                il.Emit(OpCodes.Call, JitLoadUser);
            }
            else
            {
                EmitStoreUserRegister(il, user, rd, value);
            }

            return;
        }

        if (writeback && !preIndex)
        {
            EmitSyncCpsr(il, state, cpsr);
            il.Emit(OpCodes.Ldloc, state);
            il.Emit(OpCodes.Ldc_I4, rd);
            il.Emit(OpCodes.Call, JitReadUser);
            il.Emit(OpCodes.Stloc, value);
        }
        else
        {
            EmitLoadUserRegister(il, user, rd, value);
        }

        // The interpreter writes PC+4 before the store; the bus may advance PC once more
        // for a CLIO PC-adjust write, so the slow path reloads the final value afterwards.
        EmitAddConstant(il, pc, 4);
        EmitStoreUserRegister(il, user, 15, pc);
        EmitAddConstant(il, pc, -4);

        if (byteAccess)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloc, address);
            il.Emit(OpCodes.Ldloc, value);
            il.Emit(OpCodes.Call, JitWriteByte);
            EmitOverlapExit(il, address, blockStart, blockEnd, forceExit);
        }
        else if (ramSize > 0)
        {
            Label slowStore = il.DefineLabel();
            Label storeDone = il.DefineLabel();
            EmitRamFastPathTest(il, address, ramSize, slowStore);

            il.Emit(OpCodes.Ldloc, address);
            il.Emit(OpCodes.Ldc_I4, -4);
            il.Emit(OpCodes.And);
            il.Emit(OpCodes.Stloc, address);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, JitRamField);
            il.Emit(OpCodes.Ldloc, address);
            il.Emit(OpCodes.Ldloc, value);
            il.Emit(OpCodes.Callvirt, JitRamWrite32);
            EmitOverlapExit(il, address, blockStart, blockEnd, forceExit);
            il.Emit(OpCodes.Br, storeDone);

            il.MarkLabel(slowStore);
            EmitBusWordStore(il, pc, user, address, value);
            EmitReturnToMachine(il, forceExit);
            il.MarkLabel(storeDone);
        }
        else
        {
            EmitBusWordStore(il, pc, user, address, value);
            EmitReturnToMachine(il, forceExit);
        }

        if ((writeback || !preIndex) && rn != 15)
        {
            EmitStoreUserRegister(il, user, rn, baseAddress);
        }
    }

    private static void EmitRamFastPathTest(
        ILGenerator il,
        LocalBuilder address,
        int ramSize,
        Label slowPath)
    {
        il.Emit(OpCodes.Ldloc, address);
        il.Emit(OpCodes.Ldc_I4, ramSize);
        il.Emit(OpCodes.Bge_Un, slowPath);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, JitRamField);
        il.Emit(OpCodes.Callvirt, JitRomOverlayGet);
        il.Emit(OpCodes.Brtrue, slowPath);
    }

    private static void EmitBusWordLoad(ILGenerator il, LocalBuilder address, LocalBuilder value)
    {
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, address);
        il.Emit(OpCodes.Call, JitReadWord);
        il.Emit(OpCodes.Stloc, value);
    }

    private static void EmitBusWordStore(
        ILGenerator il,
        LocalBuilder pc,
        LocalBuilder user,
        LocalBuilder address,
        LocalBuilder value)
    {
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, address);
        il.Emit(OpCodes.Ldloc, value);
        il.Emit(OpCodes.Call, JitWriteWord);

        EmitLoadUserRegister(il, user, 15, pc);
        EmitAddConstant(il, pc, -4);
    }

    private static void EmitOverlapExit(
        ILGenerator il,
        LocalBuilder address,
        uint blockStart,
        uint blockEnd,
        LocalBuilder forceExit)
    {
        Label outside = il.DefineLabel();
        il.Emit(OpCodes.Ldloc, address);
        il.Emit(OpCodes.Ldc_I4, unchecked((int)blockStart));
        il.Emit(OpCodes.Blt_Un, outside);
        il.Emit(OpCodes.Ldloc, address);
        il.Emit(OpCodes.Ldc_I4, unchecked((int)blockEnd));
        il.Emit(OpCodes.Bge_Un, outside);
        EmitSetForceExit(il, forceExit);
        il.MarkLabel(outside);
    }

    private static void EmitSetForceExit(ILGenerator il, LocalBuilder forceExit)
    {
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Stloc, forceExit);
    }

    private static void EmitReturnToMachine(ILGenerator il, LocalBuilder forceExit)
    {
        EmitSetForceExit(il, forceExit);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Stfld, JitReturnToMachineField);
    }

    private static void EmitSyncCpsr(ILGenerator il, LocalBuilder state, LocalBuilder cpsr)
    {
        il.Emit(OpCodes.Ldloc, state);
        il.Emit(OpCodes.Ldloc, cpsr);
        il.Emit(OpCodes.Callvirt, JitCpsrSet);
    }

    private static void EmitBranch(
        ILGenerator il,
        LocalBuilder state,
        LocalBuilder user,
        LocalBuilder pc,
        uint instruction)
    {
        if ((instruction & (1u << 24)) != 0)
        {
            EmitAddConstant(il, pc, 4);
            EmitStoreUserRegister(il, user, 14, pc);
            EmitAddConstant(il, pc, -4);
        }

        var offset = (int)(instruction & 0xffffff);
        if ((offset & 0x800000) != 0)
        {
            offset |= unchecked((int)0xff000000);
        }

        offset <<= 2;

        il.Emit(OpCodes.Ldloc, pc);
        il.Emit(OpCodes.Ldc_I4, offset + 8);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, pc);
    }

    private sealed class JitBlock
    {
        internal JitBlock(uint pc, uint codeEnd, JitBlockDelegate execute, int instructionCount)
        {
            Pc = pc;
            CodeEnd = codeEnd;
            Execute = execute;
            InstructionCount = instructionCount;
        }

        internal uint Pc { get; }

        internal uint CodeEnd { get; }

        internal JitBlockDelegate Execute { get; }

        internal int InstructionCount { get; }
    }
}
