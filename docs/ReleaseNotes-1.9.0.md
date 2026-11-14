# 5DO 1.9.0

**Released:** 2026-11-14

5DO 1.9.0 is a **feature and interoperability release**. It adds English/German
localization, external metadata lookup with manual media attachments, physical optical
drive boot, a Windows DirectInput/HID gamepad backend, a live controller preview,
automatic blank-border crop, display-sleep inhibition, Lua input scripting with an
editable input timeline, and best-effort 4DO `.4dosav` import/export.

The deterministic emulation core, frame/audio/state hashes, and the documented binary
compatibility model are unchanged.

## Localization

- Embedded **English and German** catalogs in `FiveDo.Localization`; the JSON is parsed
  with `JsonDocument`, so it is trimming- and NativeAOT-safe.
- The desktop shell translates window titles, menus, labels, tabs, placeholders, and
  tooltips after each window is realized. Data-bound values are left alone.
- **Settings > General > UI language** selects `system`, `en`, or `de`. `--locale <code>`
  overrides the saved setting for one desktop launch; `--list-locales` prints the
  embedded catalogs in both executables.
- CLI usage and error text are localized; machine-readable JSON stays in English.
- The translated smoke test records the active locale and first menu header
  (`locale=de`, `firstMenuHeader=Datei`).

## Metadata and media

- `metadata lookup` downloads a version-1 library-metadata catalog from an HTTP(S) or
  `file:` URL, validates the version and 8 MB limit, writes a deterministic atomic cache,
  and searches by exact game ID, title, or normalized containment.
- `metadata media` attaches, detaches, or lists manual media files for a game. The
  desktop **Game Metadata** window can add, remove, and open the same attachments.
- A lookup never blocks the library UI; a failed download falls back to the cache with a
  warning. `--write` keeps existing user attachments and the local disc path.

## Physical optical drives

- **File > Boot Optical Drive...** lists attached CD/DVD drives with media and boots the
  selected disc through the host's cooked 2048-byte sector interface.
- Drive-root CLI paths (`D:`, `\\.\D:`, `/dev/sr0`) are recognized by `run`, `info`, and
  the other disc commands. Reads are read-only and never write to the disc.
- Data discs boot like the equivalent image. CD-DA playback from a physical drive is not
  exposed yet because the source does not implement `ITrackedDiscSource`.

## Gamepads and controller preview

- Windows now enumerates legacy **DirectInput/HID** pads alongside XInput, including
  axes, POV hats, and calibrated triggers. XInput remains the fallback and Xbox pads are
  filtered from the DirectInput list so a device is never exposed twice.
- **Input Bindings** draws a 3DO pad diagram: mapped buttons are outlined, the selected
  row is amber, and a held key or gamepad control turns its button green.
- `--gamepad-probe` prints the active source, connected controller count, and raw samples
  for hardware diagnosis.

## Presentation

- **Auto-crop borders** in Settings > Video > Overscan uses 4DO's 25%-per-edge blank
  border detector and a one-second hysteresis. A manual or per-game crop overrides it.
  Only presentation and native screenshots change; hashes, audio, input, and save states
  are untouched.
- **Display-sleep inhibition** keeps the Windows display and system awake while a game is
  running and unpaused, and releases on pause, stop, fault, and exit. Linux and macOS
  remain best-effort because their compositors can still blank the display.
- Avalonia's `Unable to configure OpenGL FBO` compositor failure now falls back to the
  CPU presenter instead of reporting an unhandled UI exception every frame. The Vulkan
  surface also turns a resize failure into a clean presenter fallback.

## TAS and 4DO state interop

- `FiveDo.Scripting` runs a hard-sandboxed MoonSharp Lua input script and emits
  frame-stamped controller entries. `run` and `movie record` accept `--input-lua`;
  `frame`, `hold`, `press`, and `release` are also available under `input.*`.
- `InputTimeline` extracts player-1 changes from a movie, edits frames, exports a plain
  input script, and applies the result to a movie while preserving the embedded state and
  the other controller slots. CLI: `movie timeline export|set|apply`; desktop:
  **Emulation > Input Timeline...**.
- `state import-4do` maps a FreeDOCore `.4dosav` into a native 5DO state using a freshly
  reset machine as the base. `state export-4do` writes the 32-bit 4DO container. ARM
  banks, memory (including the stock high-resolution mirrors), MADAM, CLIO, DSP, SPORT,
  VDLP, Quartz, and XBUS are mapped; unsupported fields are reported as warnings.
- The 4DO path is explicitly best-effort and versionless/compiler-layout dependent.
  Native 4DO state compatibility is not a goal.

## Fixes

- Library scanning no longer stops with `Not a valid Win32 FileTime` when a removable or
  network volume reports a FILETIME outside the `DateTime` range. The library and
  disc-health caches store an unknown stamp and do not re-hash the file on every scan.
- External metadata records with a null title/path no longer match every lookup, null
  records are dropped on load, and user media attachments survive `metadata lookup
  --write`.
- An empty or unreadable physical optical drive now disposes its device handle before the
  domain error is reported.
- The DirectInput enumeration callback stays rooted for the native call.
- 4DO export now writes the 64-byte XBUS device-offset table required by FreeDOCore's
  loader; the record is structurally valid even when no device data is attached.
- Dependencies updated: Meziantou.Analyzer 3.0.340, CHDSharp 1.7.0, SkiaSharp 4.156.0,
  Microsoft.NET.Test.Sdk 18.12.0, and MoonSharp 2.0.0 for Lua input scripting.

## Validation

- Full suite **2,252/2,252** passes in Debug with .NET SDK 11.0.100.
- `dotnet build`, `dotnet restore --locked-mode`, `dotnet format --verify-no-changes`, and
  a clean-cache `jb inspectcode --severity=WARNING` report no warnings or errors.
- Trimmed win-x64 single-file CLI and frontend publishes pass; the frontend smoke writes
  `FRONTEND SMOKE OK 1.9.0`, `locale=de`, `firstMenuHeader=Datei`, and
  `windowState=Normal`.
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
| Windows x64 | `5do-1.9.0-win-x64.zip` |
| Windows arm64 | `5do-1.9.0-win-arm64.zip` |
| Linux x64 | `5do-1.9.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.9.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.9.0-osx-x64.dmg` |
| macOS arm64 | `5do-1.9.0-osx-arm64.dmg` |

See the [release page](https://github.com/purelogiccode/5DO/releases/tag/v1.9.0) for
`SHA256SUMS.txt`.

## Known limits

- Playability follows the documented binary beyond-title rule, not a full playthrough.
- CD command cadence remains the residual differential on the 50-title panel.
- Windows x64 remains the validated GPU target; Linux GPU presentation under
  llvmpipe/SSH is preview/best-effort.
- Physical optical drive and DirectInput evidence still needs a host with the relevant
  hardware; automated tests cover parsing, translation, and the mapping policy.
- 4DO state import/export is best-effort and does not guarantee native 4DO state
  compatibility.
- Signing and notarization require a Windows certificate and an Apple developer account.
