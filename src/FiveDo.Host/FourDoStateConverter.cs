using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using FiveDo.Core;
using FiveDo.Madam;

namespace FiveDo.Host;

/// <summary>
/// Best-effort conversion between 4DO <c>.4dosav</c> states and 5DO save states.
/// </summary>
/// <remarks>
/// <para>
/// 4DO stores raw FreeDOCore component structures. 5DO stores the same architectural
/// state with explicit little-endian section payloads, so the conversion is a field
/// mapping rather than an emulation replay. Fields that 4DO does not persist (the
/// derived VDL frame, the CD-ROM drive state, controller attachments, and the DSP
/// instruction cache) are preserved from the base 5DO state on import and omitted on
/// export.
/// </para>
/// <para>
/// The format is versionless and compiler-layout dependent. Only the 32-bit and 64-bit
/// ARM layouts produced by current FreeDOCore builds are accepted.
/// </para>
/// </remarks>
public static class FourDoStateConverter
{
    private const int WordSize = sizeof(uint);
    private const int DspInstTraSize = 0x8000 * 2;
    private const int DspRegConvSize = 8 * 16 * 2;
    private const int DspBrCondTabSize = 32 * 32;
    private const int DspNMemSize = 2048 * 2;
    private const int DspIMemSize = 1024 * 2;
    private const int DspRegisterStageSize = 20;
    private const int DspInterpreterStageSize = 21;
    private const int DspCpuSupplySize = 16;
    private const int DspFixedTailSize = DspRegConvSize
        + DspBrCondTabSize
        + DspNMemSize
        + DspIMemSize
        + sizeof(int)
        + DspRegisterStageSize
        + DspInterpreterStageSize
        + sizeof(uint)
        + DspCpuSupplySize;

    private const int MadamRegisterCount = 2048 + 64;
    private const int MadamPlutCount = 32;
    private const int MadamPbusQueueSize = 256;
    private const int MadamLegacyPbusQueueSize = 20;
    private const int ClioRegisterCount = 65_536;
    private const int ClioInputFifoCount = 13;
    private const int ClioOutputFifoCount = 4;
    private const int XbusStatusFifoSize = 255;
    private const int XbusCommandFifoSize = 7;
    private const uint ClioDefaultRandomSeed = 0xDEADBEEF;

    /// <summary>
    /// Imports a 4DO state into a 5DO state, using a base 5DO state for the sections
    /// that 4DO does not persist.
    /// </summary>
    /// <param name="fourDoData">Complete <c>.4dosav</c> bytes.</param>
    /// <param name="baseFiveDoState">
    /// A 5DO state captured from a freshly reset machine with the intended BIOS. Its
    /// CD-ROM, derived frame, machine options, and controller chain are kept.
    /// </param>
    /// <param name="warnings">Receives human-readable conversion notes.</param>
    /// <returns>A complete 5DO save-state image.</returns>
    /// <exception cref="InvalidDataException">Thrown when the 4DO state is malformed.</exception>
    /// <exception cref="InvalidStateException">Thrown when the base 5DO state is malformed or incomplete.</exception>
    public static byte[] Import(
        ReadOnlySpan<byte> fourDoData,
        ReadOnlySpan<byte> baseFiveDoState,
        out IReadOnlyList<string> warnings)
    {
        FourDoSaveState fourDo = FourDoSaveState.Parse(fourDoData);
        FiveDoStateDocument document = FiveDoStateDocument.Parse(baseFiveDoState);
        var notes = new List<string>(fourDo.Warnings);
        foreach (uint id in RequiredBaseSections)
        {
            if (document.Find(id) is null)
            {
                throw new InvalidStateException(
                    $"The base 5DO state is missing the required section {StateSectionIds.GetName(id)} (0x{id:X8}).");
            }
        }

        document.Replace(StateSectionIds.Cpu, 1, WriteCpu(fourDo));
        document.Replace(StateSectionIds.Memory, 4, WriteMemory(fourDo));
        document.Replace(StateSectionIds.Madam, 4, WriteMadam(fourDo));
        document.Replace(StateSectionIds.Clio, 3, WriteClio(fourDo));
        document.Replace(StateSectionIds.Dsp, 1, WriteDsp(fourDo));
        document.Replace(StateSectionIds.Sport, 1, WriteSport(fourDo));
        document.Replace(StateSectionIds.Vdlp, 4, WriteVdlp(fourDo, notes));
        document.Replace(StateSectionIds.Quartz, 3, WriteQuartz(fourDo, notes));
        PatchXbus(document, fourDo);

        notes.Add(
            "4DO does not save the CD-ROM drive or derived video frame; those sections "
            + "were kept from the base 5DO state.");
        notes.Add(
            "4DO saves a raw 3 MB DRAM/VRAM image; the stock 5DO high-resolution mirror "
            + "planes were rebuilt from the VRAM bank.");
        notes.Add(
            "4DO saves no controller-device state; the base state's controller chain was kept.");
        notes.Add(
            "4DO saves a decoded DSP instruction cache and convolution tables; 5DO decodes "
            + "its DSP program from IMem, so those records were ignored.");
        warnings = notes;
        return document.Write();
    }

