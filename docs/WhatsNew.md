# What's new in 5DO 1.10.0

**Released:** 2026-11-15

5DO 1.10.0 adds **RetroAchievements** to the desktop emulator. The MIT rcheevos
evaluation engine is ported to managed C#, 3DO disc hashing matches RAHasher, and the
shell now supports sign-in, achievements, measured progress, rich presence, leaderboards,
hardcore rules, and OSD notifications. The emulation core, deterministic hashes, and the
documented compatibility model are unchanged.

## Highlights

### RetroAchievements

- **Configuration > RetroAchievements...** provides account sign-in with a password or
  API token, enable/hardcore switches, notification options, the current disc hash, and
  the full achievement list for the loaded game.
- 3DO discs are identified with the same `rc_hash_3do` algorithm as RetroArch. The hash
  was verified byte-for-byte against RAHasher 1.8.3 on real CHD images.
- Achievements and leaderboards are evaluated on the core thread once per emulated frame.
  The managed engine covers measured progress, `ResetIf`/`PauseIf`, `AddSource`,
  `SubSource`, `AddAddress`, `Remember`, `AddHits`/`SubHits`, `AndNext`/`OrNext`,
  BCD/inverted operands, floats, delta/prior values, indirect reads, lookup tables,
  conditional rich presence, and leaderboard start/cancel/submit triggers.
- The status bar shows the account or the current `unlocked/total`, points, and
  pending-sync count. Unlock, progress, leaderboard, game-identification, and error
  messages use the existing OSD queue.

### Hardcore rules

- Hardcore blocks **loading** save states, rewind, frame advance, cheats, TAS/movie
  playback and recording, input recording, the movie editor, and the input timeline.
- Fast-forward and **creating** save states remain available, matching the published RA
  rules. The save-slot manager can save and delete in hardcore, but its load action is
  disabled.
- Switching from casual to hardcore resets the game before the hardcore session starts.
  Switching hardcore to casual is allowed without a reset.
- Rich presence is always submitted in hardcore even when the local display option is
  off. Leaderboard entries are submitted only from hardcore sessions.
- Casual-only unlocks remain active in a hardcore session, so a player can earn the
  hardcore version of an achievement they previously unlocked in casual.

### Durable offline unlocks

- Unlocks created while the server is unavailable are checksummed, written atomically to
  `retroachievements-pending.json`, and retried every 30 seconds, after login, and when
  the matching game session starts.
- The queue never stores the API token, keeps entries for up to 30 days, and is pruned to
  1,000 records. Corrupt files are quarantined as `.bad`.
- Entries for other games no longer block the current game's queued unlocks.

### Reliability

- Achievement evaluation, notification handlers, and frame-completed callbacks are
  isolated from the emulation loop, so an achievements or UI failure cannot stop the
  game.
- Loading a save state, rewinding, or restoring a movie state now resets achievement hit
  counts and measured progress in casual mode.
- The 3DO hasher rejects malformed OperaFS directory chains instead of looping, and the
  Web API client treats a non-JSON HTTP 200 response as an unavailable service instead of
  faulting the caller.
- Diagnostics bundles remove the RetroAchievements account name, API token, and custom
  host from exported settings.

## Fixed

- Hardcore mode no longer blocks fast-forward or saving states, both of which
  RetroAchievements permits.
- Hardcore sessions now correctly keep casual-only achievements active instead of
  treating them as already completed.
- Casual sessions no longer submit leaderboard entries that RetroAchievements would not
  record.
- The offline queue now filters by the active game, so an unsynced unlock from another
  game cannot strand the current game's awards.
- Save-state loads, rewind, and movie state restores now reset runtime hit counts.
- The RA session now starts after the casual-to-hardcore reset rather than before it.

## Validation

- Full suite **2,338/2,338** passes in Debug with .NET SDK 11.0.100.
- Build is clean with **0 warnings and 0 errors**; `dotnet format --verify-no-changes`
  is clean.
- `jb inspectcode --severity=WARNING` reports **0 results**.
- New tests cover hardcore mode-specific unlocks, casual versus hardcore leaderboard
  submission, and queue entries for a non-active game.
- Live validation passed for login, game data (`r=patch`), session start, rich-presence
  ping, a hardcore session, and a real softcore award on Gex (game 1009, achievement
  579967, score `0 -> 1`). The 3DO sets used for validation did not expose leaderboards.

## Requirements

- The **.NET 11 Runtime** installed on the host. The bundles are framework-dependent and
  do **not** include the runtime. Choose the x64 or arm64 bundle that matches your OS.
- A legally obtained 1 MB 3DO BIOS ROM (for example `panafz1.bin`).
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, CHD, or a compatible physical
  optical drive.
- Optional: a RetroAchievements account and network access for achievements. Local
  covers, the core emulator, and cached metadata work offline.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.10.0-win-x64.zip` |
| Windows arm64 | `5do-1.10.0-win-arm64.zip` |
| Linux x64 | `5do-1.10.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.10.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.10.0-osx-x64.dmg` |
| macOS arm64 | `5do-1.10.0-osx-arm64.dmg` |

See the [release page](https://github.com/purelogiccode/5DO/releases) and
[RetroAchievements.md](RetroAchievements.md).
