# 5DO 1.11.0

**Released:** 2026-11-15

5DO 1.11.0 is a **RetroAchievements continuity and startup** update. Achievement hit
counts and leaderboard attempts now survive save-state loads, the desktop app is renamed
to `FiveDo.exe` / `FiveDo`, and the main window appears faster.

## Save-state hit persistence

- Achievement hit counts, measured progress, trigger state, delta/prior memory-reference
  values, and active leaderboard attempts are stored in a dedicated
  `RetroAchievements` section of the normal `.tdos` save state.
- Loading a state restores that progress instead of resetting the achievement runtime.
- The game hash and every definition are validated first. A state from another game or
  an updated definition set is discarded and the runtime resets normally.

## Renamed desktop app

- Windows bundles run **`FiveDo.exe`**.
- Linux and macOS bundles run **`FiveDo`**.
- The CLI remains `FiveDo.Cli.exe` / `FiveDo.Cli`.
- Existing settings, saves, BIOS paths, and data folders are unchanged.

## Fullscreen Escape

- Pressing **Escape** while a game is running in fullscreen leaves fullscreen and pauses
  emulation in one step.
- Press **Escape** again to close 5DO. **F11** still toggles fullscreen independently.

## Loading overlay

- Starting a game shows **Loading game... / Please wait...** until the first visible
  frame appears, so the window is not blank while the BIOS, disc, and renderer start.
- The overlay is localized and has a 30-second safety timeout.

## Faster startup

- Audio initialization is lazy and runs when a game starts or audio settings change.
- The remembered BIOS and disc load after the shell paints.
- Library metadata and disc-health caches load inside the background scan.
- Release bundles are ReadyToRun precompiled. On the validation host the first window
  appeared in about **0.62 s** versus **1.09 s** for the Debug build.

## Validation

- Full suite **2,344/2,344** passes in Debug.
- Build, locked restore, `dotnet format --verify-no-changes`, and
  `jb inspectcode --severity=WARNING` are clean.
- The packaged `FiveDo.exe` smoke test writes `FRONTEND SMOKE OK 1.11.0`.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.11.0-win-x64.zip` |
| Windows arm64 | `5do-1.11.0-win-arm64.zip` |
| Linux x64 | `5do-1.11.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.11.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.11.0-osx-x64.dmg` |
| macOS arm64 | `5do-1.11.0-osx-arm64.dmg` |

Install the **.NET 11 Runtime** first; the bundles do not include it. See the
[release page](https://github.com/purelogiccode/5DO/releases/tag/v1.11.0) for
`SHA256SUMS.txt`.
