# 5DO 1.7.1

**Released:** 2026-11-10

5DO 1.7.1 is a **desktop playback and dependency update**. It removes the startup
stutter and audio underruns caused by the host disc prefetcher shadowing CHDSharp's
own read-ahead, and refreshes the Avalonia, CLI, and test dependencies.

## Highlights

- **Smooth startup on CHD images.** CHD sources now use CHDSharp's built-in hunk
  read-ahead directly instead of being wrapped in a second sector prefetcher. The
  previous look-ahead could hold the CHD reader lock while the emulation thread waited,
  producing multi-frame rendering stalls and audio underruns during the first seconds of
  a game.
- **Background prefetch no longer competes with emulation.** The remaining sector
  prefetcher for uncompressed images runs at below-normal priority and yields between
  read-ahead sectors.
- **Dependency refresh.** Avalonia 12.1.6, System.CommandLine 3.0.0, and
  Microsoft.NET.Test.Sdk 18.11.1 are used by the fresh bundles.

## Validation

- Full suite **2,125/2,125** passes in Debug.
- `dotnet build` reports **0 warnings and 0 errors**; `dotnet format
  --verify-no-changes` and `jb inspectcode --severity=WARNING` are clean.
- A 20-second desktop Road Rash run reports **0 audio underruns** and no skipped frames.
- Lemmings and Space Ace desktop runs are clean in repeated 20-second checks.
- All six bundles contain the CLI, desktop app, README, license, notices, `whatsnew.md`,
  and release/legal docs, with no runtime, PDB, or XML files.

## Requirements

- The **.NET 10 Runtime** installed on the host. The bundles are framework-dependent and
  do **not** include the runtime. Choose the x64 or arm64 bundle that matches your OS.
- A legally obtained 1 MB 3DO BIOS ROM (for example `panafz1.bin`).
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, or CHD format.
- Optional: network access for the online cover catalog. Local covers and the core
  emulator work offline.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.7.1-win-x64.zip` |
| Windows arm64 | `5do-1.7.1-win-arm64.zip` |
| Linux x64 | `5do-1.7.1-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.7.1-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.7.1-osx-x64.dmg` |
| macOS arm64 | `5do-1.7.1-osx-arm64.dmg` |

Each bundle contains the framework-dependent single-file CLI and desktop app, `README.md`,
`LICENSE`, `THIRD-PARTY-NOTICES.md`, `whatsnew.md`, and the release/legal docs. See the
[release page](https://github.com/purelogiccode/5DO/releases/tag/v1.7.1) for
`SHA256SUMS.txt`.

## Known limits

- Playability follows the documented binary beyond-title rule, not a full playthrough.
- CD command cadence remains the residual differential on the 50-title panel.
- Windows x64 remains the validated GPU target; Linux GPU presentation under
  llvmpipe/SSH is preview/best-effort.
- Physical macOS gamepad detection still needs a macOS host with a connected controller.
- Signing and notarization require a Windows certificate and an Apple developer account.
