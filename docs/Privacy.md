# Privacy

5DO is a local desktop application. It does not include telemetry, analytics, crash
reporting, advertising, or an automatic update service.

## What stays on your machine

The desktop app stores its state in the local 5DO data directory:

- `settings.json` — configuration and paths.
- `nvram.ram` and per-game NVRAM images — console save memory.
- `states/` — save slots and thumbnails.
- `cheats/` — per-game cheat files.
- `library-state.json` and `library-metadata.json` — favorites, play time, and offline
  library metadata.
- `library-cache.json` — scanned game metadata.
- `logs/` — rolling diagnostic logs.

Use **Settings > Paths** or **About** to see the exact directory. Portable mode keeps
the same data beside the executable.

## Diagnostics bundles

**Help > Save Diagnostics Bundle...** and the `diagnostics bundle` CLI command create a
local ZIP archive for support. Before writing the archive, 5DO:

- replaces the user profile, app directory, and temp directory with placeholders;
- replaces the account name when it is at least three characters long;
- removes known path-valued settings;
- replaces path-like values in settings JSON with `<path>`;
- includes only the state, frame, or movie evidence you explicitly select.

A bundle can contain:

- `manifest.json` — product, version, OS, architecture, and included evidence.
- `environment.txt` — runtime and environment details.
- `settings.json` — sanitized settings, when selected.
- `logs/*.log` — the newest log files, truncated to the configured limit.
- `state.bin`, `frame.png`, `input.replay` — optional evidence, when selected.

Review a bundle before uploading it. Screenshots and save states can still contain
game content, and a log can contain a title or path that the scrubber did not recognize.

## Network access

The emulator itself does not make network requests. The documentation site is a static
GitHub Pages site. GitHub may collect standard web-server logs according to GitHub's
privacy statement. The docs theme may load web fonts from Google Fonts; the emulator
does not.

Bug reports, discussions, and pull requests are hosted by GitHub and are subject to
GitHub's terms and privacy policy.

## Copyrighted content

Do not upload BIOS ROMs, game images, or extracted game assets to issues, discussions,
pull requests, or diagnostics bundles. Describe the file and provide hashes or
synthetic fixtures instead. See [Licensing](Licensing.md).
