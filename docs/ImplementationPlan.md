# 5DO — Compatibility Completion Plan

**Snapshot:** 2026-11-08
**Objective:** finish with the largest playable 3DO game set — more playable titles than
4DO and Phoenix on the same BIOS, the same unique-title corpus, and the same input
scripts.
**Scope:** compatibility only. Product, performance, research, and platform work do not
block completion unless they stop a title from reaching the finish line.
**History:** this file is the active task list. The former plan, status snapshots, stable
IDs, evidence, completed log, appendices, and reference strategy are in
[ImplementationPlanArchive.md](ImplementationPlanArchive.md).

---

## 1. Finish line

### 1.1 Playable definition

A title is **Playable** when all four are true:

1. It boots from its canonical image with a supported BIOS (auto-selected or a documented
   per-title override).
2. It renders at least one non-black frame.
3. A standard input script advances past the title/attract screen to a distinct
   interactive or content screen — a menu, options screen, character select, gameplay, or
   FMV/content screen.
4. It does not hang or crash before that screen is captured.

Not required: reaching deep gameplay, finishing a level, completing the game, or matching
another emulator's timing. A loop that never leaves the title/attract screen is **not**
Playable. A title with no separate title/attract screen is Playable when its first
rendered interactive or content screen accepts input. The standard input script may be
the repository's generic 10-minute script or a minimal title/device-specific boot script;
it does not need to reach gameplay. Compatibility claims are measured on the **378 unique
games**; the 1,371 images remain loadability and duplicate coverage, not separate
playability claims.

### 1.2 Completion checklist

The plan is complete only when every item is true:

- [x] **All 378 games classified.** Every title in the unique game test set has a dated
      beyond-title result, screenshot, BIOS name, disc hash, and frame count.
- [x] **0 `Regression` rows.** No title that 4DO or Phoenix can play fails on 5DO.
- [x] **0 `Behind` rows.** Every peer-playable title is also 5DO-playable. A peer row may
      be removed only when its claim is disproved with evidence.
- [x] **Strict superset.** 5DO's playable set contains every 4DO-playable and
      Phoenix-playable title, plus at least one title neither peer plays.
- [x] **Higher count.** 5DO's **375** playable titles exceed 4DO's **167** and Phoenix's
      **233** on the shared corpus (after the one evidenced peer invalidation).
- [x] **Numeric target.** **375 of 378** games are playable; the original target was
      **≥254**. The peer union is **252** after the *Return Fire – Maps O' Death* data
      disc was disproved with a beyond-title capture.
- [x] **Every failure explained.** The two remaining `Title-only` rows and one `No video`
      row are non-retail or add-on images with root causes and captures in
      [`BeyondTitleEvidence.md`](BeyondTitleEvidence.md).
- [x] **Regression gate green.** The full-corpus beyond-title suite runs nightly or
      weekly against a committed baseline.
- [x] **Release gate green.** Build, tests, format, InspectCode, docs check, bundles, and
      checksums pass.

### 1.3 Current baseline (2026-11-08)

| Item | Value |
|---|---:|
| Unique games in the test set | 378 |
| 4DO playable titles (after peer invalidation) | 167 |
| Phoenix playable titles (after peer invalidation) | 233 |
| Peer union (after peer invalidation) | 252 |
| 5DO confirmed playable | **375** |
| 5DO title-only | 2 |
| 5DO no-video | 1 |
| Target | **≥254 (met)** |

The raw peer lists claim 168 4DO / 234 Phoenix (union 253). The standalone
*Return Fire – Maps O' Death* data disc is recorded as `peerInvalid4do` and
`peerInvalidPhoenix` with a beyond-title capture: the only rendered screen is a static
box-art/title card, and the peer claim covers the expansion content used with the base
game, not the standalone data image. The comparison counts above use the invalidated
peer set. See [`BeyondTitleEvidence.md`](BeyondTitleEvidence.md).

The per-title state is the `Unique titles` table in
[CompatibilityComparison.md](CompatibilityComparison.md). Its `5DO`, `vs 4DO`, and
`vs Phoenix` columns are the authoritative per-title matrix.

The remaining non-playable rows are three non-retail/add-on images: `Joypad Test v1.0`
is a static pad status card, `LameNES Emulator` renders only a minimal "HELLO, WORLD!"
text line (0.43% non-black), and the *Return Fire – Maps O' Death* standalone data disc
is a static box-art/title card. All are documented in the evidence page.

---

## 2. Task list

| Phase | Tasks | Exit condition |
|---|---|---|
| **0 — Measure** | COMPAT-01 … COMPAT-06 | 378/378 classified; every failure triaged |
| **1 — Fix** | COMPAT-07 … COMPAT-13 | 0 `Behind`/`Regression`; ≥254 playable; strict superset |
| **2 — Lock and ship** | COMPAT-14 … COMPAT-16, REL-01 … REL-03 | regression gate green; six bundles published |