    /// <summary>Exports a 5DO state to a 4DO <c>.4dosav</c> image.</summary>
    /// <param name="fiveDoData">Complete 5DO save-state bytes.</param>
    /// <param name="warnings">Receives human-readable conversion notes.</param>
    /// <returns>A complete 4DO save-state image.</returns>
    /// <exception cref="InvalidStateException">Thrown when the 5DO state is malformed.</exception>
    /// <exception cref="NotSupportedException">Thrown when a section layout cannot be exported.</exception>
    public static byte[] Export(ReadOnlySpan<byte> fiveDoData, out IReadOnlyList<string> warnings)
    {
        FiveDoStateDocument document = FiveDoStateDocument.Parse(fiveDoData);
        var notes = new List<string>();
        FiveDoStateSection cpu = Require(document, StateSectionIds.Cpu);
        FiveDoStateSection memory = Require(document, StateSectionIds.Memory);
        FiveDoStateSection madam = Require(document, StateSectionIds.Madam);
        FiveDoStateSection clio = Require(document, StateSectionIds.Clio);
        FiveDoStateSection dsp = Require(document, StateSectionIds.Dsp);
        FiveDoStateSection sport = Require(document, StateSectionIds.Sport);
        FiveDoStateSection vdlp = Require(document, StateSectionIds.Vdlp);
        FiveDoStateSection quartz = Require(document, StateSectionIds.Quartz);
        FiveDoStateSection xbus = Require(document, StateSectionIds.Xbus);

        MemoryImage image = ParseMemory(memory, notes);
        var components = new List<byte[]>(8)
        {
            BuildArm(cpu, image, notes),
            BuildVdl(vdlp, notes),
            BuildDsp(dsp, notes),
            BuildClio(clio, notes),
            BuildQuartz(quartz, notes),
            BuildSport(sport),
            BuildMadam(madam, notes),
            BuildXbus(xbus, notes),
        };

        var header = new byte[FourDoSaveState.HeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(header, FourDoSaveState.Magic);
        var offset = FourDoSaveState.HeaderSize;
        for (var i = 0; i < components.Count; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan((i + 1) * sizeof(int)), offset);
            offset += components[i].Length;
        }

        BinaryPrimitives.WriteInt32LittleEndian(
            header.AsSpan((components.Count + 1) * sizeof(int)),
            offset);

        var result = new byte[offset];
        header.CopyTo(result, 0);
        offset = FourDoSaveState.HeaderSize;
        foreach (byte[] component in components)
        {
            component.CopyTo(result, offset);
            offset += component.Length;
        }

        notes.Add(
            "4DO does not store the CD-ROM drive, derived video frame, machine options, "
            + "or controller attachments; those were omitted from the export.");
        notes.Add(
            "The ARM pointer fields are written as zero; 4DO ignores them and rebinds its "
            + "own buffers when loading.");
        warnings = notes;
        return result;
    }

    private static readonly uint[] RequiredBaseSections =
    [
        StateSectionIds.Cpu,
        StateSectionIds.Memory,
        StateSectionIds.Madam,
        StateSectionIds.Clio,
        StateSectionIds.Dsp,
        StateSectionIds.Sport,
        StateSectionIds.Vdlp,
        StateSectionIds.Xbus,
        StateSectionIds.Cdrom,
        StateSectionIds.Machine,
    ];

    private static FiveDoStateSection Require(FiveDoStateDocument document, uint id)
    {
        return document.Find(id)
            ?? throw new InvalidStateException(
                $"The 5DO state is missing the {StateSectionIds.GetName(id)} section (0x{id:X8}).");
    }

    private static byte[] WriteCpu(FourDoSaveState fourDo)
    {
        ReadOnlySpan<byte> registers = fourDo.ArmRegisters;
        var writer = new StateWriter(220);
        var offset = 0;
        // USER[16], CASH[7], SVC[2], ABT[2], FIQ[7], IRQ[2], UND[2], SPSR[6].
        foreach (var count in CpuRegisterGroups)
        {
            for (var i = 0; i < count; i++)
            {
                writer.WriteUInt32(BinaryPrimitives.ReadUInt32LittleEndian(registers[offset..]));
                offset += WordSize;
            }
        }

        writer.WriteUInt32(BinaryPrimitives.ReadUInt32LittleEndian(registers[offset..]));
        offset += WordSize;
        var fiqPending = registers[offset++] != 0;
        var secondRom = registers[offset++] != 0;
        var dataAbort = registers[offset] != 0;
        writer.WriteBoolean(fiqPending);
        writer.WriteBoolean(secondRom);
        writer.WriteBoolean(dataAbort);
        // The remaining interpreter scratch (carry latch and LDM/STM temporaries) is not
        // part of the architectural state and starts clear at a frame boundary.
        writer.WriteUInt32(0);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        return writer.ToArray();
    }

