# Documentation Guide

This page explains how the 5DO documentation set is organized and how to build,
preview, and publish it.

The rendered site is **<https://purelogiccode.github.io/5DO/>**. The source lives in
`docs/` and is built with **MkDocs Material**.

## Prerequisites

- Python 3.12 or newer
- Git
- PowerShell 7 (`pwsh`) for the wiki publish script

## Build and preview locally

From the repository root:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --require-hashes -r requirements-docs.txt
mkdocs serve
```

Open <http://127.0.0.1:8000/>. The development server watches the docs directory and
reloads on changes.

Validate the production build exactly as CI does:

```powershell
mkdocs build --strict
```

`--strict` turns MkDocs warnings into errors. Broken relative links, missing pages, and
invalid anchors must be fixed rather than ignored.

The compatibility browser reads `docs/data/compatibility.json`, which is generated from the
committed compatibility Markdown. Regenerate it after changing
`GameCompatibilityResults.md` or `GameCompatibilityList.md`:

```powershell
python tools/generate-compat-site.py
```

Use `--import-screenshots` when the sweep artifacts contain new final-frame PNGs. The docs
CI job runs `--check` and fails if the committed JSON is stale.

## Publishing

The site deploys automatically on every push to `master` through
`.github/workflows/docs.yml`. Pull requests build the site without deploying it.
GitHub Pages is configured to deploy from GitHub Actions, not from a branch.

The workflow uses `actions/upload-pages-artifact` and `actions/deploy-pages`, so no
generated `site/` directory is committed.

## Wiki

The curated wiki source is versioned in `wiki/`. Publish it with:

```powershell
pwsh -File tools/publish-wiki.ps1
```

The script clones `https://github.com/purelogiccode/5DO.wiki.git`, copies the curated
pages (including `_Sidebar.md` and `_Footer.md`), commits, and pushes. Do not edit the
wiki directly without copying the change back into `wiki/`.

The wiki is intentionally a concise portal. Long-form content belongs in `docs/` so
there is one source of truth.

## Page structure

`docs/index.md` is the landing page. The navigation is defined in `mkdocs.yml` and is
grouped into:

- **Getting started** — installation, user guide, FAQ.
- **Using 5DO** — compatibility, saves, cheats, TAS, configuration, troubleshooting.
- **Reference** — CLI, save-state format, audio, video, Vulkan, diagnostics.
- **Development** — architecture, developer guide, build/test, test strategy, porting,
  reference strategy, differential harness, trimming.
- **Project** — status, roadmap, implementation plan, release checklist, legal, test
  corpus.

When you add a page:

1. Add the Markdown file under `docs/`.
2. Add it to `mkdocs.yml`.
3. Link it from the landing page or a related page.
4. Run `mkdocs build --strict`.

## Writing rules

- Use relative links for files inside `docs/`.
- Use absolute GitHub URLs for repository-root files (`LICENSE`, `CHANGELOG.md`,
  `CONTRIBUTING.md`, and so on).
- Never include BIOS ROMs, game images, or extracted assets.
- Use ISO-8601 dates and record evidence instead of making unsupported claims.
- Keep commands copy-pasteable from the repository root.
- Mark historical measurements and machine-specific evidence clearly.

See the [documentation source guide](DocumentationSource.md) for the full conventions and the
maintenance checklist.
