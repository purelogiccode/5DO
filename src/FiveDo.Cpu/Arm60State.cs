using System;

namespace FiveDo.Cpu;

/// <summary>
/// Architectural state of the ARM60 core, including the banked registers used by the
/// FreeDOCore interpreter.
/// </summary>
/// <remarks>
/// <para>
/// FreeDO keeps the currently visible registers in <c>USER</c>, the non-FIQ r8-r12 and
/// user-mode r13/r14 in <c>CASH</c>, and the other modes' banked registers in separate
/// arrays. The switching helpers in this class reproduce
/// <c>ARM_RestUserRONS</c>, <c>ARM_RestFiqRONS</c>, <c>ARM_RestIrqRONS</c>,
/// <c>ARM_RestSvcRONS</c>, <c>ARM_RestAbtRONS</c>, and <c>ARM_RestUndRONS</c> exactly.
/// </para>
/// <para>
/// Do not replace the banked model with a "cleaner" per-mode register file. The reference
/// saves the <em>current</em> mode's registers before loading the target mode's bank, and
/// several games depend on the resulting quirks.
/// </para>
/// </remarks>
public sealed class Arm60State
{
    /// <summary>Number of user-visible registers.</summary>
    public const int RegisterCount = 16;

    internal readonly uint[] User = new uint[RegisterCount];
    internal readonly uint[] Cash = new uint[7];
    internal readonly uint[] Svc = new uint[2];
    internal readonly uint[] Abt = new uint[2];
    internal readonly uint[] Fiq = new uint[7];
    internal readonly uint[] Irq = new uint[2];
    internal readonly uint[] Und = new uint[2];
    internal readonly uint[] Spsr = new uint[6];

    /// <summary>Gets or sets the current program status register.</summary>
    /// <remarks>Use <see cref="SetCpsr"/> when the mode bits may change.</remarks>
    public uint Cpsr { get; internal set; }

    /// <summary>Gets or sets a value indicating whether a fast interrupt is pending.</summary>
    public bool FiqPending { get; set; }

    /// <summary>Gets or sets a value indicating whether the second BIOS ROM bank is selected.</summary>
    public bool SecondRom { get; set; }

    /// <summary>Gets or sets a value indicating whether an unmapped access has been observed.</summary>
    /// <remarks>FreeDO: <c>MAS_Access_Exept</c>. Kept for future data-abort accuracy.</remarks>
    public bool DataAbortPending { get; set; }

    /// <summary>Gets or sets the program counter (R15).</summary>
    public uint ProgramCounter
    {
        get => User[15];
        set => User[15] = value;
    }

    /// <summary>Gets or sets a user-visible register by index.</summary>
    /// <param name="index">Register index, 0-15.</param>
    /// <returns>The register value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside 0-15.</exception>
    public uint this[int index]
    {
        get
        {
            ValidateRegister(index);
            return User[index];
        }

        set
        {
            ValidateRegister(index);
            User[index] = value;
        }
    }

    /// <summary>Reads one banked SPSR.</summary>
    /// <param name="index">SPSR index 0-5 (user, FIQ, IRQ, SVC, ABT, UND).</param>
    /// <returns>The SPSR value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside 0-5.</exception>
    public uint ReadSpsr(int index)
    {
        if ((uint)index >= (uint)Spsr.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The SPSR index must be between 0 and 5.");
        }

        return Spsr[index];
    }

    /// <summary>Gets the SPSR belonging to the current processor mode.</summary>
    internal uint CurrentSpsr
    {
        get
        {
            var index = ArmModes.Table[Cpsr & 0x1f];
            return index < Spsr.Length ? Spsr[index] : 0u;
        }
    }

    /// <summary>Restores the FreeDOCore power-on values.</summary>
    public void Reset()
    {
        Array.Clear(User);
        Array.Clear(Cash);
        Array.Clear(Svc);
        Array.Clear(Abt);
        Array.Clear(Fiq);
        Array.Clear(Irq);
        Array.Clear(Und);
        Array.Clear(Spsr);
        Cpsr = 0x13;
        FiqPending = false;
        SecondRom = false;
        DataAbortPending = false;
        User[15] = 0x03000000;
    }