    private static readonly int[] CpuRegisterGroups = [16, 7, 2, 2, 7, 2, 2, 6];

    private static byte[] WriteMemory(FourDoSaveState fourDo)
    {
        var writer = new StateWriter(EmulatorConstants.StockHiresRamSize + EmulatorConstants.RomSize + EmulatorConstants.NvramSize);
        writer.WriteByte(fourDo.ArmRegisters[181] != 0 ? (byte)1 : (byte)0);
        writer.WriteBoolean(false);
        writer.WriteBoolean(false);
        writer.WriteByte((byte)MemoryConfiguration.Dram2MbVram1Mb);
        writer.WriteInt32(EmulatorConstants.RamAllocationSize);
        writer.WriteInt32(EmulatorConstants.StockHiresRamSize);

        var ram = new byte[EmulatorConstants.StockHiresRamSize];
        CopySwappedWords(fourDo.ArmRam, ram.AsSpan(0, EmulatorConstants.RamSize));
        var vram = ram.AsSpan(EmulatorConstants.VramBase, EmulatorConstants.VramSize);
        for (var plane = 0; plane < EmulatorConstants.RamMirrorCount; plane++)
        {
            vram.CopyTo(ram.AsSpan(EmulatorConstants.RamSize + (plane * EmulatorConstants.VramSize)));
        }

        writer.WriteBytes(ram);

        var rom = new byte[EmulatorConstants.RomSize];
        CopySwappedWords(fourDo.ArmRom, rom);
        writer.WriteBytes(rom);
        writer.WriteBytes(fourDo.ArmNvram);
        return writer.ToArray();
    }

