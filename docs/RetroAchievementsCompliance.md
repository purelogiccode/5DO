# RetroAchievements hardcore compliance package

This page is the application checklist and evidence package for asking
RetroAchievements to recognize **5DO** as a hardcore-compliant client. It is written
against the published [Hardcore Compliance Requirements](https://docs.retroachievements.org/general/hardcore-compliance-requirements.html).

> **Current status: not yet submitted.** `RetroAchievementsCompliance.HardcoreApproved` is
> `false`, so the UI labels hardcore as pending. RetroAchievements injects the
> zero-point `Warning: Unknown Emulator` achievement until the client is approved and
> currently treats hardcore unlocks as casual. Casual mode works normally.

---

## 1. Client identity

| Item | Value |
|---|---|
| Emulator name | 5DO |
| Stable User-Agent | `5DO/1.11.0` |
| Source repository | <https://github.com/purelogiccode/5DO> |
| Release page | <https://github.com/purelogiccode/5DO/releases> |
| Supported system | 3DO Interactive Multiplayer (RA console 43) |
| Lineage | FreeDO/4DO-derived C# emulator; no native emulator DLL or P/Invoke into FreeDOCore |
| License | FreeDO modified GNU LGPL; non-commercial donationware. See [Licensing.md](Licensing.md). |
| Third-party notices | [THIRD-PARTY-NOTICES.md](https://github.com/purelogiccode/5DO/blob/master/THIRD-PARTY-NOTICES.md) |
| Privacy policy | [Privacy.md](Privacy.md) |
| RA integration | In-tree MIT rcheevos port (`FiveDo.Achievements`) plus a managed Web API client |

Rules that apply to the application:

- The User-Agent has a unique, stable first segment and an incrementing numeric version.
- 5DO does not identify itself as RetroArch, RALibRetro, or another approved client.
- The emulator has not previously shipped another client identity.
- The project is non-commercial. There are no paid features or in-app purchases.

---

## 2. Feature evidence

| Requirement | 5DO implementation | Evidence |
|---|---|---|
| Achievement list in the UI | **Configuration > RetroAchievements...** lists title, description, points, measured progress, unlock state, unsupported state, and rarity | `RetroAchievementsWindow.axaml` |
| Triggers evaluate correctly | Managed rcheevos condition/trigger/value/leaderboard/rich-presence port | `FiveDo.Achievements/Rcheevos`, focused engine vectors |
| Measured and trigger flags | Measured values are evaluated and shown as `n/m`; Trigger conditions, pause/reset, hit targets, and primed state use the upstream state machine | `RcTrigger`, `RcCondSet`, `AchievementProgress.ProgressText` |
| Rich Presence | Served from the game script every 30 frames and sent by the periodic ping even when local display is off | `RetroAchievementsService.Tick`, `PingAsync` |
| Leaderboards | Start/cancel/submit triggers, live values, and score submission | `RcLeaderboard`, `SubmitLeaderboardAsync` |
| Offline unlock queue | Durable, checksummed, atomically written queue; retries every 30 seconds, after login, and when the matching game session starts | `PendingUnlockQueue`, `RunUnlockQueueAsync` |
| Toolkit support | RAIntegration is not shipped; this is optional for compliance but produces the standard toolkit warning | Documented below |
| Save-state hit storage | Hit counts are runtime-only and reset after a state load; this is recommended, not required | `ResetRuntime` |
| Standard save files | NVRAM and memory-card data use the emulator's normal persisted formats; save states are separate | Storage docs |

### Offline queue details

- File: `retroachievements-pending.json` in the 5DO data directory.
- Stores achievement ID, game ID, disc hash, username, hardcore flag, unlock time, title,
  and points. **It never stores the API token.**
- Atomic temp-file replacement; SHA-256 checksum rejects corrupted payloads; invalid files
  are quarantined as `.bad`.
- Entries older than 30 days or beyond 1,000 records are pruned. The API token is required
  to flush; the queue survives logout and application restarts.
- A queued unlock is removed when the server accepts it or reports that the user already
  has it. Session start also removes queued entries that the server already reports as
  unlocked.

---

## 3. Hardcore rules enforcement

The desktop emulator is the only RA-enabled client. The CLI's debug, watchpoint, TAS, and
scripting tools do not load RetroAchievements, so they cannot earn achievements.

| Rule | Enforcement |
|---|---|
| Cheats disabled | Cheat application and the cheats window are blocked; an already-loaded set stops being applied |
| Loading save states always blocked | Quick-menu load and the save-slot manager load action are blocked. Creating states, deleting states, and the save-slot manager remain available for debugging, as RA permits. |
| Rewind disabled | Menu, hotkey, and quick-menu rewind are blocked |
| Slow motion disabled | 5DO has no slow-motion mode |
| Fast-forward allowed | Menu, hotkey, and quick-menu fast-forward remain available, matching the RA FAQ exception for speed control |
| Frame advance disabled | `FrameAdvance` hotkey is blocked |
| TAS / recorded-input playback prohibited | Movie playback, movie recording, input recording, movie editor, and input timeline are blocked |
| Scripting prohibited | Desktop RA path does not load Lua/input scripts; CLI scripting is outside the RA client |
| Memory editors/debuggers prohibited | No desktop memory editor, watchpoint, or debugger is shipped; CLI tools do not run RA |
| Casual → hardcore switch resets the game | `EnterRetroAchievementsHardcore` stops TAS/input tools and resets the machine and runtime |
| Rich Presence cannot be disabled in hardcore | Ping always sends rich presence when hardcore is active, even if the display option is off |
| Leaderboards cannot be disabled in hardcore | Leaderboards stay active; only local toasts can be hidden |
| Hardcore visibly indicated | Status bar shows `RA hardcore (pending)` until approval; game identification OSD says hardcore is active |
| Mode-specific unlocks | Casual-only unlocks stay active in a hardcore session; hardcore unlocks count for both modes |
| Unique stable user agent | `5DO/1.11.0`, one source constant in `RetroAchievementsCompliance` |

### Mode switch behavior

Enabling hardcore while a game is running:

1. Stops input recording and movie/TAS playback.
2. Resets the emulated machine and RA hit counts.
3. Restarts the RA session with the hardcore flag.

Switching hardcore → casual is allowed without a reset.

---

## 4. Validation evidence

| Check | Result |
|---|---|
| Build | 0 warnings, 0 errors |
| Full .NET suite | 2,344 tests, including rcheevos vectors, 3DO hashing, API parsing, warning filtering, and queue round-trips |
| 3DO hash parity | Byte-identical to RAHasher 1.8.3 on real CHDs |
| Live login | Passed |
| Live game data (`r=patch`) | Passed |
| Live softcore session / ping | Passed |
| Live casual award | Passed: Gex 1009, achievement 579967, score 0 → 1 |
| Live hardcore session start | Passed at the API level; hardcore credit still requires RA approval |
| Leaderboard submission | Request shape/signature verified against rcheevos; no tested 3DO set exposed a leaderboard |

---

## 5. Application checklist

- [x] Implement achievements, measured progress, rich presence, leaderboards, and OSD.
- [x] Use a unique, stable user agent in the required format.
- [x] Enforce the hardcore rule set listed above.
- [x] Reset the game on casual → hardcore switches.
- [x] Keep rich presence and leaderboards enabled in hardcore.
- [x] Implement a durable offline unlock queue.
- [x] Publish license, third-party notices, and privacy policy.
- [x] Validate login, patch data, session, ping, and a real softcore award.
- [ ] Six months of public availability (RA eligibility window).
- [ ] Send the package to RetroAchievements staff and complete their review.
- [ ] After approval, make `RetroAchievementsCompliance.IsHardcoreApproved()` return `true`.

Useful links for the submission:

- [Hardcore Compliance Requirements](https://docs.retroachievements.org/general/hardcore-compliance-requirements.html)
- [Emulator Support](https://docs.retroachievements.org/general/emulator-support-and-issues.html)
- [RetroAchievements Discord](https://discord.gg/dq2E4hE)
- Contact page: <https://retroachievements.org/contact>

Suggested message:

> 5DO is an open-source, non-commercial 3DO emulator written in C#. We have added a
> managed rcheevos-compatible client, a stable `5DO/<version>` User-Agent, 3DO hashing
> matching RAHasher, hardcore rule enforcement (no save-state load, rewind, frame advance,
> cheats, TAS/movie playback, input recording, or scripting; fast-forward and creating
> states remain available), a reset on casual-to-hardcore switches, always-on rich
> presence/leaderboards, and a durable offline unlock queue. We would like 5DO added to
> the recognized-client list for hardcore credit.
> Repository: https://github.com/purelogiccode/5DO

---

## 6. Optional / follow-up items

These are not hard blockers but improve the package:

- Ship RAIntegration on Windows and note it on the Downloads page.
- Persist RA hit counts in 5DO save states.
- Add a durable offline queue for leaderboard submissions.
- Add badge images and a dedicated leaderboard browser.
- Record a short video showing hardcore blocking each prohibited feature.
- Add a test that opens a movie, enables hardcore, and asserts the reset and playback stop.

## 7. After approval

1. Make `RetroAchievementsCompliance.IsHardcoreApproved()` return `true`.
2. Remove the "pending" wording from the status bar and RA window note.
3. Run a live hardcore award on a throwaway achievement and confirm it appears in the
   hardcore score.
4. Update this page, `RetroAchievements.md`, `KnownIssues.md`, and the changelog.
