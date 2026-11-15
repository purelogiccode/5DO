# Privacy

5DO is a local desktop application. It does not include analytics, crash reporting,
advertising, or an automatic update service.

## Update checks

The desktop shell and CLI check GitHub's public release feed once at startup (cached for
several hours). The request contains only the public repository URL and a client user
agent; no user, machine, game, settings, or diagnostics data is sent. GitHub receives the
usual HTTP metadata (IP address, user agent, and requested URL) under its privacy policy.

The update check is enabled by default and can be disabled with **Settings > Video >
Check for updates when 5DO starts**, `--no-update-check`, or `FIVEDO_UPDATE_CHECK=0`.
Opening the download page uses the system browser. 5DO never downloads or installs an
update automatically.

## What stays on your machine

The desktop app stores its state in the local 5DO data directory:

- `settings.json` — configuration and paths.
- `nvram.ram` and per-game NVRAM images — console save memory.
- `states/` — save slots and thumbnails.
- `cheats/` — per-game cheat files.
- `library-state.json` and `library-metadata.json` — favorites, play time, and offline
  library metadata.
- `library-cache.json` - scanned game metadata.
- `update-check.json` - the last successful GitHub release check.
- `retroachievements-pending.json` - queued achievement unlocks, only when
  RetroAchievements is enabled (no API token).
- `logs/` - rolling diagnostic logs.

Use **Settings > Paths** or **About** to see the exact directory. Portable mode keeps
the same data beside the executable.

## Diagnostics bundles

**Help > Save Diagnostics Bundle...** and the `diagnostics bundle` CLI command create a
local ZIP archive for support. Before writing the archive, 5DO:

- replaces the user profile, app directory, and temp directory with placeholders;
- replaces the account name when it is at least three characters long;
- removes known path-valued settings, the RetroAchievements account name, API token, and
  custom host;
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

## RetroAchievements

RetroAchievements is opt-in. When enabled and signed in, 5DO sends the account's API
token, the disc hash, the current rich-presence string, achievement unlocks, leaderboard
scores, and play-session pings to `retroachievements.org` (or the configured host). The
account name and API token are stored in `settings.json`; protect that file like a
password. Pending unlocks are queued locally in `retroachievements-pending.json` with the
title, points, game hash, account name, hardcore flag, and unlock time; the queue never
stores the API token. Diagnostics bundles replace known path-valued settings, but review
any bundle before sharing it. Disabling RetroAchievements stops all requests and clears
the active session; queued unlocks remain until they sync or expire after 30 days.

## Retention, locations, and GDPR

5DO has no telemetry, analytics, crash reporting, or user profiles. Local data is retained
only until you delete it, reset the relevant feature, or remove the 5DO data directory:

- diagnostics logs are rolling and capped by the log settings;
- save states, screenshots, movies, and NVRAM images remain until you delete them;
- the RetroAchievements pending-unlock queue drops entries after 30 days and is removed
  when entries sync;
- `update-check.json` is overwritten on the next successful check;
- uninstalling 5DO does not automatically delete the user data directory, so delete it
  manually if you want complete removal.

Network recipients:

- **GitHub, Inc. (United States)** — update checks, release downloads, the documentation
  site, and issue/PR hosting. GitHub's own retention and privacy terms apply.
- **RetroAchievements.org (independent service)** — only when you enable and sign in.
  Account data, unlocks, leaderboard entries, rich presence, and session pings are stored
  by RetroAchievements under its own privacy policy and retention rules. 5DO does not
  operate those servers and cannot delete data from your RA account; use the account
  controls on RetroAchievements.org for access, correction, or deletion.
- **User-configured catalog/media hosts** — only when you enable online covers or
  metadata and configure a host; those hosts receive the HTTP request for the requested
  asset.

The legal basis for RetroAchievements requests is your opt-in consent, which you can
withdraw at any time by disabling the feature. 5DO performs no automated decision-making
or profiling. Because 5DO retains no telemetry, there is no separate GDPR export or
erasure request to file with the 5DO project; use the local data controls above and
contact RetroAchievements for data held by that service. The project does not sell
personal data.

## Network access

The emulator and game rendering run locally and do not need a network connection. The
startup update check, the optional launch-statistics request, cover/metadata downloads,
and an enabled RetroAchievements session are the requests made by the application; each
can be disabled. The documentation site is a static GitHub Pages site. GitHub may collect
standard web-server logs according to GitHub's privacy statement. The docs theme may load
web fonts from Google Fonts; the emulator does not.

Bug reports, discussions, and pull requests are hosted by GitHub and are subject to
GitHub's terms and privacy policy.

## Copyrighted content

Do not upload BIOS ROMs, game images, or extracted game assets to issues, discussions,
pull requests, or diagnostics bundles. Describe the file and provide hashes or
synthetic fixtures instead. See [Licensing](Licensing.md).