All compatibility tasks are complete; the remaining open items are the platform validation
and bundle publication in Phase 2. A task is complete only when its **Done when** line is
satisfied and the evidence is committed.

### Phase 0 — Measure the whole corpus

- [x] **COMPAT-01 — Freeze the corpus and peer baseline**
  Refresh the image inventory, collapse duplicates, and rebuild the canonical 378-game
  test set. Re-parse the saved 4DO and Phoenix compatibility lists, compute both playable
  sets and their union, and commit the target counts.
  **Done when:** `artifacts/corpus/inventory.json`, `docs/data/unique-corpus.csv`,
  `docs/data/compatibility-comparison.json`, and the comparison page are current; every
  game has a canonical disc; 0 titles are untested.
  **Evidence:** `CompatibilityComparison.md`; `ImplementationPlanArchive.md` §1.

- [x] **COMPAT-02 — Align the comparison with the playable rule**
  Update `tools/generate-compat-comparison.py` and the committed JSON so peer verdicts
  use a binary `playable` state: 5DO `Good` and `Playable` both count as playable, and
  4DO `Good`/`Playable` and Phoenix `Good` are peer-playable. Keep the quality grade as
  information, but do not mark a reached beyond-title screen as `Behind` merely because a
  peer says `Good`. A `Rendered`/untested title stays `Unverified` and is excluded from
  the playable count; it becomes `Behind` only after a beyond-title test fails.
  **Done when:** a 5DO title that reaches beyond-title is `Parity` regardless of whether
  its quality label is `Good` or `Playable`; `Behind` means only that 5DO failed the
  playable rule; the matrix and target counts are regenerated.

- [x] **COMPAT-03 — Build the beyond-title runner**
  Extend `tools/playability-sweep.py` and the CLI run path with one reproducible pass:
  auto BIOS, per-title overrides, standard input script, captures at boot/title/beyond,
  timeout and retry, and JSON fields for status, screenshot, BIOS, disc hash, frame, and
  failure reason.
  **Done when:** one command classifies a title as `Playable`, `Title-only`, `No video`,
  or `Not loadable`; known passes (Road Rash, Panzer General, Quarterback Attack) and a
  known title-only case reproduce; results are deterministic across two runs.
  **Implementation:** `tools/beyond-title-sweep.py` with an input/control checkpoint
  pair; see `CompatibilitySweep.md` §4.

- [x] **COMPAT-04 — Re-grade the 107 reviewed titles**
  Apply the new playable rule to existing captures. Promote titles that already reach a
  distinct screen beyond the title; keep title-only rows failing. Re-run only where the
  existing evidence is missing.
  **Done when:** all 107 reviewed titles have the new status and a linked capture.

- [x] **COMPAT-05 — Run the remaining 271 titles**
  Run every title currently marked `Rendered` (269) or `No video` (2) through the
  beyond-title runner. Use per-title BIOS, input device, fix mode, and disc where the
  database requires them.
  **Done when:** **378/378** games have a dated beyond-title result and screenshot; 0
  untested; the matrix and database are updated.

- [x] **COMPAT-06 — Triage every failure**
  Put every non-playable title into exactly one bucket: BIOS/region, disc/multi-disc,
  input device, core/CD boot, per-title game fix, or harness/script gap. Record the
  reproduction command and the failing frame/PC/hash.
  **Done when:** every failure has a root cause, a linked fix task, and a stored
  reproduction. The two current `No video` rows are homebrew images; fix or document
  them as non-retail exceptions.

### Phase 1 — Close every gap

- [x] **COMPAT-07 — BIOS and region coverage (SYS-03)**
  Verify auto-selection and per-title overrides for NTSC, PAL1/PAL2, Japan, Kanji,
  Anvil, Arcade, and RSA-disabled images. No user should need a manual BIOS step for a
  tested title.
  **Done when:** every region-sensitive title reaches beyond-title with the documented
  BIOS; no failure is caused by BIOS selection; overrides are in the game database.

- [x] **COMPAT-08 — Disc and multi-disc coverage (SYS-01/02)**
  Verify ISO, BIN, CUE+BIN, CHD, multi-track, CD-DA, M3U/M3U8, parent/child CHD, and
  multi-disc titles that need them. Confirm the canonical image for every test-set title
  opens and boots.
  **Done when:** every format-sensitive title reaches beyond-title; no format-specific
  failure remains; multi-disc and CD-DA cases have a recorded swap/playback check.

- [x] **COMPAT-09 — Input-dependent boots (SYS-15)**
  Verify titles that need joypad, mouse, lightgun, arcade lightgun, flightstick, or
  trackball. Add a minimal boot script for each device-dependent title; a deep gameplay
  script is not required.
  **Done when:** every title requiring a non-default device reaches beyond-title with
  that device selected.

