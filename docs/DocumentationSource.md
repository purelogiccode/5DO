# Documentation Source Guide

This directory is the source of the 5DO documentation site and the canonical reference
set for the emulator. The rendered site is published at
**<https://purelogiccode.github.io/5DO/>** with MkDocs Material.

The site is built from this directory; `docs/index.md` is the landing page. The
repository root contains the community and legal documents that GitHub surfaces
directly (`README.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`,
`SUPPORT.md`, `LICENSE`, and `THIRD-PARTY-NOTICES.md`).

## Quick start

From the repository root:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements-docs.txt
mkdocs serve
```

Open <http://127.0.0.1:8000/>. To validate the production build:

```powershell
mkdocs build --strict
```

CI runs the same strict build. A warning is treated as a failure, so fix broken
relative links, missing pages, and invalid anchors rather than suppressing them.

## Publishing

The docs site deploys automatically from `master` through
`.github/workflows/docs.yml`. Pull requests build the site without deploying it.

The wiki is versioned in `wiki/` and published with:

```powershell
pwsh -File tools/publish-wiki.ps1
```

The script clones `https://github.com/purelogiccode/5DO.wiki.git`, copies the curated
pages, commits, and pushes. Do not edit the wiki directly without copying the change
back into `wiki/`.

## Writing conventions

- **Commands** are shown from the repository root. PowerShell examples use backticks
  for line continuation; shell examples use `\`.
- **Placeholders** are written as `<path>`, `<disc>`, `<bios>`, `N`, or `0x...`.
- **Hex values** use a `0x` prefix unless a field is explicitly decimal.
- **Implemented** means the code path exists and is covered by at least one test or a
  recorded real-content run. **Validated** means it was exercised on the stated
  hardware/content and the evidence is recorded in this repository.
- **Dates** are ISO-8601 (`YYYY-MM-DD`) and describe when evidence was recorded, not
  when the document was last edited.
- **Paths** use forward slashes in prose and native separators in platform-specific
  commands.
- **Links**: use relative Markdown links for pages and assets inside `docs/`. Use
  absolute GitHub URLs for repository-root files and files outside `docs/`, because
  the site build only sees this directory.
- **No copyrighted content**: never include BIOS ROMs, game images, or extracted
  assets. Hashes and frame counts are facts, not assets.

## Keeping documentation current

When behavior changes:

1. Update the user-visible page (`UserGuide.md`, `Installation.md`, `FAQ.md`,
   `CliReference.md`, `Configuration.md`, or `SaveStates.md`).
2. Update the implementation-facing page (`Architecture.md`, `DeveloperGuide.md`,
   `Audio.md`, or `Video.md`).
3. Update [`CHANGELOG.md`](https://github.com/purelogiccode/5DO/blob/master/CHANGELOG.md)
   for user-visible changes.
4. Update [Status](Status.md), [Roadmap](Roadmap.md), or [Known issues](KnownIssues.md)
   when the release position changes.
5. Record new evidence in [Test corpus](TestCorpus.md) or
   [Compatibility](Compatibility.md) instead of claiming it in prose.
6. Run `mkdocs build --strict` and, for pages that cite numbers, verify the current
   value rather than copying an old one.

The source code is the final authority. If a document and the code disagree, treat the
code as correct, then fix the document in the same change.

## Page catalog

The navigation is defined in [`mkdocs.yml`](https://github.com/purelogiccode/5DO/blob/master/mkdocs.yml).
The main groups are:

- **Getting started** — installation, user guide, FAQ.
- **Using 5DO** — compatibility, saves, cheats, TAS, recording, settings, troubleshooting.
- **Reference** — CLI, configuration, save-state format, audio, video, Vulkan, diagnostics.
- **Development** — architecture, developer guide, build/test, test strategy, porting,
  reference strategy, differential harness, trimming, documentation.
- **Project** — status, roadmap, implementation plan, release checklist, legal, test corpus.

If you add a page, add it to `mkdocs.yml`, link it from a relevant landing page, and
verify the strict build.
