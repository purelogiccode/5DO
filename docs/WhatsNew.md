# What's new in 5DO 1.9.0

**Released:** 2026-11-14

5DO 1.9.0 is a **feature and interoperability release**. It adds localization, external
metadata and media attachments, physical optical-drive boot, a Windows DirectInput/HID
gamepad backend, a live controller preview, automatic blank-border crop, display-sleep
inhibition, Lua input scripting with an editable input timeline, and best-effort 4DO
`.4dosav` import/export. The emulation core and the documented compatibility model are
unchanged.

## Highlights

### English and German UI

- The desktop shell and CLI now load embedded **English and German** catalogs.
- **Settings > General > UI language** selects `system`, `en`, or `de`; `--locale <code>`
  overrides it for one desktop launch. `--list-locales` prints the available catalogs.
- Menus, labels, tabs, placeholders, and tooltips are translated after each window is
  realized. Data-bound values, JSON output, and machine diagnostics stay stable.
- A translated smoke run records `locale=de` and `firstMenuHeader=Datei`.

### Metadata and manual media

- `metadata lookup` downloads a versioned library-metadata catalog over HTTP(S) or a
  `file:` URI, validates it, writes a deterministic cache, and searches by game ID,
  title, or normalized containment.
- `metadata media` and the **Game Metadata** window attach, remove, open, and list manual
  media files. Attachments are metadata only; 5DO never copies or modifies the files.

### Physical optical drives

- **File > Boot Optical Drive...** lists attached CD/DVD drives with media and boots the
  selected disc through the host's cooked 2048-byte sector interface.
- Drive-root paths such as `D:`, `\\.\D:`, and `/dev/sr0` work in the CLI.
- Data discs boot like the equivalent image. CD-DA playback from a physical drive is not
  exposed yet.

### Gamepads and controller preview

- Windows enumerates **DirectInput/HID** pads alongside XInput, including axes, POV hats,
  and calibrated triggers. XInput remains the fallback and duplicate Xbox pads are
  filtered out.
- **Input Bindings** shows a live 3DO pad: mapped buttons are outlined, the selected row
  is amber, and a held key or gamepad control turns its button green.
- `--gamepad-probe` prints the active source and raw controller samples.

### Presentation

- **Auto-crop borders** uses 4DO's 25%-per-edge detector and a one-second hysteresis.
  Manual and per-game crops override it; hashes, audio, input, and save states do not
  change.
- **Display-sleep inhibition** keeps the Windows display and system awake while a game is
  running and unpaused, and releases on pause, stop, fault, and exit. Linux and macOS
  remain best-effort.
- The OpenGL framebuffer-failure path now falls back to the CPU presenter instead of
  raising an unhandled UI exception every frame.

### TAS and 4DO state interop

- **Lua input scripting** runs under MoonSharp's hard sandbox. `run` and `movie record`
  accept `--input-lua`; the API exposes `frame`, `hold`, `press`, and `release` (also
  `input.*`). The emitted entries replay through the normal deterministic movie path.
- **Emulation > Input Timeline...** draws one row per pad button with a playhead. The CLI
  provides `movie timeline export|set|apply` and preserves the embedded state and other
  controller slots.
- `state import-4do` and `state export-4do` map ARM, memory (with high-resolution
  mirrors), MADAM, CLIO, DSP, SPORT, VDLP, Quartz, and XBUS. Unsupported fields are
  reported as warnings. Native 4DO state compatibility is not guaranteed.

## Fixed

- Removable and network volumes with an invalid FILETIME no longer abort the library scan
  with `Not a valid Win32 FileTime`.
- The library and disc-health caches keep an unknown stamp instead of re-hashing the file
  on every scan.
- An empty or unreadable optical drive disposes its device handle before reporting the
  error.
- External metadata with a null title/path no longer matches every query; null records
  are dropped, and `metadata lookup --write` preserves existing media and the local disc
  path.
- The DirectInput enumeration callback stays rooted for the native call.
- 4DO export writes the 64-byte XBUS device-offset table required by FreeDOCore's
  loader.
- Dependencies updated: Meziantou.Analyzer 3.0.340, CHDSharp 1.7.0, SkiaSharp 4.156.0,
  Microsoft.NET.Test.Sdk 18.12.0, and MoonSharp 2.0.0.

## Validation

- Full suite **2,252/2,252** passes in Debug with .NET SDK 11.0.100.
- `dotnet build`, `dotnet restore --locked-mode`, `dotnet format --verify-no-changes`, and
  a clean-cache `jb inspectcode --severity=WARNING` are clean.
- Trimmed win-x64 single-file CLI and frontend publishes pass; the frontend smoke writes
  `FRONTEND SMOKE OK 1.9.0`, `locale=de`, and `firstMenuHeader=Datei`.
- CI covers Windows, Linux, and macOS; trimmed single-file publishes; NativeAOT apphosts;
  and the six release bundles.
- Each bundle contains the CLI, desktop app, `README.md`, `LICENSE`,
  `THIRD-PARTY-NOTICES.md`, `whatsnew.md`, and the release/legal docs, with no runtime,
  PDB, or XML files.

## Requirements

- The **.NET 11 Runtime** installed on the host. The bundles are framework-dependent and
  do **not** include the runtime. Choose the x64 or arm64 bundle that matches your OS.
- A legally obtained 1 MB 3DO BIOS ROM (for example `panafz1.bin`).
- Legally obtained 3DO disc images in ISO, BIN, CUE+BIN, CHD, or a compatible physical
  optical drive.
- Optional: network access for the online cover catalog and external metadata lookup.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.9.0-win-x64.zip` |
| Windows arm64 | `5do-1.9.0-win-arm64.zip` |
| Linux x64 | `5do-1.9.0-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.9.0-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.9.0-osx-x64.dmg` |
| macOS arm64 | `5do-1.9.0-osx-arm64.dmg` |

Each bundle contains the framework-dependent single-file CLI and desktop app, `README.md`,
`LICENSE`, `THIRD-PARTY-NOTICES.md`, `whatsnew.md`, and the release/legal docs. See the
[release page](https://github.com/purelogiccode/5DO/releases/tag/v1.9.0) for
`SHA256SUMS.txt`.

## Known limits

- Playability follows the documented binary beyond-title rule, not a full playthrough.
- CD command cadence remains the residual differential on the 50-title panel.
- Windows x64 remains the validated GPU target; Linux GPU presentation under
  llvmpipe/SSH is preview/best-effort.
- Physical optical drive and DirectInput evidence still needs a host with the relevant
  hardware.
- 4DO state import/export is best-effort and does not guarantee native compatibility.
- Signing and notarization require a Windows certificate and an Apple developer account.
