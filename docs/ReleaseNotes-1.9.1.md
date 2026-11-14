# 5DO 1.9.1

**Released:** 2026-11-14

5DO 1.9.1 is a **localization and quality-of-life update**. It adds seven UI languages
to English and German, fixes switching the desktop back to English without a restart, and
adds a two-step Escape pause/quit shortcut. The deterministic emulation core,
frame/audio/state hashes, and the documented binary compatibility model are unchanged.

## Localization

- Embedded **Brazilian Portuguese (`pt-br`)**, **French (`fr`)**, **Spanish (`es`)**,
  **Italian (`it`)**, **Japanese (`ja`)**, **Korean (`ko`)**, and **Simplified Chinese
  (`zh`)** catalogs join English and German in `FiveDo.Localization`.
- Each catalog mirrors the 591-key German set: every non-tooltip shell/dialog/CLI string
  plus the core shell tooltips. Missing secondary tooltips fall back to English.
- **Settings > General > UI language** lists all nine catalogs. `--locale <code>`
  overrides the saved setting for one desktop launch; `--list-locales` prints the
  embedded catalogs in both executables.
- Regional and language-only codes resolve to the shipped catalog: `pt`, `pt-PT`, and
  `pt-BR` select `pt-br`; `zh-CN` and `zh-Hans` select `zh`.
- The desktop executable writes `--list-locales` as UTF-8 so native display names survive
  terminals and redirected output.
- The localization pass now remembers each control's original English text. Switching
  from German to English (or to another catalog) retranslates immediately instead of
  leaving the previous language on screen until restart. The same fix covers launches
  started with `--locale`.

## Two-step Escape

- While emulation is running, the first **Escape** pauses the game and shows
  `Paused. Press Escape again to quit.`
- The second **Escape** closes 5DO.
- Fullscreen exit, the quick menu, the library view, and text-entry focus keep priority.
  Resuming or stopping emulation, opening the quick menu, or losing window focus disarms
  the quit step.
- The behavior is covered by `EscapeQuitPolicyTests`; the desktop key handler only
  consumes the shortcut in the game view when no text entry has focus.

## Fixes

- Switching the UI language back to English no longer requires an application restart.
- `--list-locales` output from the desktop executable is UTF-8 instead of the Windows
  subsystem code page.
- The three analysis-only `GameLibraryWindow` symbol errors reported by
  `jb inspectcode` are resolved with an explicit type alias, so the warning-severity gate
  is clean.

## Validation

- Full suite **2,259/2,259** passes in Debug with .NET SDK 11.0.100.
- `dotnet restore --locked-mode`, `dotnet build`, and
  `dotnet format --verify-no-changes` are clean.
- `jb inspectcode --severity=WARNING` reports **0 results**.
- Trimmed win-x64 single-file CLI and frontend publishes pass; the frontend smoke writes
  `FRONTEND SMOKE OK 1.9.1`. French and Japanese smoke runs write
  `firstMenuHeader=Fichier` and `firstMenuHeader=ファイル`.
- CI builds and tests on Windows, Linux, and macOS, publishes trimmed CLI/frontend
  single files, validates both NativeAOT apphosts, and produces the six release bundles.
- Each framework-dependent bundle contains the CLI, desktop app, `README.md`, `LICENSE`,
  `THIRD-PARTY-NOTICES.md`, `whatsnew.md`, and the release/legal docs, with no runtime,
  PDB, or XML files.

## Requirements

- The **.NET 11 Runtime** installed on the host. The bundles are framework-dependent and
  do **not** include the runtime. Choose the x64 or arm64 bundle that matches your OS.
- A legally obtained 1 MB 3DO BIOS ROM (for example `panafz1.bin`).
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, CHD, or a compatible physical
  optical drive.
- Optional: network access for the online cover catalog and external metadata lookup.
  Local covers, the core emulator, and cached metadata work offline.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.9.1-win-x64.zip` |
| Windows arm64 | `5do-1.9.1-win-arm64.zip` |
| Linux x64 | `5do-1.9.1-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.9.1-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.9.1-osx-x64.dmg` |
| macOS arm64 | `5do-1.9.1-osx-arm64.dmg` |

See the [release page](https://github.com/purelogiccode/5DO/releases/tag/v1.9.1) for
`SHA256SUMS.txt`.

## Known limits

- Playability follows the documented binary beyond-title rule, not a full playthrough.
- CD command cadence remains the residual differential on the 50-title panel.
- Windows x64 remains the validated GPU target; Linux GPU presentation under
  llvmpipe/SSH is preview/best-effort.
- The bundled Inter font has no CJK glyphs; Japanese, Korean, and Simplified Chinese
  depend on the host's system font fallback.
- Physical optical drive and DirectInput evidence still needs a host with the relevant
  hardware.
- 4DO state import/export is best-effort and does not guarantee native compatibility.
- Signing and notarization require a Windows certificate and an Apple developer account.