    /// <summary>Changes the CPSR and switches the banked registers. FreeDO: <c>_arm_SetCPSR</c>.</summary>
    /// <param name="value">New CPSR value.</param>
    internal void SetCpsr(uint value)
    {
        value |= 0x10;
        ChangeModeSafe(value);
        Cpsr = value & 0xf00000df;
    }

    /// <summary>Changes only the mode bits and switches the banked registers. FreeDO: <c>SETM</c>.</summary>
    /// <param name="value">Value whose low five bits select the new mode.</param>
    internal void SetMode(uint value)
    {
        value |= 0x10;
        ChangeModeSafe(value);
        Cpsr = (Cpsr & 0xffffffe0) | (value & 0x1f);
    }

    /// <summary>Switches the banked registers for a target mode. FreeDO: <c>ARM_Change_ModeSafe</c>.</summary>
    /// <param name="mode">Target mode value.</param>
    internal void ChangeModeSafe(uint mode)
    {
        switch (ArmModes.Table[mode & 0x1f])
        {
            case ArmModes.User:
                RestUserRegisters();
                break;
            case ArmModes.Fiq:
                RestFiqRegisters();
                break;
            case ArmModes.Irq:
                RestIrqRegisters();
                break;
            case ArmModes.Svc:
                RestSvcRegisters();
                break;
            case ArmModes.Abt:
                RestAbtRegisters();
                break;
            case ArmModes.Und:
                RestUndRegisters();
                break;
        }
    }

    /// <summary>Writes a currently visible register. FreeDO: <c>load</c>.</summary>
    /// <param name="index">Register index.</param>
    /// <param name="value">Value to write.</param>
    internal void Load(int index, uint value) => User[index] = value;

    /// <summary>Writes a register using user-mode bank selection. FreeDO: <c>loadusr</c>.</summary>
    /// <param name="index">Register index.</param>
    /// <param name="value">Value to write.</param>
    internal void LoadUser(int index, uint value)
    {
        if (index == 15)
        {
            User[15] = value;
            return;
        }

        switch (ArmModes.Table[(Cpsr & 0x1f) | 0x10])
        {
            case ArmModes.User:
                User[index] = value;
                break;
            case ArmModes.Fiq:
                if (index > 7)
                {
                    Cash[index - 8] = value;
                }
                else
                {
                    User[index] = value;
                }

                break;
            case ArmModes.Irq:
            case ArmModes.Abt:
            case ArmModes.Und:
            case ArmModes.Svc:
                if (index > 12)
                {
                    Cash[index - 8] = value;
                }
                else
                {
                    User[index] = value;
                }

                break;
        }
    }

    /// <summary>Reads a register using user-mode bank selection. FreeDO: <c>rreadusr</c>.</summary>
    /// <param name="index">Register index.</param>
    /// <returns>The register value.</returns>
    internal uint ReadUser(int index)
    {
        if (index == 15)
        {
            return User[15];
        }

        switch (ArmModes.Table[Cpsr & 0x1f])
        {
            case ArmModes.User:
                return User[index];
            case ArmModes.Fiq:
                return index > 7 ? Cash[index - 8] : User[index];
            case ArmModes.Irq:
            case ArmModes.Abt:
            case ArmModes.Und:
            case ArmModes.Svc:
                return index > 12 ? Cash[index - 8] : User[index];
            default:
                return 0;
        }
    }

    /// <summary>Sets or clears the negative flag. FreeDO: <c>SETN</c>.</summary>
    /// <param name="value">Flag value.</param>
    internal void SetN(bool value) => Cpsr = (Cpsr & 0x7fffffffu) | (value ? 1u << 31 : 0u);

    /// <summary>Sets or clears the zero flag. FreeDO: <c>SETZ</c>.</summary>
    /// <param name="value">Flag value.</param>
    internal void SetZ(bool value) => Cpsr = (Cpsr & 0xbfffffffu) | (value ? 1u << 30 : 0u);

    /// <summary>Sets or clears the carry flag. FreeDO: <c>SETC</c>.</summary>
    /// <param name="value">Flag value.</param>
    internal void SetC(bool value) => Cpsr = (Cpsr & 0xdfffffffu) | (value ? 1u << 29 : 0u);

    /// <summary>Sets the carry flag from bit zero of <paramref name="value"/>. FreeDO: <c>ARM_SET_C</c>.</summary>
    /// <param name="value">Value whose low bit becomes the carry flag.</param>
    internal void SetC(uint value) => Cpsr = (Cpsr & 0xdfffffffu) | ((value & 1u) << 29);

