# 5DO 1.10.0

**Released:** 2026-11-15

5DO 1.10.0 is the **RetroAchievements release**. It brings a managed rcheevos-compatible
client, 3DO disc identification, achievements, rich presence, leaderboards, hardcore
rules, OSD notifications, and a durable offline unlock queue to the desktop emulator.
Emulation accuracy, deterministic frame/state hashes, and the documented compatibility
model are unchanged.

> **Hardcore credit is pending RetroAchievements client approval.** Until the
> `5DO/1.10.0` User-Agent is recognized, the server injects the zero-point
> `Warning: Unknown Emulator` achievement and treats hardcore unlocks as casual.
> Casual unlocks work normally. See
> [RetroAchievementsCompliance.md](RetroAchievementsCompliance.md).

## Highlights

### Account, achievements, and progress

- **Configuration > RetroAchievements...** signs in with a password or API token and
  stores the returned token in `settings.json`.
- Loading a 3DO disc computes the RA hash with the `rc_hash_3do` algorithm. The result was
  verified byte-for-byte against RAHasher 1.8.3 on real CHD images.
- The window lists every core achievement with title, description, points, measured
  progress, unlock state, rarity, and unsupported-definition markers.
- The status bar shows the account or `unlocked/total`, points, and pending unlocks.
- Unlock, progress, leaderboard, game-identification, and error messages appear in the
  existing OSD queue.

### Managed rcheevos engine

- `FiveDo.Achievements` is an in-tree C# port of the MIT rcheevos engine: operands,
  conditions, condition sets, triggers, values, leaderboards, rich presence, formatting,
  and runtime containers.
- Supported logic includes measured progress, `ResetIf`, `PauseIf`, `AddSource`,
  `SubSource`, `AddAddress`, `Remember`/`recall`, `AddHits`/`SubHits`,
  `AndNext`/`OrNext`, `ResetNextIf`, BCD/inverted operands, floats, delta/prior values,
  indirect memory reads, lookup tables, conditional rich presence, and leaderboard
  start/cancel/submit triggers.
- The engine evaluates on the emulation thread once per frame. Fast-forward evaluates
  every hidden frame; run-ahead's speculative frames are excluded so hit counts do not
  double-count replayed frames.

### Hardcore mode

- Blocks loading save states, rewind, frame advance, cheats, movie/TAS playback and
  recording, input recording, the movie editor, and the input timeline.
- Fast-forward and creating save states remain available, matching the RA rules. The
  save-slot manager can save and delete in hardcore but cannot load.
- Casual to hardcore transitions reset the game before the hardcore session starts;
  hardcore to casual transitions do not reset.
- Rich presence is submitted even when the local display option is off. Leaderboard
  entries are submitted only from hardcore sessions.
- Casual-only unlocks remain active in hardcore so they can be earned again with
  hardcore credit. Hardcore unlocks count for both modes.
- The UI labels hardcore as `RA hardcore (pending)` until RA approves the client.

### Offline unlocks

- Pending awards are checksummed, written atomically to
  `retroachievements-pending.json`, and retried every 30 seconds, after login, and when
  the matching game session starts.
- The queue stores no API token, keeps entries for 30 days, caps at 1,000 records, and
  quarantines corrupt files as `.bad`.
- The queue filters by the active game, so an unlock for another game cannot block the
  current game's sync.
- Leaderboard scores are not queued offline; they require a live connection and are
  hardcore-only.

## Reliability and fixes

- Achievement evaluation, notification handlers, and frame-completed callbacks cannot
  fault the emulation loop.
- Save-state loads, rewind, and movie state restoration reset runtime hit counts and
  measured progress in casual mode.
- The 3DO hasher rejects malformed OperaFS directory chains instead of looping.
- The Web API client treats a non-JSON HTTP 200 response as an unavailable service.
- Diagnostics bundles remove the RetroAchievements username, API token, and custom host.
- RetroAchievements login no longer logs the account name.

## Validation

- Full suite **2,338/2,338** in Debug.
- Build **0 warnings / 0 errors**; `dotnet format --verify-no-changes` clean.
- `jb inspectcode --severity=WARNING` reports **0 results**.
- Frontend smoke test passes.
- Live login, game data (`r=patch`), session start, rich-presence ping, hardcore session
  start, and a real softcore award on Gex (achievement 579967) passed.
- The tested 3DO sets had no leaderboards, so live leaderboard submission remains
  unverified. Request shape and signature are covered by tests and match rcheevos.

## Requirements

- The **.NET 11 Runtime** installed on the host. Bundles are framework-dependent and do
  not include the runtime.
- A legally obtained 1 MB 3DO BIOS ROM.
- Legally obtained 3DO disc images or a compatible physical optical drive.
- Optional network access and a RetroAchievements account for achievements.

## Known issues

- Hardcore credit awaits RetroAchievements client approval and the six-month
  public-availability window. See [RetroAchievementsCompliance.md](RetroAchievementsCompliance.md).
- Bonus, specialty, and exclusive RA subsets are not selected. Unofficial achievements
  are listed but not activated.
- The achievement window is a text/progress list; RetroArch-style badge overlays and a
  leaderboard browser are not implemented.
- The API token is stored in plain `settings.json`; protect the data directory.
- Full compatibility remains documented, not exhaustive. See
  [KnownIssues.md](KnownIssues.md) and [Compatibility.md](Compatibility.md).

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.10.0-win-x64.zip` |
| Windows arm64 | `5do-1.10.0-win-arm64.zip` |
| Linux x64 | `5do-1.10.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.10.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.10.0-osx-x64.dmg` |
| macOS arm64 | `5do-1.10.0-osx-arm64.dmg` |

SHA-256 checksums are published as `SHA256SUMS.txt` on the
[release page](https://github.com/purelogiccode/5DO/releases/tag/v1.10.0).