- [x] **COMPAT-10 — Core boot failures**
  Fix every remaining black screen, hang, invalid PC, no-video, or not-loadable retail
  title that a peer can play. One root cause per change, with a focused regression test.
  **Done when:** 0 retail titles fail to reach beyond-title while a peer claims them
  playable; each fix has a test or a dated matrix note.

- [x] **COMPAT-11 — Per-title game fixes**
  Apply the 4DO/Opera known fixes that are still relevant and add 5DO-specific fixes for
  the remaining failures. No speculative fixes: every change starts from a failing
  matrix row.
  **Done when:** every remaining peer-gap title is either fixed or has evidence that the
  peer claim does not meet the playable definition.

- [x] **COMPAT-12 — Clear the peer-gap ledger (CORE-13)**
  Re-evaluate every `Behind` row after COMPAT-02/04/05, then fix or disprove each one.
  The current ledger under the old quality-rank model is **22 vs 4DO / 25 vs Phoenix**;
  the target is **0**. A peer claim may be disproved only with a beyond-title capture and
  an explicit `peerInvalid`/`peerEvidence` record in the comparison data; never drop a row
  silently.
  **Done when:** the comparison has 0 `Behind` and 0 `Regression` rows, and every title
  a peer plays also reaches beyond-title on 5DO.

- [x] **COMPAT-13 — Validate the wins and publish the final matrix**
  Re-run the full corpus after the fixes. Validate every `5DO ahead` title and every
  peer-unplayable title with a beyond-title capture. Recompute the peer union and the
  final count; confirm the strict-superset and ≥254 targets.
  **Done when:** the final comparison is committed, the target table is green, and every
  claimed win has evidence.

### Phase 2 — Lock the result and ship

- [x] **COMPAT-14 — Full-corpus regression gate (TEST-06 / CORE-11/16)**
  Add the 378-title beyond-title pass to the nightly or weekly run. Commit a baseline
  status for every title and fail the run when a title changes from Playable to failing.
  Allow only dated, justified exceptions.
  **Done when:** the gate runs unattended, the baseline is green, and a status regression
  fails the build.

- [x] **COMPAT-15 — Accuracy smoke (TEST-07)**
  Keep the 50-title frame/audio panel green. Investigate a divergence only if it blocks
  beyond-title or causes a player-visible regression in a passing title.
  **Done when:** the panel has 0 errors and no unexplained frame/audio divergence that
  affects a playable title.

- [x] **COMPAT-16 — Conditional CD and timing blockers (CORE-04 / CORE-07)**
  Fix the CD command/cadence residual and the related timer/read path only when they
  prevent a title from reaching beyond-title or change a status. Otherwise keep the
  measured parity evidence and document the residual.
  **Done when:** no compatibility blocker remains open under this item.

- [x] **REL-01 — Release build gate (ENG-01/09)**
  Run `dotnet build 5DO.sln -c Debug`, the full test suite,
  `dotnet format --verify-no-changes`, and `jb inspectcode --severity=WARNING`. Run the
  docs JSON/comparison `--check`.
  **Done when:** 0 warnings, 0 errors, 0 format changes, 0 InspectCode results, and the
  comparison outputs are current.
  **2026-11-08 (1.6.0):** Debug build 0 warnings/errors, **2,061/2,061 tests**, format
  clean, InspectCode 0 results, `verify-beyond-title-ledger.py` and
  `generate-compat-comparison.py --check` pass.

- [x] **REL-02 — Platform validation (ECO-04)**
  Validate Windows x64/arm64 bundle smoke and Linux x64 on the local VM. Record macOS
  build validation and GPU presentation as best-effort.
  **Done when:** the release gate is green on Windows and Linux; macOS is documented.
  **2026-11-08 (1.6.0):** Windows x64 bundle smoke passed (CLI 1.6.0 `selftest` + frontend
  `--smoke-test`). Local PE/ELF/Mach-O inspection confirms all six bundle apphost
  architectures. Release run
  <https://github.com/purelogiccode/5DO/actions/runs/42552318341> passed the Linux x64
  CLI/frontend smoke under xvfb and all six bundle jobs; CI run
  <https://github.com/purelogiccode/5DO/actions/runs/42552303788> passed Windows, Linux,
  and macOS build/test. The local Linux VM rerun remains blocked by host memory; macOS
  remains best-effort.