    /// <summary>Sets or clears the overflow flag. FreeDO: <c>SETV</c>.</summary>
    /// <param name="value">Flag value.</param>
    internal void SetV(bool value) => Cpsr = (Cpsr & 0xefffffffu) | (value ? 1u << 28 : 0u);

    /// <summary>Sets or clears the IRQ disable bit. FreeDO: <c>SETI</c>.</summary>
    /// <param name="value">Flag value.</param>
    internal void SetI(bool value) => Cpsr = (Cpsr & 0xffffff7fu) | (value ? 1u << 7 : 0u);

    /// <summary>Sets or clears the FIQ disable bit. FreeDO: <c>SETF</c>.</summary>
    /// <param name="value">Flag value.</param>
    internal void SetF(bool value) => Cpsr = (Cpsr & 0xffffffbfu) | (value ? 1u << 6 : 0u);

    private void RestUserRegisters()
    {
        switch (ArmModes.Table[Cpsr & 0x1f])
        {
            case ArmModes.User:
                break;
            case ArmModes.Fiq:
                Array.Copy(User, 8, Fiq, 0, 7);
                Array.Copy(Cash, 0, User, 8, 7);
                break;
            case ArmModes.Irq:
                Irq[0] = User[13];
                Irq[1] = User[14];
                User[13] = Cash[5];
                User[14] = Cash[6];
                break;
            case ArmModes.Svc:
                Svc[0] = User[13];
                Svc[1] = User[14];
                User[13] = Cash[5];
                User[14] = Cash[6];
                break;
            case ArmModes.Abt:
                Abt[0] = User[13];
                Abt[1] = User[14];
                User[13] = Cash[5];
                User[14] = Cash[6];
                break;
            case ArmModes.Und:
                Und[0] = User[13];
                Und[1] = User[14];
                User[13] = Cash[5];
                User[14] = Cash[6];
                break;
        }
    }

    private void RestFiqRegisters()
    {
        switch (ArmModes.Table[Cpsr & 0x1f])
        {
            case ArmModes.User:
                Array.Copy(User, 8, Cash, 0, 7);
                Array.Copy(Fiq, 0, User, 8, 7);
                break;
            case ArmModes.Fiq:
                break;
            case ArmModes.Irq:
                Array.Copy(User, 8, Cash, 0, 5);
                Irq[0] = User[13];
                Irq[1] = User[14];
                Array.Copy(Fiq, 0, User, 8, 7);
                break;
            case ArmModes.Svc:
                Array.Copy(User, 8, Cash, 0, 5);
                Svc[0] = User[13];
                Svc[1] = User[14];
                Array.Copy(Fiq, 0, User, 8, 7);
                break;
            case ArmModes.Abt:
                Array.Copy(User, 8, Cash, 0, 5);
                Abt[0] = User[13];
                Abt[1] = User[14];
                Array.Copy(Fiq, 0, User, 8, 7);
                break;
            case ArmModes.Und:
                Array.Copy(User, 8, Cash, 0, 5);
                Und[0] = User[13];
                Und[1] = User[14];
                Array.Copy(Fiq, 0, User, 8, 7);
                break;
        }
    }

    private void RestIrqRegisters()
    {
        switch (ArmModes.Table[Cpsr & 0x1f])
        {
            case ArmModes.User:
                Cash[5] = User[13];
                Cash[6] = User[14];
                User[13] = Irq[0];
                User[14] = Irq[1];
                break;
            case ArmModes.Fiq:
                Array.Copy(User, 8, Fiq, 0, 7);
                Array.Copy(Cash, 0, User, 8, 5);
                User[13] = Irq[0];
                User[14] = Irq[1];
                break;
            case ArmModes.Irq:
                break;
            case ArmModes.Svc:
                Svc[0] = User[13];
                Svc[1] = User[14];
                User[13] = Irq[0];
                User[14] = Irq[1];
                break;
            case ArmModes.Abt:
                Abt[0] = User[13];
                Abt[1] = User[14];
                User[13] = Irq[0];
                User[14] = Irq[1];
                break;
            case ArmModes.Und:
                Und[0] = User[13];
                Und[1] = User[14];
                User[13] = Irq[0];
                User[14] = Irq[1];
                break;
        }
    }

