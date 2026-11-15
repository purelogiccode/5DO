# What's new in 5DO 1.11.0

**Released:** 2026-11-15

5DO 1.11.0 is a **RetroAchievements continuity and startup** update. Achievement hit
counts and leaderboard attempts now survive save-state loads, the desktop apphost is
renamed to **`FiveDo.exe`** / **`FiveDo`**, and the shell reaches its first window
faster by deferring non-essential startup work and precompiling the bundled frontend.
The emulation core, deterministic hashes, and the documented compatibility model are
unchanged.

## Highlights

### Save-state hit persistence

- Achievement hit counts, measured progress, trigger state, delta/prior memory-reference
  values, and active leaderboard attempts are written into a dedicated
  `RetroAchievements` section of the normal `.tdos` save state.
- Loading a state restores that progress instead of resetting the achievement runtime.
  The service validates the game hash and every definition before applying the section;
  a state from a different game or an updated definition set is discarded safely and
  falls back to a normal reset.
- Save slots, quick save/load, and the save-slot manager all use the same path.
- A save state written before an unlock does not clear an achievement unlocked later in
  the session; unlocks remain authoritative on the server and in the local session.

### `FiveDo.exe` is the desktop app

- The frontend assembly and apphost are now **`FiveDo`**, so the Windows bundle runs
  `FiveDo.exe` and the Linux/macOS apphost runs `FiveDo`.
- The CLI remains `FiveDo.Cli.exe` / `FiveDo.Cli`.
- Windows shortcuts, file associations, the Linux desktop entry, Debian/RPM packaging,
  the macOS app bundle, `PACKAGE.txt`, CI smoke commands, and the documentation all use
  the new name.

### Faster time to the main window

- Audio initialization is lazy: the OpenAL device opens when a machine starts or the user
  applies audio settings, not while the library window is being constructed.
- The remembered BIOS and disc are opened after the shell paints instead of blocking the
  first frame. Library metadata and disc-health caches load on the background scan
  instead of the UI thread.
- Release frontend bundles are now **ReadyToRun** precompiled for each RID, reducing
  cold-start JIT work. The validation host reached the first window in roughly
  **0.62 s** with the ReadyToRun bundle versus **1.09 s** with the debug build; the
  exact time depends on storage, antivirus, and the configured library.
- The application-stats request is explicitly backgrounded and never participates in
  the first-window path.

## RetroAchievements fixes carried forward

- Hardcore sessions keep casual-only achievements active so they can be earned again
  for hardcore credit.
- Leaderboard entries are submitted only from hardcore sessions, and the submission
  toast appears only after the server accepts the score.
- The durable offline unlock queue filters by the active game and retries on login and
  when that game's session starts.
- Fast-forward and creating save states remain available in hardcore; save-state loading,
  rewind, frame advance, cheats, TAS/movie playback and recording, input recording, the
  movie editor, and the input timeline remain blocked.
- Casual-to-hardcore switches reset the game before the hardcore session starts.

## Validation

- Full suite **2,344/2,344** in Debug.
- Build **0 warnings / 0 errors**; `dotnet format --verify-no-changes` clean.
- `jb inspectcode --severity=WARNING` reports **0 results**.
- Frontend smoke test passes with the renamed `FiveDo.exe` apphost.
- New tests cover progress export/import, hit-count restoration, leaderboard attempt
  persistence, and the save-state section round-trip.
- Live RetroAchievements login, game data, session, ping, hardcore session, and a real
  casual award remain validated; leaderboard submission is covered by request-shape and
  signature tests.

## Requirements

- The **.NET 11 Runtime** installed on the host. Bundles are framework-dependent and do
  **not** include the runtime.
- A legally obtained 1 MB 3DO BIOS ROM and legally obtained 3DO disc images or a
  compatible physical optical drive.
- Optional: a RetroAchievements account and network access for achievements.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.11.0-win-x64.zip` |
| Windows arm64 | `5do-1.11.0-win-arm64.zip` |
| Linux x64 | `5do-1.11.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.11.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.11.0-osx-x64.dmg` |
| macOS arm64 | `5do-1.11.0-osx-arm64.dmg` |

See the [release page](https://github.com/purelogiccode/5DO/releases) and
[RetroAchievements.md](RetroAchievements.md).