- [x] **REL-03 — Bundles, checksums, and release notes**
  Publish the six platform/architecture bundles with `README.md`, `LICENSE`,
  `THIRD-PARTY-NOTICES.md`, `whatsnew.md`, and the release/legal docs. Publish
  `SHA256SUMS.txt`; keep a local copy in
  `src/FiveDo.Frontend.Avalonia/bin/Release`; remove PDB/XML files.
  **Done when:** all six bundles and checksums are published and verified, and
  `CompatibilityComparison.md`, `Status.md`, `KnownIssues.md`, `README.md`, and the
  release notes reflect the final result.
  **2026-11-08 (1.6.0):** six local bundles built in
  `src/FiveDo.Frontend.Avalonia/bin/Release`; all `SHA256SUMS-1.6.0.txt` entries verify;
  no runtime/PDB/XML files. Tag `v1.6.0` published six bundles and
  `SHA256SUMS.txt`; all published checksums verify:
  <https://github.com/purelogiccode/5DO/releases/tag/v1.6.0>.

---

## 3. Execution order

1. **Measure:** COMPAT-01 → COMPAT-02 → COMPAT-03 → COMPAT-04 → COMPAT-05 → COMPAT-06.
2. **Fix:** COMPAT-07 through COMPAT-13. Work the failure ledger by root-cause bucket;
   do not start with title-specific patches.
3. **Lock and ship:** COMPAT-14 → COMPAT-15 → COMPAT-16 → REL-01 → REL-02 → REL-03.

Rules:

- **One title, one outcome.** Every game in the 378-title matrix must end as Playable,
  not-playable with a root cause, or peer-claim-disproved.
- **Evidence before checkbox.** Attach the BIOS, disc hash, frame count, capture, and
  command to every claim.
- **No speculative fixes.** Every fix starts from a failing matrix row.
- **No deep-playthrough requirement.** Reaching a distinct screen beyond the title is
  enough for this plan.

---

## 4. Verification commands

```powershell
# Build, test, format, static analysis
dotnet build 5DO.sln -c Debug
dotnet test 5DO.sln -c Debug --no-build --no-restore
dotnet format 5DO.sln --verify-no-changes --no-restore
jb inspectcode 5DO.sln --severity=WARNING --no-updates

# Unique corpus and 3-way comparison
python tools/corpus-inventory.py --jobs 8 --out artifacts/corpus/inventory.json
python tools/generate-compat-comparison.py --inventory artifacts/corpus/inventory.json --sweep artifacts/compat-full/summary-merged.csv --sweep artifacts/compat-unique/summary.csv
python tools/generate-compat-comparison.py --check --inventory artifacts/corpus/inventory.json --sweep artifacts/compat-full/summary-merged.csv --sweep artifacts/compat-unique/summary.csv

# Beyond-title playability pass
python tools/playability-sweep.py --manifest benchmarks/diff/tierb-panel-4.json --out artifacts/tierb-panel-4 --jobs 4
python tools/beyond-title-sweep.py --scope peer-gap --jobs 10 --out artifacts/beyond-title --ledger docs/data/beyond-title-results.csv
python tools/beyond-title-report.py --ledger docs/data/beyond-title-results.csv
python tools/verify-beyond-title-ledger.py --ledger docs/data/beyond-title-results.csv
python tools/beyond-title-baseline.py --ledger docs/data/beyond-title-results.csv --check --require-count 378

# Accuracy smoke (frame/audio panel)
dotnet run --project tools/FiveDo.DiffHarness -c Release -- run benchmarks/diff/panel50.json --core artifacts/diff/opera-build/out/OperaCore.dll

# Frontend smoke
dotnet run --project src/FiveDo.Frontend.Avalonia -c Release -- --smoke-test
```

The 2026-11-08 panel matches **150/150 frame hashes** and **150/150 audio fields**.
The harness's PC/CD-command gate still reports the documented CORE-04 cadence residual;
it does not change a frame hash or a beyond-title result.

The former plan's full command reference, including GPU probes and release packaging, is
preserved in [ImplementationPlanArchive.md](ImplementationPlanArchive.md#7-verification-commands).

---

## 5. Out of scope

These do not block compatibility completion and must not be added as prerequisites:

- Deep gameplay verification, ending validation, or full-game completion.
- Frame-perfect parity with 4DO/Phoenix after the beyond-title screen.
- Performance work, managed JIT, threaded DSP, or internal 4K CEL research.
- Product/UX features, netplay, mobile/browser targets, Raspberry Pi, or Steam Deck.
- Six-player/multi-tap, lightgun crosshair polish, or presentation-only filters.
- Platform work beyond the Windows/Linux release gate; macOS remains best-effort.

---

## 6. History

The former plan and its completed log are archived in
[ImplementationPlanArchive.md](ImplementationPlanArchive.md). It preserves the
2026-11-08 status snapshot, the old A-D gates, stable IDs (`TEST-06`, `CORE-04`,
`CORE-13`, `SYS-*`, `ECO-*`), the shipped baseline, evidence gaps, reference strategy,
appendices, and every completed entry. Completed work must not be re-opened here.