    private void RestSvcRegisters()
    {
        switch (ArmModes.Table[Cpsr & 0x1f])
        {
            case ArmModes.User:
                Cash[5] = User[13];
                Cash[6] = User[14];
                User[13] = Svc[0];
                User[14] = Svc[1];
                break;
            case ArmModes.Fiq:
                Array.Copy(User, 8, Fiq, 0, 7);
                Array.Copy(Cash, 0, User, 8, 5);
                User[13] = Svc[0];
                User[14] = Svc[1];
                break;
            case ArmModes.Irq:
                Irq[0] = User[13];
                Irq[1] = User[14];
                User[13] = Svc[0];
                User[14] = Svc[1];
                break;
            case ArmModes.Svc:
                break;
            case ArmModes.Abt:
                Abt[0] = User[13];
                Abt[1] = User[14];
                User[13] = Svc[0];
                User[14] = Svc[1];
                break;
            case ArmModes.Und:
                Und[0] = User[13];
                Und[1] = User[14];
                User[13] = Svc[0];
                User[14] = Svc[1];
                break;
        }
    }

    private void RestAbtRegisters()
    {
        switch (ArmModes.Table[Cpsr & 0x1f])
        {
            case ArmModes.User:
                Cash[5] = User[13];
                Cash[6] = User[14];
                User[13] = Abt[0];
                User[14] = Abt[1];
                break;
            case ArmModes.Fiq:
                Array.Copy(User, 8, Fiq, 0, 7);
                Array.Copy(Cash, 0, User, 8, 5);
                User[13] = Abt[0];
                User[14] = Abt[1];
                break;
            case ArmModes.Irq:
                Irq[0] = User[13];
                Irq[1] = User[14];
                User[13] = Abt[0];
                User[14] = Abt[1];
                break;
            case ArmModes.Svc:
                Svc[0] = User[13];
                Svc[1] = User[14];
                User[13] = Abt[0];
                User[14] = Abt[1];
                break;
            case ArmModes.Abt:
                break;
            case ArmModes.Und:
                Und[0] = User[13];
                Und[1] = User[14];
                User[13] = Abt[0];
                User[14] = Abt[1];
                break;
        }
    }

    private void RestUndRegisters()
    {
        switch (ArmModes.Table[Cpsr & 0x1f])
        {
            case ArmModes.User:
                Cash[5] = User[13];
                Cash[6] = User[14];
                User[13] = Und[0];
                User[14] = Und[1];
                break;
            case ArmModes.Fiq:
                Array.Copy(User, 8, Fiq, 0, 7);
                Array.Copy(Cash, 0, User, 8, 5);
                User[13] = Und[0];
                User[14] = Und[1];
                break;
            case ArmModes.Irq:
                Irq[0] = User[13];
                Irq[1] = User[14];
                User[13] = Und[0];
                User[14] = Und[1];
                break;
            case ArmModes.Svc:
                Svc[0] = User[13];
                Svc[1] = User[14];
                User[13] = Und[0];
                User[14] = Und[1];
                break;
            case ArmModes.Abt:
                Abt[0] = User[13];
                Abt[1] = User[14];
                User[13] = Und[0];
                User[14] = Und[1];
                break;
            case ArmModes.Und:
                break;
        }
    }

    private static void ValidateRegister(int index)
    {
        if ((uint)index >= RegisterCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The register index must be between 0 and 15.");
        }
    }
}

/// <summary>
/// ARM processor mode constants and the FreeDOCore mode lookup table.
/// </summary>
internal static class ArmModes
{
    internal const byte User = 0;
    internal const byte Fiq = 1;
    internal const byte Irq = 2;
    internal const byte Svc = 3;
    internal const byte Abt = 4;
    internal const byte Und = 5;
    internal const byte Unknown = 0xff;

    internal static readonly byte[] Table =
    {
        Unknown, Unknown, Unknown, Unknown,
        Unknown, Unknown, Unknown, Unknown,
        Unknown, Unknown, Unknown, Unknown,
        Unknown, Unknown, Unknown, Unknown,
        User, Fiq, Irq, Svc,
        Unknown, Unknown, Unknown, Abt,
        Unknown, Unknown, Unknown, Und,
        Unknown, Unknown, Unknown, Unknown,
    };
}
