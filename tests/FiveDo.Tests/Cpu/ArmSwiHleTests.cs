using FiveDo.Core;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Cpu;

public sealed class ArmSwiHleTests
{
    private const uint VectorAddress = 0x2000;
    private const uint MatrixAddress = 0x2100;
    private const uint DestinationAddress = 0x2200;
    private const uint SecondAddress = 0x2300;

    [Fact]
    public void MulVec3Mat33_AppliesTheReferenceRowMajorLayout()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x10000, -0x20000, 0x8000);
        WriteIdentity33(rig, MatrixAddress);
        LoadSwi(rig, 0x50000, DestinationAddress, VectorAddress, MatrixAddress);

        rig.Run(1);

        Assert.Equal(0x10000, ReadWord(rig, DestinationAddress));
        Assert.Equal(-0x20000, ReadWord(rig, DestinationAddress + 4));
        Assert.Equal(0x8000, ReadWord(rig, DestinationAddress + 8));
        Assert.Equal(1, rig.Cpu.SwiHleInstructions);
        Assert.Equal(0x1004u, rig.Cpu.ProgramCounter);
    }

    [Fact]
    public void MulMat33Mat33_MultipliesByIdentity()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x10000, 0x20000, 0x30000, 0x40000, 0x50000, 0x60000, 0x70000, 0x80000, 0x90000);
        WriteIdentity33(rig, MatrixAddress);
        LoadSwi(rig, 0x50001, DestinationAddress, VectorAddress, MatrixAddress);

        rig.Run(1);

        for (var i = 0; i < 9; i++)
        {
            Assert.Equal((i + 1) * 0x10000, ReadWord(rig, DestinationAddress + ((uint)i * 4)));
        }
    }

    [Fact]
    public void MulManyVec3Mat33_MultipliesEveryRecord()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x10000, 0, 0, 0, 0x20000, 0);
        WriteIdentity33(rig, MatrixAddress);
        LoadSwi(rig, 0x50002, DestinationAddress, VectorAddress, MatrixAddress);
        rig.Cpu.WriteRegister(3, 2);

        Assert.Equal(2u, rig.Cpu.ReadRegister(3));
        Assert.Equal(0x20000, ReadWord(rig, VectorAddress + 16));
        rig.Run(1);

        Assert.Equal(1, rig.Cpu.SwiHleInstructions);
        Assert.Equal(0x10000, ReadWord(rig, DestinationAddress));
        Assert.Equal(0x20000, ReadWord(rig, DestinationAddress + 16));
    }

    [Fact]
    public void MulManyVec3Mat33_MatchesTheOperaReference()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(
            rig,
            VectorAddress,
            0x10000,
            0x20000,
            0x30000,
            -0x10000,
            0x8000,
            0);
        WriteWords(
            rig,
            MatrixAddress,
            0x10000,
            0x8000,
            -0x4000,
            0x18000,
            -0x10000,
            0x3000,
            -0x20000,
            0x4000,
            0x10000);
        LoadSwi(rig, 0x50002, DestinationAddress, VectorAddress, MatrixAddress);
        rig.Cpu.WriteRegister(3, 2);

        rig.Run(1);

        int[] expected =
        [
            -131072, -49152, 204800,
            -16384, -65536, 22528,
        ];
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], ReadWord(rig, DestinationAddress + ((uint)i * 4)));
        }
    }

    [Fact]
    public void MulManyF16_MultipliesEveryElement()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x20000, -0x20000);
        WriteWords(rig, SecondAddress, 0x30000, 0x40000);
        LoadSwi(rig, 0x50005, DestinationAddress, VectorAddress, SecondAddress);
        rig.Cpu.WriteRegister(3, 2);

        rig.Run(1);

        Assert.Equal(0x60000, ReadWord(rig, DestinationAddress));
        Assert.Equal(-0x80000, ReadWord(rig, DestinationAddress + 4));
    }

    [Fact]
    public void MulScalerF16_AppliesTheScaler()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x10000, -0x10000);
        LoadSwi(rig, 0x50006, DestinationAddress, VectorAddress, 0);
        rig.Cpu.WriteRegister(2, 0x20000);
        rig.Cpu.WriteRegister(3, 2);

        rig.Run(1);

        Assert.Equal(0x20000, ReadWord(rig, DestinationAddress));
        Assert.Equal(-0x20000, ReadWord(rig, DestinationAddress + 4));
    }

    [Fact]
    public void MulVec4Mat44_MultipliesByIdentity()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x10000, 0x20000, 0x30000, 0x40000);
        WriteIdentity44(rig, MatrixAddress);
        LoadSwi(rig, 0x50007, DestinationAddress, VectorAddress, MatrixAddress);

        rig.Run(1);

        Assert.Equal(0x10000, ReadWord(rig, DestinationAddress));
        Assert.Equal(0x20000, ReadWord(rig, DestinationAddress + 4));
        Assert.Equal(0x30000, ReadWord(rig, DestinationAddress + 8));
        Assert.Equal(0x40000, ReadWord(rig, DestinationAddress + 12));
    }

    [Fact]
    public void MulMat44Mat44_MultipliesByIdentity()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(
            rig,
            VectorAddress,
            0x10000,
            0x20000,
            0x30000,
            0x40000,
            0x50000,
            0x60000,
            0x70000,
            0x80000,
            0x90000,
            0xA0000,
            0xB0000,
            0xC0000,
            0xD0000,
            0xE0000,
            0xF0000,
            0x100000);
        WriteIdentity44(rig, MatrixAddress);
        LoadSwi(rig, 0x50008, DestinationAddress, VectorAddress, MatrixAddress);

        rig.Run(1);

        for (var i = 0; i < 16; i++)
        {
            Assert.Equal((i + 1) * 0x10000, ReadWord(rig, DestinationAddress + ((uint)i * 4)));
        }
    }

    [Fact]
    public void MulVec3Mat33_MatchesTheOperaReference()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x18000, -0x9000, 0x24000);
        WriteWords(
            rig,
            MatrixAddress,
            0x10000,
            0x8000,
            -0x4000,
            0x18000,
            -0x10000,
            0x3000,
            -0x20000,
            0x4000,
            0x10000);
        LoadSwi(rig, 0x50000, DestinationAddress, VectorAddress, MatrixAddress);

        rig.Run(1);

        Assert.Equal(-251904, ReadWord(rig, DestinationAddress));
        Assert.Equal(122880, ReadWord(rig, DestinationAddress + 4));
        Assert.Equal(115968, ReadWord(rig, DestinationAddress + 8));
    }

    [Fact]
    public void MulMat33Mat33_MatchesTheOperaReference()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(
            rig,
            VectorAddress,
            0x10000,
            0x8000,
            -0x4000,
            0x18000,
            -0x10000,
            0x3000,
            -0x20000,
            0x4000,
            0x10000);
        WriteWords(rig, MatrixAddress, 0x8000, 0, -0x8000, 0x4000, 0x10000, 0, 0, -0x4000, 0x8000);
        LoadSwi(rig, 0x50001, DestinationAddress, VectorAddress, MatrixAddress);

        rig.Run(1);

        int[] expected =
        [
            40960, 36864, -40960,
            32768, -68608, -43008,
            -61440, 0, 98304,
        ];
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], ReadWord(rig, DestinationAddress + ((uint)i * 4)));
        }
    }

    [Fact]
    public void MulVec3Mat33DivZ_MatchesTheOperaReference()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x18000, -0x9000, 0x24000);
        WriteWords(
            rig,
            MatrixAddress,
            0x10000,
            0x8000,
            -0x4000,
            0x18000,
            -0x10000,
            0x3000,
            -0x20000,
            0x4000,
            0x10000);
        LoadSwi(rig, 0x50011, DestinationAddress, VectorAddress, MatrixAddress);
        rig.Cpu.WriteRegister(3, 0x30000);

        rig.Run(1);

        Assert.Equal(-427068, ReadWord(rig, DestinationAddress));
        Assert.Equal(208325, ReadWord(rig, DestinationAddress + 4));
        Assert.Equal(115968, ReadWord(rig, DestinationAddress + 8));
    }

    [Fact]
    public void MulVec4Mat44_MatchesTheOperaReference()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x18000, -0x9000, 0x24000, 0x10000);
        WriteWords(
            rig,
            MatrixAddress,
            0x10000,
            0x8000,
            -0x4000,
            0x2000,
            0x18000,
            -0x10000,
            0x3000,
            0,
            -0x20000,
            0x4000,
            0x10000,
            -0x8000,
            0,
            0,
            0,
            0x10000);
        LoadSwi(rig, 0x50007, DestinationAddress, VectorAddress, MatrixAddress);

        rig.Run(1);

        Assert.Equal(-251904, ReadWord(rig, DestinationAddress));
        Assert.Equal(122880, ReadWord(rig, DestinationAddress + 4));
        Assert.Equal(115968, ReadWord(rig, DestinationAddress + 8));
        Assert.Equal(4096, ReadWord(rig, DestinationAddress + 12));
    }

    [Fact]
    public void MulManyVec4Mat44_MatchesTheOperaReference()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(
            rig,
            VectorAddress,
            0x10000,
            0x20000,
            0x30000,
            0x40000,
            -0x10000,
            0x8000,
            0,
            0x10000);
        WriteWords(
            rig,
            MatrixAddress,
            0x10000,
            0x8000,
            -0x4000,
            0x2000,
            0x18000,
            -0x10000,
            0x3000,
            0,
            -0x20000,
            0x4000,
            0x10000,
            -0x8000,
            0,
            0,
            0,
            0x10000);
        LoadSwi(rig, 0x50009, DestinationAddress, VectorAddress, MatrixAddress);
        rig.Cpu.WriteRegister(3, 2);

        rig.Run(1);

        int[] expected =
        [
            -131072, -49152, 204800, 172032,
            -16384, -65536, 22528, 57344,
        ];
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], ReadWord(rig, DestinationAddress + ((uint)i * 4)));
        }
    }

    [Fact]
    public void MulMat44Mat44_MatchesTheOperaReference()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(
            rig,
            VectorAddress,
            0x10000,
            0x8000,
            -0x4000,
            0x2000,
            0x18000,
            -0x10000,
            0x3000,
            0,
            -0x20000,
            0x4000,
            0x10000,
            -0x8000,
            0,
            0,
            0,
            0x10000);
        WriteIdentity44(rig, MatrixAddress);
        LoadSwi(rig, 0x50008, DestinationAddress, VectorAddress, MatrixAddress);

        rig.Run(1);

        int[] expected =
        [
            0x10000, 0x8000, -0x4000, 0x2000,
            0x18000, -0x10000, 0x3000, 0,
            -0x20000, 0x4000, 0x10000, -0x8000,
            0, 0, 0, 0x10000,
        ];
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], ReadWord(rig, DestinationAddress + ((uint)i * 4)));
        }

        // The same source multiplied by itself, matching the C reference.
        WriteWords(
            rig,
            SecondAddress,
            0x10000,
            0x8000,
            -0x4000,
            0x2000,
            0x18000,
            -0x10000,
            0x3000,
            0,
            -0x20000,
            0x4000,
            0x10000,
            -0x8000,
            0,
            0,
            0,
            0x10000);
        LoadSwi(rig, 0x50008, DestinationAddress, VectorAddress, SecondAddress);
        rig.Run(1);

        int[] squared =
        [
            0x24000, -0x1000, -0x6800, 0x6000,
            -0x6000, 0x1CC00, -0x6000, 0x1800,
            -0x3A000, -0x10000, 0x18C00, -0x14000,
            0, 0, 0, 0x10000,
        ];
        for (var i = 0; i < squared.Length; i++)
        {
            Assert.Equal(squared[i], ReadWord(rig, DestinationAddress + ((uint)i * 4)));
        }
    }

    [Fact]
    public void MulManyVec4Mat44_MultipliesEveryRecord()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x10000, 0, 0, 0, 0, 0x20000, 0, 0);
        WriteIdentity44(rig, MatrixAddress);
        LoadSwi(rig, 0x50009, DestinationAddress, VectorAddress, MatrixAddress);
        rig.Cpu.WriteRegister(3, 2);

        rig.Run(1);

        Assert.Equal(0x10000, ReadWord(rig, DestinationAddress));
        Assert.Equal(0x20000, ReadWord(rig, DestinationAddress + 20));
    }

    [Fact]
    public void Dot3_ReturnsTheFixedPointDotProduct()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x10000, 0x20000, 0x30000);
        WriteWords(rig, SecondAddress, 0x40000, 0x50000, 0x60000);
        LoadSwi(rig, 0x5000C, VectorAddress, SecondAddress, 0);

        rig.Run(1);

        Assert.Equal(0x200000u, rig.Cpu.ReadRegister(0));
    }

    [Fact]
    public void Cross3_ReturnsTheFixedPointCrossProduct()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x10000, 0, 0);
        WriteWords(rig, SecondAddress, 0, 0x10000, 0);
        LoadSwi(rig, 0x5000E, DestinationAddress, VectorAddress, SecondAddress);

        rig.Run(1);

        Assert.Equal(0, ReadWord(rig, DestinationAddress));
        Assert.Equal(0, ReadWord(rig, DestinationAddress + 4));
        Assert.Equal(0x10000, ReadWord(rig, DestinationAddress + 8));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0x10000, 1)]
    [InlineData(0x40000, 2)]
    [InlineData(0x8000, 2)]
    public void AbsVec3_UsesOperasRestoringLoop(int component, int expected)
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, component, 0, 0);
        LoadSwi(rig, 0x5000F, VectorAddress, 0, 0);

        rig.Run(1);

        Assert.Equal(unchecked((uint)expected), rig.Cpu.ReadRegister(0));
    }

    [Fact]
    public void AbsVec4_UsesOperasRestoringLoop()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x10000, 0, 0, 0);
        LoadSwi(rig, 0x50010, VectorAddress, 0, 0);

        rig.Run(1);

        Assert.Equal(1u, rig.Cpu.ReadRegister(0));
    }

    [Fact]
    public void MulVec3Mat33DivZ_AppliesPerspectiveDivide()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x10000, 0x20000, 0x40000);
        WriteIdentity33(rig, MatrixAddress);
        LoadSwi(rig, 0x50011, DestinationAddress, VectorAddress, MatrixAddress);
        rig.Cpu.WriteRegister(3, 0x20000);

        rig.Run(1);

        Assert.Equal(0x8000, ReadWord(rig, DestinationAddress));
        Assert.Equal(0x10000, ReadWord(rig, DestinationAddress + 4));
        Assert.Equal(0x40000, ReadWord(rig, DestinationAddress + 8));
    }

    [Fact]
    public void MulVec3Mat33DivZ_LeavesXAndYWhenZIsZero()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x10000, 0x20000, 0);
        WriteIdentity33(rig, MatrixAddress);
        LoadSwi(rig, 0x50011, DestinationAddress, VectorAddress, MatrixAddress);
        rig.Cpu.WriteRegister(3, 0x20000);

        rig.Run(1);

        Assert.Equal(0x10000, ReadWord(rig, DestinationAddress));
        Assert.Equal(0x20000, ReadWord(rig, DestinationAddress + 4));
        Assert.Equal(0, ReadWord(rig, DestinationAddress + 8));
    }

    [Fact]
    public void MulManyVec3Mat33DivZ_ReadsTheGuestStruct()
    {
        ArmTestRig rig = CreateHleRig();
        WriteWords(rig, VectorAddress, 0x10000, 0x20000, 0x40000);
        WriteIdentity33(rig, MatrixAddress);
        WriteWords(
            rig,
            SecondAddress,
            unchecked((int)DestinationAddress),
            unchecked((int)VectorAddress),
            unchecked((int)MatrixAddress),
            0x20000,
            1);
        LoadSwi(rig, 0x50012, SecondAddress, 0, 0);

        rig.Run(1);

        Assert.Equal(0x8000, ReadWord(rig, DestinationAddress));
        Assert.Equal(0x10000, ReadWord(rig, DestinationAddress + 4));
        Assert.Equal(0x40000, ReadWord(rig, DestinationAddress + 8));
    }

    [Theory]
    [InlineData(0x50003)]
    [InlineData(0x50004)]
    [InlineData(0x5000A)]
    [InlineData(0x5000B)]
    [InlineData(0x5000D)]
    public void UnsupportedSwi_FallsBackToTheGuestVector(uint comment)
    {
        ArmTestRig rig = CreateHleRig();
        LoadSwi(rig, comment, 0, 0, 0);

        rig.Run(1);

        Assert.Equal(0x08u, rig.Cpu.ProgramCounter);
        Assert.Equal(0, rig.Cpu.SwiHleInstructions);
    }

    [Fact]
    public void SwiHleDisabled_TrapsToTheGuestVector()
    {
        var rig = new ArmTestRig();
        LoadSwi(rig, 0x50000, 0, 0, 0);

        rig.Run(1);

        Assert.Equal(0x08u, rig.Cpu.ProgramCounter);
        Assert.Equal(0, rig.Cpu.SwiHleInstructions);
    }

    [Fact]
    public void SwiHle_ConsumesTheSameCyclesAsTheGuestTrap()
    {
        ArmTestRig hle = CreateHleRig();
        LoadSwi(hle, 0x50000, 0, 0, 0);
        var trap = new ArmTestRig();
        LoadSwi(trap, 0x50000, 0, 0, 0);

        int hleCycles = hle.Run(1);
        int trapCycles = trap.Run(1);

        Assert.Equal(trapCycles, hleCycles);
    }

    [Fact]
    public void SwiHleTelemetry_ResetsWithTheCpu()
    {
        ArmTestRig rig = CreateHleRig();
        WriteIdentity33(rig, MatrixAddress);
        LoadSwi(rig, 0x50000, DestinationAddress, VectorAddress, MatrixAddress);

        rig.Run(1);
        rig.Run(1);

        Assert.Equal(1, rig.Cpu.SwiHleInstructions);
        rig.Cpu.Reset();
        Assert.Equal(0, rig.Cpu.SwiHleInstructions);
    }

    private static ArmTestRig CreateHleRig()
    {
        return new ArmTestRig(new EmulatorTuning { SwiHle = true });
    }

    private static void LoadSwi(ArmTestRig rig, uint comment, uint r0, uint r1, uint r2)
    {
        rig.Cpu.WriteRegister(0, r0);
        rig.Cpu.WriteRegister(1, r1);
        rig.Cpu.WriteRegister(2, r2);
        rig.Cpu.WriteRegister(3, 0);
        rig.LoadProgram(ArmTestAssembler.Swi(comment), ArmTestAssembler.Nop());
    }

    private static void WriteWords(ArmTestRig rig, uint address, params int[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            rig.WriteRam(address + ((uint)i * 4), unchecked((uint)values[i]));
        }
    }

    private static void WriteIdentity33(ArmTestRig rig, uint address)
    {
        WriteWords(rig, address, 0x10000, 0, 0, 0, 0x10000, 0, 0, 0, 0x10000);
    }

    private static void WriteIdentity44(ArmTestRig rig, uint address)
    {
        WriteWords(
            rig,
            address,
            0x10000,
            0,
            0,
            0,
            0,
            0x10000,
            0,
            0,
            0,
            0,
            0x10000,
            0,
            0,
            0,
            0,
            0x10000);
    }

    private static int ReadWord(ArmTestRig rig, uint address)
    {
        return unchecked((int)rig.ReadRam(address));
    }
}
