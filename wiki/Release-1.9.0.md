# 5DO 1.9.0

**Released:** 2026-11-14

5DO 1.9.0 is a feature and interoperability release. It adds English/German
localization, external metadata and media attachments, physical optical-drive boot, a
Windows DirectInput/HID gamepad backend, a controller preview, auto-crop, display-sleep
inhibition, Lua input scripting with an editable input timeline, and best-effort 4DO
`.4dosav` import/export.

## Highlights

- **Localization.** Embedded English and German catalogs, Settings > General > UI
  language, `--locale`, and `--list-locales`. The translated smoke marker is
  `locale=de` / `firstMenuHeader=Datei`.
- **Metadata and media.** `metadata lookup` caches an external catalog; `metadata media`
  and the Game Metadata window attach, remove, and open manual media files.
- **Physical optical drives.** File > Boot Optical Drive... and drive-root CLI paths read
  the host's 2048-byte sectors. Data discs boot; CD-DA from a physical drive is not
  exposed yet.
- **Gamepads.** Windows DirectInput/HID enumeration adds axes, POV hats, and triggers
  alongside XInput. Input Bindings draws a live 3DO pad. `--gamepad-probe` diagnoses the
  active source.
- **Presentation.** Auto-crop uses 4DO's 25% detector and a one-second hysteresis.
  Windows display-sleep inhibition runs while a game is active. OpenGL framebuffer
  failures fall back to the CPU presenter.
- **TAS and state interop.** Lua input scripts run under MoonSharp's hard sandbox.
  `movie timeline export|set|apply` and **Emulation > Input Timeline...** edit movies.
  `state import-4do` / `state export-4do` provide best-effort 4DO interop.

## Fixes

- Invalid FILETIME values on removable/network volumes no longer abort the library scan.
- External metadata with a null title/path no longer matches every query; null records
  are dropped and `metadata lookup --write` preserves media and the local disc path.
- Empty optical drives dispose their device handle; the DirectInput enumeration callback
  stays rooted.
- Dependencies updated (Meziantou.Analyzer 3.0.340, CHDSharp 1.7.0, SkiaSharp 4.156.0,
  Microsoft.NET.Test.Sdk 18.12.0, MoonSharp 2.0.0).

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

## Requirements and limits

- The **.NET 11 Runtime** is required; bundles do not include it.
- Supply a legally obtained 3DO BIOS and disc images.
- Windows x64 is the validated GPU target. Linux GPU presentation remains
  preview/best-effort.
- Physical optical drive and DirectInput evidence still needs the relevant hardware.
- 4DO state import/export is best-effort and does not guarantee native compatibility.
