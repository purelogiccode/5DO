# Cheats

5DO has a per-game RAM cheat engine (UX-04). It applies enabled entries once before
every emulated frame, so cheats survive save-state loads, rewind replay, and
fast-forward. The desktop shell and the CLI share the same parser and files.

## Important scope and legal notes

- 3DO does not have a standard, publicly documented RAM cheat-code format comparable to
  Game Genie or GameShark. The Game Guru is a **save-file editor**, not a RAM cheat
  device. 5DO therefore implements a documented raw/typed RAM code syntax rather than
  pretending to decode a format that does not exist.
- 5DO does not ship cheat lists, copyrighted code databases, or game assets. Codes are
  user-supplied, and users are responsible for their legality and for the game versions
  they target.
- `--poke` remains a one-shot diagnostic patch. The cheat engine is the persistent,
  per-game feature.

## Files

Cheat files live in the configured cheat directory:

- Default: `%LOCALAPPDATA%\5DO\cheats` on Windows, or the equivalent local application
  data directory on Linux/macOS.
- Settings > Paths > Cheats changes the directory.
- The file name is the 4DO game ID, for example `ABCD1234.json`. With no disc loaded
  the shared file is `global.json`.

The document is JSON:

```json
{
  "Version": 1,
  "GameId": "ABCD1234",
  "Cheats": [
    {
      "Name": "Infinite health",
      "Description": "Keeps the health word at 100.",
      "Code": "write16 0x00123456 = 0x0064",
      "Enabled": true
    }
  ]
}
```

Invalid individual codes are preserved and shown in the UI/CLI with an error. They are
not applied. A document version newer than the supported format is rejected rather than
silently misread.

## Code syntax

Numbers are **hexadecimal by default**. Use `0x`/`$` for explicit hex and `0d` for
decimal. Statements are separated by `;` or newlines. `#` and `//` start a comment.

### Raw byte writes

```text
0x00123456 = 12 34 56
write 0x00123456 = 12 34 56
```

The bytes are written in the order shown. This is the same syntax as `--poke`, so a
diagnostic patch can be copied into a cheat entry.

### Typed writes

```text
write8  0x00123456 = 0x64
write16 0x00123456 = 0x0064
write32 0x00123456 = 0x00000064
```

Typed writes store the value in the guest's word order. Reading the same address with
`Read8`/`Read16`/`Read32` observes the intended value.

### Conditional writes

```text
if 0x00123456 == 0x64 then write8 0x00123456 = 0xFF
if byte 0x00123456 != 0 then write8 0x00123456 = 0
if word 0x00123456 >= 0x10 then write16 0x00123456 = 0x10
if dword 0x00123456 < 0x1000 then write32 0x00123456 = 0x1000
```

Conditions compare a guest memory value. The default width is 32 bits; `byte`, `word`,
and `dword` select 8, 16, or 32 bits. Operators are `==`, `!=`, `<`, `<=`, `>`, `>=`.
A single `=` is accepted as equality in a condition.

### Button conditions

```text
if button A then write8 0x00123456 = 0x01
if not button Start then write8 0x00123456 = 0
if button B and 0x00123456 == 0 then write8 0x00123456 = 0x01
```

Buttons are read from the player-1 PBUS packet. Supported names: `A`, `B`, `C`, `P`
(also `Start`/`Pause`), `X`, `L`, `R`, `Up`, `Down`, `Left`, `Right`. Conditions can be
combined with `and`, `or`, `not`, and parentheses. The packet is the one the machine
used for the previous frame, so a button condition reacts on the next frame.

### Examples

```text
# Freeze a 32-bit score at 999999.
write32 0x00200000 = 0x000F423F

# Only restore health when the game has reduced it below 50.
if word 0x00200100 < 0x0032 then write16 0x00200100 = 0x0064

# Give a one-frame item grant while B is held.
if button B then write8 0x00200200 = 0x01
```

## CLI

```powershell
# List the current game's cheats.
5do cheat list --game ABCD1234

# Add an entry (enabled by default).
5do cheat add --game ABCD1234 --name "Infinite health" --code "write16 0x00123456 = 0x0064"

# Enable, disable, or remove by name.
5do cheat disable --game ABCD1234 --name "Infinite health"
5do cheat enable  --game ABCD1234 --name "Infinite health"
5do cheat remove  --game ABCD1234 --name "Infinite health"

# Import/export a whole document. Import merges by name; existing names are replaced.
5do cheat import --game ABCD1234 --in my-cheats.json
5do cheat export --game ABCD1234 --out my-cheats.json

# Use a cheat file or a one-off code for a run.
5do run game.chd --bios bios/panafz1.bin --cheat cheats/ABCD1234.json
5do run game.chd --bios bios/panafz1.bin --cheat-code "write8 0x00200000 = 1"

# Load the per-game file from the configured cheat directory.
5do run game.chd --bios bios/panafz1.bin --cheat-auto --cheats-dir C:\cheats
```

`run --json` reports `cheatCount`, `cheatWrites`, and `cheatPath`. `--cheat-auto`
silently uses an empty set when the game has no file; an explicit `--cheat` path must
exist. `--cheat` and `--cheat-auto` cannot be combined.

## Desktop shell

**Emulation > Cheats...** opens the per-game manager:

- Check a cheat to enable it.
- Select an entry to edit its name, description, or code.
- Add, Update, and Delete write the JSON file immediately.
- Invalid code is shown in red and is not applied.
- The running machine picks up the new set without restarting; the rewind history is
  cleared when the set changes.

Cheats are applied before each frame, so loading a save state does not disable them.
The saved state itself contains the RAM values produced by the cheats; the file remains
the source of truth for which entries are enabled.

## Tests and evidence

- Parser/engine tests cover raw and typed writes, memory conditions, button conditions,
  logical operators, malformed code, disabled entries, duplicate names, and state-load
  reapplication.
- Store tests cover atomic roundtrip, missing files, invalid-code preservation, malformed
  JSON, unsupported document versions, and game ID normalization.
- CLI process tests cover add/list/enable/disable/remove and malformed-code errors.
- The `run --cheat-code` smoke path reports the write count in `run --json` and the
  patched bytes in `--dump-ram`.