    private static byte[] WriteMadam(FourDoSaveState fourDo)
    {
        ReadOnlySpan<byte> source = fourDo.Madam;
        var writer = new StateWriter(FourDoSaveState.MadamSize + 320);
        var offset = 0;
        for (var i = 0; i < MadamRegisterCount; i++)
        {
            writer.WriteUInt32(BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]));
            offset += WordSize;
        }

        for (var i = 0; i < MadamPlutCount; i++)
        {
            writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(source[offset..]));
            offset += sizeof(ushort);
        }

        Span<byte> pbus = stackalloc byte[MadamPbusQueueSize];
        pbus.Fill(0xFF);
        source.Slice(offset, MadamLegacyPbusQueueSize).CopyTo(pbus);
        offset += MadamLegacyPbusQueueSize;
        writer.WriteBytes(pbus);

        var readModulo = BinaryPrimitives.ReadInt32LittleEndian(source[offset..]);
        offset += sizeof(int);
        var writeModulo = BinaryPrimitives.ReadInt32LittleEndian(source[offset..]);
        offset += sizeof(int);
        var fsm = BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]);
        writer.WriteInt32(readModulo);
        writer.WriteInt32(writeModulo);
        writer.WriteUInt32(fsm);
        writer.WriteDouble(0);
        writer.WriteDouble(0);
        writer.WriteDouble(0);
        writer.WriteDouble(0);
        writer.WriteUInt32(fsm == (uint)CelFsmState.InProcess ? 1u : 0u);
        writer.WriteUInt64(0);
        writer.WriteUInt64(0);
        writer.WriteBoolean(false);
        return writer.ToArray();
    }

    private static byte[] WriteClio(FourDoSaveState fourDo)
    {
        ReadOnlySpan<byte> source = fourDo.Clio;
        var writer = new StateWriter(FourDoSaveState.ClioSize + 64);
        var offset = 0;
        for (var i = 0; i < ClioRegisterCount; i++)
        {
            writer.WriteUInt32(BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]));
            offset += WordSize;
        }

        writer.WriteInt32(BinaryPrimitives.ReadInt32LittleEndian(source[offset..]));
        offset += sizeof(int);
        writer.WriteInt32(BinaryPrimitives.ReadInt32LittleEndian(source[offset..]));
        offset += sizeof(int);
        writer.WriteInt32(BinaryPrimitives.ReadInt32LittleEndian(source[offset..]));
        offset += sizeof(int);

        // FreeDOCore clears its timer accumulator in _clio_Load. The CLIO random seed
        // is a 5DO diagnostic extension with no 4DO equivalent.
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteUInt32(0);
        writer.WriteUInt32(ClioDefaultRandomSeed);

        for (var i = 0; i < ClioInputFifoCount; i++)
        {
            writer.WriteInt32(BinaryPrimitives.ReadInt32LittleEndian(source[offset..]));
            offset += sizeof(int);
        }

        for (var i = 0; i < ClioOutputFifoCount; i++)
        {
            writer.WriteInt32(BinaryPrimitives.ReadInt32LittleEndian(source[offset..]));
            offset += sizeof(int);
        }

        for (var i = 0; i < ClioInputFifoCount; i++)
        {
            WriteFifo(writer, source, ref offset);
        }

        for (var i = 0; i < ClioOutputFifoCount; i++)
        {
            WriteFifo(writer, source, ref offset);
        }

        return writer.ToArray();
    }

    private static void WriteFifo(StateWriter writer, ReadOnlySpan<byte> source, ref int offset)
    {
        writer.WriteUInt32(BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]));
        offset += WordSize;
        writer.WriteInt32(BinaryPrimitives.ReadInt32LittleEndian(source[offset..]));
        offset += sizeof(int);
        writer.WriteUInt32(BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]));
        offset += WordSize;
        writer.WriteInt32(BinaryPrimitives.ReadInt32LittleEndian(source[offset..]));
        offset += sizeof(int);
    }

    private static byte[] WriteDsp(FourDoSaveState fourDo)
    {
        ReadOnlySpan<byte> source = fourDo.Dsp;
        var tail = source.Length - DspFixedTailSize;
        var registerStageOffset = tail
            + DspRegConvSize
            + DspBrCondTabSize
            + DspNMemSize
            + DspIMemSize
            + sizeof(int);
        var writer = new StateWriter(DspFixedTailSize + 128);
        var registers = source.Slice(registerStageOffset, DspRegisterStageSize);
        writer.WriteUInt32(BinaryPrimitives.ReadUInt32LittleEndian(registers));
        writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(registers[4..]));
        writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(registers[6..]));
        writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(registers[8..]));
        writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(registers[10..]));
        writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(registers[12..]));
        writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(registers[14..]));
        writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(registers[16..]));
        writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(registers[18..]));

        var interpreter = source.Slice(registerStageOffset + DspRegisterStageSize, DspInterpreterStageSize);
        writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(interpreter));
        writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(interpreter[2..]));
        writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(interpreter[4..]));
        writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(interpreter[6..]));
        writer.WriteInt32(BinaryPrimitives.ReadInt32LittleEndian(interpreter[8..]));
        writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(interpreter[14..]));
        writer.WriteUInt16(BinaryPrimitives.ReadUInt16LittleEndian(interpreter[16..]));
        writer.WriteByte(interpreter[18]);
        writer.WriteBoolean(interpreter[19] != 0);
        writer.WriteBoolean(interpreter[20] != 0);
        writer.WriteUInt32(BinaryPrimitives.ReadUInt32LittleEndian(source));

        var registerIndexOffset = tail + DspRegConvSize + DspBrCondTabSize + DspNMemSize + DspIMemSize;
        writer.WriteInt32(BinaryPrimitives.ReadInt32LittleEndian(source[registerIndexOffset..]));
        writer.WriteUInt32(
            BinaryPrimitives.ReadUInt32LittleEndian(source[(source.Length - DspCpuSupplySize - sizeof(uint))..]));
        var supplyOffset = source.Length - DspCpuSupplySize;
        for (var i = 0; i < DspCpuSupplySize; i++)
        {
            writer.WriteBoolean(source[supplyOffset + i] != 0);
        }

        writer.WriteBytes(source.Slice(tail + DspRegConvSize + DspBrCondTabSize, DspNMemSize));
        writer.WriteBytes(source.Slice(tail + DspRegConvSize + DspBrCondTabSize + DspNMemSize, DspIMemSize));
        return writer.ToArray();
    }

    private static byte[] WriteSport(FourDoSaveState fourDo)
    {
        ReadOnlySpan<byte> source = fourDo.Sport;
        var writer = new StateWriter(FourDoSaveState.SportSize);
        writer.WriteUInt32(BinaryPrimitives.ReadUInt32LittleEndian(source));
        writer.WriteUInt32(BinaryPrimitives.ReadUInt32LittleEndian(source[4..]));
        writer.WriteUInt32(BinaryPrimitives.ReadUInt32LittleEndian(source[8..]));
        return writer.ToArray();
    }

    private static byte[] WriteVdlp(FourDoSaveState fourDo, List<string> notes)
    {
        ReadOnlySpan<byte> source = fourDo.Vdl;
        var writer = new StateWriter(160);
        writer.WriteBytes(source[..96]);
        var background = BinaryPrimitives.ReadUInt32LittleEndian(source[96..]);
        var headVdl = BinaryPrimitives.ReadUInt32LittleEndian(source[100..]);
        var modulo = BinaryPrimitives.ReadUInt32LittleEndian(source[104..]);
        var currentVdl = BinaryPrimitives.ReadUInt32LittleEndian(source[108..]);
        var currentBitmap = BinaryPrimitives.ReadUInt32LittleEndian(source[112..]);
        var previousBitmap = BinaryPrimitives.ReadUInt32LittleEndian(source[116..]);
        var outControl = BinaryPrimitives.ReadUInt32LittleEndian(source[120..]);
        var clutDma = BinaryPrimitives.ReadUInt32LittleEndian(source[124..]);
        var lineDelay = BinaryPrimitives.ReadInt32LittleEndian(source[128..]);

        writer.WriteUInt32(background);
        writer.WriteInt32(0);
        writer.WriteUInt32(currentVdl);
        writer.WriteUInt32(headVdl);
        writer.WriteUInt32(currentVdl);
        writer.WriteUInt32(modulo);
        writer.WriteUInt32(currentBitmap);
        writer.WriteUInt32(previousBitmap);
        writer.WriteUInt32(outControl);
        writer.WriteUInt32(clutDma);
        writer.WriteInt32(lineDelay);
        writer.WriteInt32(lineDelay > 0 ? lineDelay : 0);
        notes.Add(
            "4DO does not store a separate successor VDL cursor; 5DO resumes from the "
            + "current cursor recorded in the state.");
        return writer.ToArray();
    }

    private static byte[] WriteQuartz(FourDoSaveState fourDo, List<string> notes)
    {
        ReadOnlySpan<byte> source = fourDo.Quartz;
        var accArm = BinaryPrimitives.ReadUInt32LittleEndian(source);
        var accDsp = BinaryPrimitives.ReadUInt32LittleEndian(source[4..]);
        var accVdl = BinaryPrimitives.ReadUInt32LittleEndian(source[8..]);
        var tCount = BinaryPrimitives.ReadUInt32LittleEndian(source[12..]);
        var line = BinaryPrimitives.ReadUInt32LittleEndian(source[20..]);
        var dspRemainder = (uint)(((accDsp & 0xFFFFFFu) * (ulong)EmulatorConstants.ArmClock) >> 24);
        var writer = new StateWriter(48);
        writer.WriteUInt32(accArm);
        writer.WriteUInt32(dspRemainder);
        writer.WriteUInt32(accVdl);
        writer.WriteUInt32(tCount);
        writer.WriteUInt32(line);
        writer.WriteUInt64(0);
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);
        notes.Add(
            "4DO stores 8.24 DSP/VDL fractions and no exact timer-slot accumulator; 5DO "
            + "converts the fractions and restarts the timer slot at zero.");
        return writer.ToArray();
    }

    private static void PatchXbus(FiveDoStateDocument document, FourDoSaveState fourDo)
    {
        FiveDoStateSection section = document.Find(StateSectionIds.Xbus)
            ?? throw new InvalidStateException("The base 5DO state has no XBUS section.");
        var payload = (byte[])section.Payload.Clone();
        if (payload.Length < 6)
        {
            throw new InvalidStateException("The base 5DO XBUS section is truncated.");
        }

        ReadOnlySpan<byte> source = fourDo.Xbus;
        payload[0] = source[0];
        payload[1] = source[1];
        payload[2] = source[2];
        payload[3] = source[3];
        var statusLengthOffset = 4 + XbusStatusFifoSize;
        payload[4] = source[statusLengthOffset];
        payload[5] = source[statusLengthOffset + 1 + XbusCommandFifoSize];
        source.Slice(4, XbusStatusFifoSize)
            .CopyTo(payload.AsSpan(6, Math.Min(XbusStatusFifoSize, payload.Length - 6)));
        var commandLength = Math.Min(XbusCommandFifoSize, payload.Length - 6 - XbusStatusFifoSize);
        if (commandLength > 0)
        {
            source.Slice(statusLengthOffset + 1, commandLength)
                .CopyTo(payload.AsSpan(6 + XbusStatusFifoSize, commandLength));
        }

        document.Replace(StateSectionIds.Xbus, section.Version, payload);
    }

    private static MemoryImage ParseMemory(FiveDoStateSection section, List<string> notes)
    {
        var reader = new StateReader(section.Payload);
        if (section.Version < 4)
        {
            throw new NotSupportedException(
                $"4DO export requires a version-4 memory section; the state has version {section.Version}.");
        }

        _ = reader.ReadByte();
        _ = reader.ReadBoolean();
        _ = reader.ReadBoolean();
        var configuration = (MemoryConfiguration)reader.ReadByte();
        var backingSize = reader.ReadInt32();
        var storedSize = reader.ReadInt32();
        if (backingSize != EmulatorConstants.RamAllocationSize)
        {
            throw new InvalidStateException(
                $"The memory section declares a {backingSize}-byte backing store; "
                + $"expected {EmulatorConstants.RamAllocationSize}.");
        }

        if (storedSize < 0 || storedSize > backingSize || (storedSize & 3) != 0)
        {
            throw new InvalidStateException($"The memory section stored size {storedSize} is invalid.");
        }

        var ram = new byte[storedSize];
        reader.ReadBytes(ram);
        var rom = new byte[EmulatorConstants.RomSize];
        reader.ReadBytes(rom);
        var nvram = new byte[EmulatorConstants.NvramSize];
        reader.ReadBytes(nvram);
        if (configuration != MemoryConfiguration.Dram2MbVram1Mb)
        {
            notes.Add(
                $"The state uses the {configuration} memory layout; 4DO always stores the "
                + "stock 2 MB DRAM / 1 MB VRAM image, so expanded DRAM or VRAM was not exported.");
        }

        if (storedSize < EmulatorConstants.RamSize)
        {
            throw new InvalidStateException(
                $"The memory section stores only {storedSize} bytes; at least "
                + $"{EmulatorConstants.RamSize} are needed for a 4DO export.");
        }

        return new MemoryImage(ram, rom, nvram);
    }

    private static byte[] BuildArm(
        FiveDoStateSection cpu,
        MemoryImage memory,
        List<string> notes)
    {
        if (cpu.Version != 1)
        {
            throw new NotSupportedException($"Unsupported CPU state version {cpu.Version}.");
        }

        var reader = new StateReader(cpu.Payload);
        var registers = new byte[FourDoSaveState.ArmRegisterSize];
        var offset = 0;
        foreach (var count in CpuRegisterGroups)
        {
            for (var i = 0; i < count; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(registers.AsSpan(offset), reader.ReadUInt32());
                offset += WordSize;
            }
        }

        BinaryPrimitives.WriteUInt32LittleEndian(registers.AsSpan(offset), reader.ReadUInt32());
        offset += WordSize;
        registers[offset++] = reader.ReadBoolean() ? (byte)1 : (byte)0;
        registers[offset++] = reader.ReadBoolean() ? (byte)1 : (byte)0;
        registers[offset] = reader.ReadBoolean() ? (byte)1 : (byte)0;

        const int pointerSize = 3 * sizeof(uint);
        var result = new byte[pointerSize + FourDoSaveState.ArmRegisterSize
            + FourDoSaveState.ArmRamSize + FourDoSaveState.ArmRomSize + FourDoSaveState.ArmNvramSize];
        registers.CopyTo(result, pointerSize);
        var ramOffset = pointerSize + FourDoSaveState.ArmRegisterSize;
        CopySwappedWords(
            memory.Ram.AsSpan(0, FourDoSaveState.ArmRamSize),
            result.AsSpan(ramOffset, FourDoSaveState.ArmRamSize));
        CopySwappedWords(memory.Rom, result.AsSpan(ramOffset + FourDoSaveState.ArmRamSize, FourDoSaveState.ArmRomSize));
        memory.Nvram.CopyTo(result, ramOffset + FourDoSaveState.ArmRamSize + FourDoSaveState.ArmRomSize);
        notes.Add(
            $"Exported the {FourDoSaveState.ArmRegisterSize + (3 * sizeof(uint))}-byte 32-bit ARM "
            + "structure (4DO's reference build is x86).");
        return result;
    }

    private static byte[] BuildVdl(FiveDoStateSection section, List<string> notes)
    {
        if (section.Version < 4)
        {
            throw new NotSupportedException($"4DO export requires a version-4 VDLP section; found {section.Version}.");
        }

        var reader = new StateReader(section.Payload);
        var result = new byte[FourDoSaveState.VdlSize];
        reader.ReadBytes(result.AsSpan(0, 96));
        var background = reader.ReadUInt32();
        var forceLine = reader.ReadInt32();
        var currentVdl = reader.ReadUInt32();
        var headVdl = reader.ReadUInt32();
        var nextVdl = reader.ReadUInt32();
        var modulo = reader.ReadUInt32();
        var currentBitmap = reader.ReadUInt32();
        var previousBitmap = reader.ReadUInt32();
        var outControl = reader.ReadUInt32();
        var clutDma = reader.ReadUInt32();
        var lineDelay = reader.ReadInt32();
        _ = reader.ReadInt32();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(96), background);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(100), headVdl);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(104), modulo);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(108), currentVdl);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(112), currentBitmap);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(116), previousBitmap);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(120), outControl);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(124), clutDma);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(128), lineDelay);
        if (forceLine != 0 || nextVdl != currentVdl)
        {
            notes.Add("4DO has no force-line or successor-cursor field; both were omitted.");
        }

        return result;
    }

    private static byte[] BuildDsp(FiveDoStateSection section, List<string> notes)
    {
        if (section.Version != 1)
        {
            throw new NotSupportedException($"Unsupported DSP state version {section.Version}.");
        }

        var reader = new StateReader(section.Payload);
        var pc = reader.ReadUInt32();
        var noise = reader.ReadUInt16();
        var audioStatus = reader.ReadUInt16();
        var semaStatus = reader.ReadUInt16();
        var semaData = reader.ReadUInt16();
        var pcnt = reader.ReadUInt16();
        var prld = reader.ReadUInt16();
        var audCnt = reader.ReadUInt16();
        var interrupt = reader.ReadUInt16();
        var mult1 = reader.ReadUInt16();
        var mult2 = reader.ReadUInt16();
        var alu1 = reader.ReadUInt16();
        var alu2 = reader.ReadUInt16();
        var bs = reader.ReadInt32();
        var opMask = reader.ReadUInt16();
        var writeback = reader.ReadUInt16();
        var requests = reader.ReadByte();
        var running = reader.ReadBoolean();
        var genFiq = reader.ReadBoolean();
        var rbase = reader.ReadUInt32();
        var regi = reader.ReadInt32();
        var seed = reader.ReadUInt32();
        var supply = new byte[DspCpuSupplySize];
        for (var i = 0; i < supply.Length; i++)
        {
            supply[i] = reader.ReadBoolean() ? (byte)1 : (byte)0;
        }

        var nmem = new byte[DspNMemSize];
        reader.ReadBytes(nmem);
        var imem = new byte[DspIMemSize];
        reader.ReadBytes(imem);

        var result = new byte[4 + DspInstTraSize + DspFixedTailSize];
        var offset = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), rbase);
        offset += 4 + DspInstTraSize;
        // REGCONV and BRCONDTAB are decoded caches in 4DO and are rebuilt by 5DO.
        offset += DspRegConvSize + DspBrCondTabSize;
        nmem.CopyTo(result, offset);
        offset += DspNMemSize;
        imem.CopyTo(result, offset);
        offset += DspIMemSize;
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), regi);
        offset += sizeof(int);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), pc);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 4), noise);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 6), audioStatus);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 8), semaStatus);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 10), semaData);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 12), pcnt);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 14), prld);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 16), audCnt);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 18), interrupt);
        offset += DspRegisterStageSize;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset), mult1);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 2), mult2);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 4), alu1);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 6), alu2);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset + 8), bs);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 12), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 14), opMask);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 16), writeback);
        result[offset + 18] = requests;
        result[offset + 19] = running ? (byte)1 : (byte)0;
        result[offset + 20] = genFiq ? (byte)1 : (byte)0;
        offset += DspInterpreterStageSize;
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), seed);
        offset += sizeof(uint);
        supply.CopyTo(result, offset);
        notes.Add(
            "4DO's decoded DSP instruction cache, register-conversion table, and branch "
            + "condition table were zeroed; 5DO rebuilds them from IMem at run time.");
        return result;
    }

    private static byte[] BuildClio(FiveDoStateSection section, List<string> notes)
    {
        if (section.Version < 3)
        {
            throw new NotSupportedException($"4DO export requires a version-3 CLIO section; found {section.Version}.");
        }

        var reader = new StateReader(section.Payload);
        var registers = new uint[ClioRegisterCount];
        for (var i = 0; i < registers.Length; i++)
        {
            registers[i] = reader.ReadUInt32();
        }

        var dspWord1 = reader.ReadInt32();
        var dspWord2 = reader.ReadInt32();
        var dspAddress = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadUInt32();
        var randomSeed = reader.ReadUInt32();
        var inputPointers = new int[ClioInputFifoCount];
        var outputPointers = new int[ClioOutputFifoCount];
        var inputFifos = new Fifo[ClioInputFifoCount];
        var outputFifos = new Fifo[ClioOutputFifoCount];
        for (var i = 0; i < inputPointers.Length; i++)
        {
            inputPointers[i] = reader.ReadInt32();
            inputFifos[i] = ReadFifo(reader);
        }

        for (var i = 0; i < outputPointers.Length; i++)
        {
            outputPointers[i] = reader.ReadInt32();
            outputFifos[i] = ReadFifo(reader);
        }

        var result = new byte[FourDoSaveState.ClioSize];
        var offset = 0;
        foreach (var value in registers)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), value);
            offset += WordSize;
        }

        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), dspWord1);
        offset += sizeof(int);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), dspWord2);
        offset += sizeof(int);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), dspAddress);
        offset += sizeof(int);
        foreach (var value in inputPointers)
        {
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), value);
            offset += sizeof(int);
        }

        foreach (var value in outputPointers)
        {
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), value);
            offset += sizeof(int);
        }

        foreach (Fifo fifo in inputFifos)
        {
            WriteFifo(result, ref offset, fifo);
        }

        foreach (Fifo fifo in outputFifos)
        {
            WriteFifo(result, ref offset, fifo);
        }

        if (randomSeed != ClioDefaultRandomSeed)
        {
            notes.Add(
                "The CLIO PRNG seed is a 5DO diagnostic extension and has no 4DO field; "
                + "4DO will restart its own sequence.");
        }

        return result;
    }

    private static Fifo ReadFifo(StateReader reader)
    {
        return new Fifo(reader.ReadUInt32(), reader.ReadInt32(), reader.ReadUInt32(), reader.ReadInt32());
    }

    private static void WriteFifo(byte[] destination, ref int offset, Fifo fifo)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset), fifo.StartAddress);
        offset += WordSize;
        BinaryPrimitives.WriteInt32LittleEndian(destination.AsSpan(offset), fifo.StartLength);
        offset += sizeof(int);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset), fifo.NextAddress);
        offset += WordSize;
        BinaryPrimitives.WriteInt32LittleEndian(destination.AsSpan(offset), fifo.NextLength);
        offset += sizeof(int);
    }

    private static byte[] BuildQuartz(FiveDoStateSection section, List<string> notes)
    {
        var reader = new StateReader(section.Payload);
        var accArm = reader.ReadUInt32();
        var dsp = reader.ReadUInt32();
        var legacyVdl = reader.ReadUInt32();
        var tCount = reader.ReadUInt32();
        var line = reader.ReadUInt32();
        if (section.Version >= 2)
        {
            // 5DO stores an exact ARM-cycle remainder; 4DO stores an 8.24 sample fraction.
            dsp = (uint)(((ulong)dsp << 24) / EmulatorConstants.ArmClock);
        }

        var result = new byte[FourDoSaveState.QuartzSize];
        BinaryPrimitives.WriteUInt32LittleEndian(result, accArm);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), dsp);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), legacyVdl);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), tCount);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16), EmulatorConstants.VdlClock);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(20), line);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(24), EmulatorConstants.VdlLinesPerHalfFrame);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(28), EmulatorConstants.VdlFrameLines);
        if (section.Version >= 3)
        {
            notes.Add(
                "The exact timer-slot accumulator is a 5DO extension and has no 4DO field; "
                + "4DO restarts its timer slot from zero.");
        }

        return result;
    }

    private static byte[] BuildSport(FiveDoStateSection section)
    {
        var reader = new StateReader(section.Payload);
        var result = new byte[FourDoSaveState.SportSize];
        BinaryPrimitives.WriteUInt32LittleEndian(result, reader.ReadUInt32());
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), reader.ReadUInt32());
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), reader.ReadUInt32());
        return result;
    }

    private static byte[] BuildMadam(FiveDoStateSection section, List<string> notes)
    {
        if (section.Version < 4)
        {
            throw new NotSupportedException($"4DO export requires a version-4 MADAM section; found {section.Version}.");
        }

        var reader = new StateReader(section.Payload);
        var result = new byte[FourDoSaveState.MadamSize];
        var offset = 0;
        for (var i = 0; i < MadamRegisterCount; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), reader.ReadUInt32());
            offset += WordSize;
        }

        for (var i = 0; i < MadamPlutCount; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset), reader.ReadUInt16());
            offset += sizeof(ushort);
        }

        var pbus = new byte[MadamPbusQueueSize];
        reader.ReadBytes(pbus);
        pbus.AsSpan(0, MadamLegacyPbusQueueSize).CopyTo(result.AsSpan(offset));
        offset += MadamLegacyPbusQueueSize;
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), reader.ReadInt32());
        offset += sizeof(int);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), reader.ReadInt32());
        offset += sizeof(int);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), reader.ReadUInt32());
        _ = reader.ReadDouble();
        _ = reader.ReadDouble();
        _ = reader.ReadDouble();
        _ = reader.ReadDouble();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt64();
        _ = reader.ReadUInt64();
        _ = reader.ReadBoolean();
        notes.Add(
            "MADAM matrix result registers and CEL busy-cycle counters are 5DO timing "
            + "extensions; 4DO has no fields for them.");
        return result;
    }

    private static byte[] BuildXbus(FiveDoStateSection section, List<string> notes)
    {
        var reader = new StateReader(section.Payload);
        var result = new byte[FourDoSaveState.XbusSize];
        result[0] = reader.ReadByte();
        result[1] = reader.ReadByte();
        result[2] = reader.ReadByte();
        result[3] = reader.ReadByte();
        var statusLength = reader.ReadByte();
        var commandPointer = reader.ReadByte();
        reader.ReadBytes(result.AsSpan(4, XbusStatusFifoSize));
        reader.ReadBytes(result.AsSpan(4 + XbusStatusFifoSize + 1, XbusCommandFifoSize));
        result[4 + XbusStatusFifoSize] = statusLength;
        result[4 + XbusStatusFifoSize + 1 + XbusCommandFifoSize] = commandPointer;
        notes.Add("4DO input devices are host-side; attached controller state was not exported.");
        return result;
    }

    private static void CopySwappedWords(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length < destination.Length)
        {
            throw new InvalidDataException(
                $"A 4DO memory image is {source.Length} bytes; {destination.Length} were requested.");
        }

        for (var i = 0; i < destination.Length; i += WordSize)
        {
            destination[i] = source[i + 3];
            destination[i + 1] = source[i + 2];
            destination[i + 2] = source[i + 1];
            destination[i + 3] = source[i];
        }
    }

    private sealed record MemoryImage(byte[] Ram, byte[] Rom, byte[] Nvram);

    private sealed record Fifo(uint StartAddress, int StartLength, uint NextAddress, int NextLength);
}
