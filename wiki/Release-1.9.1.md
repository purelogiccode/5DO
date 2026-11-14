# 5DO 1.9.1

**Released:** 2026-11-14

5DO 1.9.1 is a **localization and quality-of-life update**. It adds seven UI languages
to English and German, fixes switching the desktop back to English without a restart,
and adds a two-step Escape pause/quit shortcut. The emulation core and compatibility
model are unchanged.

## UI languages

- **Brazilian Portuguese (`pt-br`)**, **French (`fr`)**, **Spanish (`es`)**, **Italian
  (`it`)**, **Japanese (`ja`)**, **Korean (`ko`)**, and **Simplified Chinese (`zh`)**
  join English and German.
- Each catalog covers every non-tooltip shell/dialog/CLI string plus the core shell
  tooltips; missing secondary tooltips fall back to English.
- **Settings > General > UI language** lists all nine catalogs. `--locale <code>`
  overrides the saved setting for one launch; `--list-locales` prints the catalogs.
- `pt`, `pt-PT`, and `pt-BR` resolve to `pt-br`; `zh-CN` and `zh-Hans` resolve to `zh`.
- The desktop executable writes `--list-locales` as UTF-8.

## Fixes

- Selecting English after German now restores the English labels immediately instead of
  requiring a restart. The same fix works when the launch used `--locale`.
- The three analysis-only `GameLibraryWindow` symbol errors reported by
  `jb inspectcode` are resolved.

## Two-step Escape

- The first **Escape** while a game is running pauses it and shows
  `Paused. Press Escape again to quit.`
- The second **Escape** closes 5DO.
- Fullscreen exit, the quick menu, the library, and text entry keep priority. Resuming,
  stopping, opening the quick menu, or losing window focus disarms the quit step.

## Validation

- Full suite **2,259/2,259** passes in Debug with .NET SDK 11.0.100.
- Build, locked restore, `dotnet format --verify-no-changes`, and
  `jb inspectcode --severity=WARNING` are clean.
- Trimmed win-x64 CLI and frontend publishes pass; the frontend smoke writes
  `FRONTEND SMOKE OK 1.9.1`. French and Japanese smoke runs write
  `firstMenuHeader=Fichier` and `firstMenuHeader=ファイル`.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.9.1-win-x64.zip` |
| Windows arm64 | `5do-1.9.1-win-arm64.zip` |
| Linux x64 | `5do-1.9.1-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.9.1-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.9.1-osx-x64.dmg` |
| macOS arm64 | `5do-1.9.1-osx-arm64.dmg` |

Install the **.NET 11 Runtime** first; the bundles do not include it. See the
[release page](https://github.com/purelogiccode/5DO/releases/tag/v1.9.1) for
`SHA256SUMS.txt`.
