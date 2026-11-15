# RetroAchievements

5DO includes a managed RetroAchievements client. It identifies the loaded 3DO disc,
loads the achievement and leaderboard definitions for that game, evaluates them against
the emulated console's main RAM, shows unlock/progress notifications, and submits unlocks
and leaderboard scores to [retroachievements.org](https://retroachievements.org).

The feature is optional and disabled by default. It requires a RetroAchievements account
and an internet connection while playing.

---

## 1. Setup

1. Open **Configuration > RetroAchievements...**.
2. Enter your RetroAchievements username and password (or an existing API token).
3. Select **Sign in**. The returned API token is stored in `settings.json` so later
   launches do not need the password.
4. Leave **Enable RetroAchievements** checked and boot a supported 3DO disc.

The same window shows the achievement list for the current disc, the current hardcore
score, the softcore score, the disc hash, and the rich-presence string. The status bar
also shows a compact `RA ...` progress summary.

The settings are also available directly in `settings.json`:

| Key | Meaning |
|---|---|
| `RetroAchievementsEnabled` | Master switch. |
| `RetroAchievementsUsername` | Account name. |
| `RetroAchievementsToken` | API token; treat the settings file as sensitive. |
| `RetroAchievementsHardcore` | Use hardcore unlocks and rules. |
| `RetroAchievementsShowNotifications` | Unlock and game-identification toasts. |
| `RetroAchievementsShowProgressNotifications` | Measured-progress toasts. |
| `RetroAchievementsShowRichPresence` | Show rich presence in the status bar/window. |
| `RetroAchievementsShowLeaderboardNotifications` | Leaderboard start/cancel/submit toasts. |
| `RetroAchievementsHost` | Web API host; leave the default unless you use a compatible server. |

---

## 2. Hardcore mode

> **Approval status.** RetroAchievements must recognize the `5DO/<version>` client before
> hardcore unlocks count. Until then, the server injects the zero-point
> `Warning: Unknown Emulator` achievement and treats hardcore unlocks as casual. The shell
> labels the mode `RA hardcore (pending)`. See
> [RetroAchievementsCompliance.md](RetroAchievementsCompliance.md).

While hardcore is active, 5DO blocks:

- loading save states (including the save-slot manager's load action and quick-menu load),
- rewind,
- slowdown and frame advance,
- cheats and an already-loaded cheat set,
- movie/TAS playback and recording,
- input recording, the movie editor, and the input timeline.

Creating save states, deleting states, fast-forward, and reset remain available. The
save-slot manager still opens in hardcore, but its **Load** button and double-click load
are disabled; **Save Current** and **Delete** work normally. RetroAchievements permits
fast-forward and allows creating states for debugging while forbidding their use during
hardcore play.

Rich presence is always submitted in hardcore even when the local display option is off.
Leaderboards remain active in both modes, but score entries are submitted only from a
hardcore session; in casual they are tracked locally without being posted. Only local
leaderboard toasts can be hidden.

Enabling hardcore while a game is running stops TAS/input tools, resets the emulated
machine, and then starts the new hardcore session from the reset state. Switching
hardcore → casual is allowed without a reset. Reset itself is always allowed. The mode is
global, not per-game.

Casual and hardcore unlocks are tracked separately. An achievement unlocked only in
casual remains active in a hardcore session so it can be earned again for hardcore credit;
a hardcore unlock counts for both modes.

---

## 3. Game identification

The 3DO hash is computed from the first data track's 2048-byte user-data sectors, using
the Opera filesystem volume header and the `LaunchMe` entry. It is the same hash that
`rc_hash` produces for RetroAchievements and it works for `.cue`, `.bin`, `.iso`, and
`.chd` images because 5DO hashes through its normal `IDiscSource` reader.

The implementation was verified against RAHasher 1.8.3 on real CHD images; for example
*Need for Speed (EU/US)* hashes to `1f7f88098b52fd76c5a9fe4d6c3a3171` and resolves to
RetroAchievements game 1008.

A disc with no registered set reports **"No achievements for this disc"**. A hash that
the server does not recognize reports the same message; it does not stop emulation.

### Live validation (2026-11-15)

A live softcore session against *Gex* (game 1009, hash
`f51d8d468bb19f621777fedd3e025365`) loaded 45 official achievements and the 3,515-character
rich-presence script. The managed engine then unlocked **Unfinished Dance** (579967) from a
synthetic RAM frame sequence, and the award endpoint accepted it (softcore score `0 -> 1`).
Login, game data (`r=patch`), session start, rich-presence ping, and a hardcore session
start also succeeded.

Warning achievements (IDs at or above `101000000`) are client notices, not real
achievements. The service excludes them from activation, totals, and unlock imports, as
rcheevos' `rc_client` does.

---

## 4. Memory map

RetroAchievements' 3DO memory map exposes **Main RAM** at `0x000000-0x1FFFFF` as
`SYSTEM_RAM`. 5DO presents the same raw byte array that Opera exposes to RetroArch:
32-bit words in host byte order, with the CPU-visible byte at `address ^ 3` on
little-endian hosts. `MachineAchievementMemory` caps reads at 2 MiB, so an expanded
4/8/14 MB DRAM configuration does not change the addresses achievement authors used.

Achievement definitions are evaluated on the core thread, once per emulated frame.
Fast-forward evaluates each hidden emulated frame; run-ahead's speculative frames are
excluded so the hit counts do not double-count a replayed frame.

---

## 5. What is supported

- Official core achievements, including measured progress (`0/10`).
- `ResetIf`, `PauseIf`, `AddSource`, `SubSource`, `AddAddress`, `Remember`/`recall`,
  `AddHits`/`SubHits`, `AndNext`/`OrNext`, `ResetNextIf`, BCD/inverted operands, floats,
  delta/prior values, and indirect memory reads.
- Leaderboards: start/cancel/submit triggers and score submission.
- Rich presence: lookup tables, formats, conditional displays, ASCII/Unicode macros.
- Session start, existing unlock import, achievement awards with bounded retries,
  periodic rich-presence pings, and score updates.
- A durable offline unlock queue. Pending awards are checksummed, written atomically to
  `retroachievements-pending.json` in the 5DO data directory, and retried every 30
  seconds, after login, and when the matching game session starts. The queue never stores
  the API token and keeps entries for up to 30 days.
- Mode-specific session imports: a hardcore session only treats hardcore unlocks as
  complete, so casual-only achievements can be earned again with hardcore credit.
- Unlock, progress, leaderboard, game-identification, and error notifications on the
  existing OSD queue.
- An achievements window with title, description, points, progress, unlock state,
  pending-sync count, and unsupported-definition markers.

### Current limitations

- Only the core achievement set returned by the compatibility endpoint is used. Bonus,
  specialty, and exclusive subsets are not selected yet.
- Unofficial achievements are listed but not activated or submitted.
- Hardcore credit is pending RetroAchievements client approval. Casual mode is fully
  usable now.
- Offline queueing covers achievement unlocks; leaderboard scores still require a live
  connection when the leaderboard completes and are only submitted from hardcore mode.
- Pending unlocks for a game sync when that game's session is active again. A queue entry
  for a game you do not reopen remains pending until it expires after 30 days.
- The achievement window is a text list; the rich badge/overlay presentation from
  RetroArch is not implemented.
- Leaderboards are evaluated and submitted in hardcore, but there is no dedicated
  leaderboard browser.
- The API token is stored in the plain settings file. Use a dedicated token and protect
  the data directory if other users share the machine.

---

## 6. Troubleshooting

| Symptom | Check |
|---|---|
| The status bar says `RA: signed out` | Open the RetroAchievements window and sign in. |
| `No achievements for this disc` | The hash is valid but the game has no set, or the disc revision is not registered. |
| Login fails | Check the password/token and that the system clock is correct. The server rejects expired tokens. |
| Unlocks stay local | The window shows `N pending sync`; the queue survives restarts and retries every 30 seconds and when the matching game session starts. Check the connection and token; queued awards expire after 30 days. |
| Cheats, save-state loading, rewind, or frame advance stop working | Hardcore mode is active. Switch to casual to use them. Fast-forward and saving states remain available. |
| `Warning: Unknown Emulator` appears | Expected until RetroAchievements approves the `5DO` client for hardcore credit. Casual unlocks still work. |
| Hardcore leaderboard has no score | Hardcore and softcore are separate server scores; switch modes and play again. Hardcore also requires RA client approval. |

---

## 7. Attribution and licensing

The evaluation engine is a C# port of [rcheevos](https://github.com/RetroAchievements/rcheevos)
(MIT). The port keeps the upstream control flow and constants and is covered by the
project's normal notices; the original MIT notice is recorded in
[`THIRD-PARTY-NOTICES.md`](https://github.com/purelogiccode/5DO/blob/master/THIRD-PARTY-NOTICES.md). The 3DO hash implementation is a
port of `rc_hash_3do` from the same MIT source.

RetroAchievements is a separate service with its own terms. 5DO is not affiliated with
or endorsed by RetroAchievements.org. Account registration and API use are subject to
the service's rules.
