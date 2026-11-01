using System;
using System.Collections.Generic;
using FiveDo.Input;
using FiveDo.Machine;

namespace FiveDo.Cheats;

/// <summary>
/// A mutable set of parsed cheats for one game. The emulation thread calls
/// <see cref="Apply"/> once per frame; the host edits entries while the core is paused.
/// </summary>
public sealed class CheatSet
{
    private readonly List<CheatEntry> _entries = [];
    private readonly List<CheatProgram?> _programs = [];

    /// <summary>Gets the entries in display order.</summary>
    public IReadOnlyList<CheatEntry> Entries => _entries;

    /// <summary>Gets the number of entries, including invalid or disabled entries.</summary>
    public int Count => _entries.Count;

    /// <summary>Gets the number of enabled entries with valid code.</summary>
    public int ActiveCount
    {
        get
        {
            var count = 0;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Enabled && _programs[i] is not null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Gets the total number of bytes written since the last statistics reset.</summary>
    public long Writes { get; private set; }

    /// <summary>Creates an empty set.</summary>
    public CheatSet()
    {
    }

    /// <summary>Creates a set from a document, marking invalid codes instead of throwing.</summary>
    /// <param name="document">Source document.</param>
    /// <returns>A set containing every entry from the document.</returns>
    public static CheatSet FromDocument(CheatDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var set = new CheatSet();
        set.LoadEntries(document.Cheats ?? []);
        return set;
    }

    /// <summary>Creates a set from entries, marking invalid codes instead of throwing.</summary>
    /// <param name="entries">Source entries.</param>
    /// <returns>A set containing every entry.</returns>
    public static CheatSet FromEntries(IEnumerable<CheatEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var set = new CheatSet();
        set.LoadEntries(entries);
        return set;
    }

    /// <summary>Finds an entry by name, case-insensitively.</summary>
    /// <param name="name">Entry name.</param>
    /// <returns>The entry, or <see langword="null"/> when no entry matches.</returns>
    public CheatEntry? Find(string name)
    {
        var index = FindIndex(name);
        return index < 0 ? null : _entries[index];
    }

    /// <summary>Adds a new entry after validating its code.</summary>
    /// <param name="name">Unique display name.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="code">Cheat code text.</param>
    /// <param name="enabled">Whether the cheat starts enabled.</param>
    /// <returns>The added entry.</returns>
    /// <exception cref="ArgumentException">Thrown when the name is empty or already exists.</exception>
    /// <exception cref="FormatException">Thrown when the code is invalid.</exception>
    public CheatEntry Add(string name, string description, string code, bool enabled)
    {
        return Add(new CheatEntry
        {
            Name = name,
            Description = description,
            Code = code,
            Enabled = enabled,
        });
    }

    /// <summary>Adds a new entry after validating its code.</summary>
    /// <param name="entry">Entry to add. The set takes ownership of the instance.</param>
    /// <returns>The added entry.</returns>
    /// <exception cref="ArgumentException">Thrown when the name is empty or already exists.</exception>
    /// <exception cref="FormatException">Thrown when the code is invalid.</exception>
    public CheatEntry Add(CheatEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidateName(entry.Name, -1);
        CheatProgram program = CheatParser.Parse(entry.Code);
        entry.Error = string.Empty;
        _entries.Add(entry);
        _programs.Add(program);
        return entry;
    }

    /// <summary>Updates an existing entry after validating the replacement code.</summary>
    /// <param name="name">Current name.</param>
    /// <param name="newName">Replacement name, or the current name.</param>
    /// <param name="description">Replacement description.</param>
    /// <param name="code">Replacement code.</param>
    /// <param name="enabled">Replacement enabled state.</param>
    /// <returns><see langword="true"/> when the entry was found and updated.</returns>
    /// <exception cref="ArgumentException">Thrown when the new name is empty or already exists.</exception>
    /// <exception cref="FormatException">Thrown when the code is invalid.</exception>
    public bool Update(string name, string newName, string description, string code, bool enabled)
    {
        var index = FindIndex(name);
        if (index < 0)
        {
            return false;
        }

        ValidateName(newName, index);
        CheatProgram program = CheatParser.Parse(code);
        CheatEntry entry = _entries[index];
        entry.Name = newName;
        entry.Description = description;
        entry.Code = code;
        entry.Enabled = enabled;
        entry.Error = string.Empty;
        _programs[index] = program;
        return true;
    }

    /// <summary>Enables or disables an entry by name.</summary>
    /// <param name="name">Entry name.</param>
    /// <param name="enabled">New enabled state.</param>
    /// <returns><see langword="true"/> when the entry was found.</returns>
    public bool SetEnabled(string name, bool enabled)
    {
        var index = FindIndex(name);
        if (index < 0)
        {
            return false;
        }

        _entries[index].Enabled = enabled;
        return true;
    }

    /// <summary>Removes an entry by name.</summary>
    /// <param name="name">Entry name.</param>
    /// <returns><see langword="true"/> when the entry was found and removed.</returns>
    public bool Remove(string name)
    {
        var index = FindIndex(name);
        if (index < 0)
        {
            return false;
        }

        _entries.RemoveAt(index);
        _programs.RemoveAt(index);
        return true;
    }

    /// <summary>Resets the byte-write counter.</summary>
    public void ResetStatistics()
    {
        Writes = 0;
    }

    /// <summary>Applies every enabled, valid entry to the machine.</summary>
    /// <param name="machine">Target machine.</param>
    /// <returns>The number of bytes written by this call.</returns>
    public long Apply(FiveDoMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        var writes = 0L;
        for (var i = 0; i < _entries.Count; i++)
        {
            if (!_entries[i].Enabled)
            {
                continue;
            }

            CheatProgram? program = _programs[i];
            if (program is null)
            {
                continue;
            }

            CheatStatement[] statements = program.Statements;
            for (var statementIndex = 0; statementIndex < statements.Length; statementIndex++)
            {
                writes += Execute(statements[statementIndex], machine);
            }
        }

        Writes += writes;
        return writes;
    }

    private static long Execute(CheatStatement statement, FiveDoMachine machine)
    {
        switch (statement)
        {
            case CheatWriteStatement write:
                Write(machine, write.Address, write.Bytes);
                return write.Bytes.Length;
            case CheatConditionalStatement conditional:
                return Evaluate(conditional.Condition, machine)
                    ? Execute(conditional.Action, machine)
                    : 0;
            default:
                return 0;
        }
    }

    private static bool Evaluate(CheatCondition condition, FiveDoMachine machine)
    {
        switch (condition)
        {
            case CheatMemoryCondition memory:
                var actual = memory.Size switch
                {
                    1 => machine.Memory.Ram.Read8(memory.Address),
                    2 => machine.Memory.Ram.Read16(memory.Address),
                    _ => machine.Memory.Ram.Read32(memory.Address),
                };
                return Compare(actual, memory.Comparison, memory.Value);
            case CheatButtonCondition button:
                return IsButtonDown(machine, button.Button);
            case CheatLogicalCondition logical:
                return logical.IsAnd
                    ? Evaluate(logical.Left, machine) && Evaluate(logical.Right, machine)
                    : Evaluate(logical.Left, machine) || Evaluate(logical.Right, machine);
            case CheatNotCondition not:
                return !Evaluate(not.Inner, machine);
            default:
                return false;
        }
    }

    private static bool Compare(uint actual, CheatComparison comparison, uint expected)
    {
        return comparison switch
        {
            CheatComparison.Equal => actual == expected,
            CheatComparison.NotEqual => actual != expected,
            CheatComparison.Less => actual < expected,
            CheatComparison.LessOrEqual => actual <= expected,
            CheatComparison.Greater => actual > expected,
            CheatComparison.GreaterOrEqual => actual >= expected,
            _ => false,
        };
    }

    private static void Write(FiveDoMachine machine, uint address, byte[] bytes)
    {
        // Byte writes preserve both the textual raw order and the little-endian byte
        // sequence produced by write8/write16/write32. The guest's Read16/Read32 then
        // observe the intended value.
        for (var i = 0; i < bytes.Length; i++)
        {
            machine.Memory.Ram.Write8(unchecked(address + (uint)i), bytes[i]);
        }
    }

    private static bool IsButtonDown(FiveDoMachine machine, ControllerButton button)
    {
        ReadOnlySpan<byte> pbus = machine.Madam.PbusQueue;
        if (pbus.Length < 4)
        {
            return false;
        }

        var low = pbus[2];
        var high = pbus[3];
        return button switch
        {
            ControllerButton.A => (high & 0x01) != 0,
            ControllerButton.Left => (high & 0x02) != 0,
            ControllerButton.Right => (high & 0x04) != 0,
            ControllerButton.Up => (high & 0x08) != 0,
            ControllerButton.Down => (high & 0x10) != 0,
            ControllerButton.L => (low & 0x04) != 0,
            ControllerButton.R => (low & 0x08) != 0,
            ControllerButton.X => (low & 0x10) != 0,
            ControllerButton.P => (low & 0x20) != 0,
            ControllerButton.C => (low & 0x40) != 0,
            ControllerButton.B => (low & 0x80) != 0,
            _ => false,
        };
    }

    private void LoadEntries(IEnumerable<CheatEntry> entries)
    {
        _entries.Clear();
        _programs.Clear();
        foreach (CheatEntry source in entries)
        {
            CheatEntry entry = source.Clone();
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                entry.Name = $"Cheat {_entries.Count + 1}";
            }

            if (string.IsNullOrWhiteSpace(entry.Code))
            {
                entry.Error = "The cheat code is empty.";
                _entries.Add(entry);
                _programs.Add(null);
                continue;
            }

            try
            {
                CheatProgram program = CheatParser.Parse(entry.Code);
                entry.Error = string.Empty;
                _entries.Add(entry);
                _programs.Add(program);
            }
            catch (FormatException exception)
            {
                entry.Error = exception.Message;
                _entries.Add(entry);
                _programs.Add(null);
            }
        }
    }

    private void ValidateName(string name, int ignoredIndex)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A cheat needs a non-empty name.", nameof(name));
        }

        for (var i = 0; i < _entries.Count; i++)
        {
            if (i != ignoredIndex
                && string.Equals(_entries[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"A cheat named '{name}' already exists.", nameof(name));
            }
        }
    }

    private int FindIndex(string name)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (string.Equals(_entries[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
