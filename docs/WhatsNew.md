# What's new in 5DO 1.9.1

**Released:** 2026-11-14

5DO 1.9.1 is a **localization and quality-of-life update**. It adds seven UI languages
to English and German, fixes switching the desktop back to English without a restart,
and adds a RetroArch-style two-step Escape shortcut. The deterministic emulation core,
frame/audio/state hashes, and the documented compatibility model are unchanged.

## Highlights

### Nine UI languages

- The desktop shell and CLI now load **English, German, Brazilian Portuguese, French,
  Spanish, Italian, Japanese, Korean, and Simplified Chinese** catalogs.
- Every translated catalog covers the full shell, dialogs, CLI-facing strings, and the
  core shell tooltips (591 keys, mirroring the existing German catalog). Secondary
  tooltips without a key fall back to English.
- **Settings > General > UI language** lists every catalog. `--locale <code>` overrides
  the saved setting for one launch, and `--list-locales` prints the embedded catalogs.
- Language-only and regional codes resolve sensibly: `pt`, `pt-PT`, and `pt-BR` select
  `pt-br`; `zh-CN` and `zh-Hans` select `zh`.
- The frontend prints `--list-locales` as UTF-8, so native names stay readable in
  terminals and redirected logs.

### Language switching works in both directions

- The desktop localization pass now remembers each control's original English text and
  retranslates from that source on every locale change.
- Selecting **English** after German (or any other catalog) restores the English labels
  and tooltips immediately instead of requiring an application restart.
- Changing the language also works when the launch used `--locale`, because the pass
  compares the active catalog rather than only the saved setting.

### Two-step Escape

- While a game is running, the **first Escape** pauses emulation and shows
  `Paused. Press Escape again to quit.`
- The **second Escape** closes 5DO, matching the requested RetroArch-style flow.
- Fullscreen exit, the quick menu, the library view, and text-entry focus keep priority.
  Resuming or stopping emulation, opening the quick menu, or losing window focus disarms
  the quit step so the app is not closed by surprise.

## Fixed

- Switching the UI language back to English no longer leaves the previous language on
  screen until restart.
- `--list-locales` from the desktop executable now writes UTF-8, so non-ASCII display
  names are not corrupted.
- The three long-standing analysis-only `GameLibraryWindow` symbol errors in
  `jb inspectcode` are resolved with an explicit type alias.

## Validation

- Full suite **2,259/2,259** passes in Debug with .NET SDK 11.0.100.
- `dotnet restore --locked-mode`, `dotnet build`, and
  `dotnet format --verify-no-changes` are clean.
- `jb inspectcode --severity=WARNING` reports **0 results**.
- Trimmed win-x64 single-file CLI and frontend publishes pass; the frontend smoke writes
  `FRONTEND SMOKE OK 1.9.1`. French and Japanese smoke runs write `firstMenuHeader=Fichier`
  and `firstMenuHeader=ファイル`.
- CI covers Windows, Linux, and macOS; trimmed single-file publishes; NativeAOT apphosts;
  and the six release bundles.

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

Each bundle contains the framework-dependent single-file CLI and desktop app, `README.md`,
`LICENSE`, `THIRD-PARTY-NOTICES.md`, `whatsnew.md`, and the release/legal docs. See the
[release page](https://github.com/purelogiccode/5DO/releases/tag/v1.9.1) for
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
