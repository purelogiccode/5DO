# Online Cover Art

5DO can resolve cover art from a local folder and from an optional online catalog.
The online service is hosted separately from the application bundles on Cloudflare R2
and is entirely optional. Local covers keep working when the network is unavailable.

**Current pack:** `2026.11.07` - **473 unique covers** - **645 / 653** 4DO game IDs
mapped - **25.4 MiB** of WebP images - **26.7 MiB** bulk ZIP.

## 1. Source collection

The source collection in `References/Screenshots` contains 1,356 PNG files (about
543 MB). Many files are byte-identical duplicates saved under Redump and No-Intro
names. The packager collapses them by SHA-256:

| Metric | Value |
|---|---|
| Source files | 1,356 |
| Unique images | 473 |
| Unique source bytes | 543 MiB |
| Encoded WebP q85 | 25.4 MiB |
| Bulk ZIP | 26.7 MiB |
| Mapped 4DO game IDs | 645 / 653 |
| Unmapped game IDs | 8 (homebrew/emulators with no cover) |
| Weak mappings reviewed | 5 |
| Manual overrides | 9 |

All 1,356 original names are preserved as catalog aliases, so a user file named with
either the Redump or No-Intro convention resolves to the same image.

## 2. R2 layout

The catalog base URL is:

```text
https://assets.purelogiccode.com/5do/v1
```

The bucket is public-read and uses a Cloudflare custom domain. No Worker is required.

```text
/v1/catalog.json                 application/json, max-age=300
/v1/images/<sha256>.webp         image/webp, max-age=31536000, immutable
/v1/packs/covers-v1.zip          application/zip, max-age=31536000, immutable
/v1/SHA256SUMS.txt               text/plain, max-age=300
/v1/report.json                  maintainer report, max-age=300
```

Image names are content hashes. A new pack must keep old hashed images available so
clients with a stale catalog keep working. Only add new objects; do not delete the
previous pack's images immediately.

`catalog.json` contains a `packSha256` for the bulk ZIP. Each image is verified against
its SHA-256 before it is moved into the cache.

## 3. Catalog schema

```json
{
  "schemaVersion": 1,
  "packVersion": "2026.11.07",
  "generatedUtc": "2026-11-07T11:52:14Z",
  "noticeUrl": "https://github.com/purelogiccode/5DO",
  "imagesBase": "images/",
  "packFile": "packs/covers-v1.zip",
  "packSize": 28044790,
  "packSha256": "fa85d7fc7bacab32cffb5e9f54fb243d0c1c70810d812131c9538c5f228cd215",
  "covers": [
    {
      "id": "7257a1b42aa0f0a4d7453c34e193db6fe4df1eaf7d53b0a2ff6bbcfdbfbbdd93",
      "file": "7257a1b42aa0f0a4d7453c34e193db6fe4df1eaf7d53b0a2ff6bbcfdbfbbdd93.webp",
      "name": "20th Century Video Almanac",
      "aliases": [
        "20th Century Video Almanac (1993)(Software Toolworks)(US)[!]",
        "20th Century Video Almanac (USA)"
      ],
      "gameIds": ["3F72700D"],
      "width": 288,
      "height": 600,
      "size": 59382,
      "regions": ["US"],
      "year": "1993",
      "rank": 0
    }
  ]
}
```

- `id` is the SHA-256 of the encoded WebP bytes.
- `file` must be a bare file name whose stem equals `id`; directory traversal is rejected.
- `gameIds` are precomputed 4DO IDs. A known disc uses an exact lookup.
- `rank` is `0` retail, `1` demo/sample, `2` beta/proto, `3` unknown.
- `regions` and `year` are used only for tie-breaking.
- `imagesBase` and `packFile` are resolved relative to the catalog URL. `file://`
  catalogs are supported for offline use and tests.

## 4. Client cache

The client stores the catalog and images under the shared base data directory:

```text
<BaseData>/covers/catalog.json
<BaseData>/covers/catalog.meta.json
<BaseData>/covers/images/<sha256>.webp
<BaseData>/covers/tmp/
<BaseData>/covers/packs/covers-v1.zip
```

The cache is shared across named profiles. Portable mode keeps it beside the
executable. The catalog is revalidated with `If-None-Match`; a cached catalog is used
when the server is unavailable.

## 5. Matching

Resolution order:

1. Sibling image beside the disc (existing behavior).
2. Exact base name in `CoverDirectory` (existing behavior).
3. Annotation-stripped exact match in `CoverDirectory`.
4. Catalog exact **game ID**.
5. Catalog exact normalized/token/no-space/subtitle match.
6. Local fuzzy match.
7. Catalog fuzzy match.

The matcher:

- strips balanced `()`, `[]`, and `{}` groups;
- normalizes case, punctuation, `&` → `and`, apostrophes, and articles;
- compares exact normalized names, sorted token sets, and no-space forms;
- expands `A - B - C` into `B - C` and `C` aliases;
- applies Jaro-Winkler with a default threshold of **0.85** (range 0.70–0.95);
- rejects sequel-number conflicts such as `Battle Chess` vs `Battle Chess II`;
- ignores version numbers on beta/demo/version-tagged names;
- tie-breaks by region, release year, retail-over-demo rank, and name length;
- returns no cover rather than a low-confidence wrong cover.

The default threshold is intentionally higher than Simple Launcher's 0.80 because 5DO
has a precomputed game-ID map and should prefer a missing cover over a wrong one.

## 6. Settings and CLI

**Settings > Covers** exposes:

- use online covers;
- download missing covers automatically;
- catalog URL (base or full `catalog.json`);
- fuzzy matching and annotation stripping;
- fuzzy threshold;
- refresh catalog, download all covers, clear cache;
- cache statistics and the legal notice.

CLI:

```powershell
5do covers update
5do covers update --catalog artifacts/covers/v1/catalog.json --cache .\covers
5do covers match "Diving Spot Guide - Marine Tour"
5do covers download --folder "D:\Games\3DO"
5do covers download --all
5do covers verify --fix
5do covers cache path
5do covers cache clear
```

`--catalog` accepts a local `catalog.json`; relative image paths resolve beside it.
This is useful for offline testing and for users who downloaded the bulk pack.

## 7. Building and publishing

Build the pack:

```powershell
packaging/package-covers.ps1
# or:
dotnet run --project tools/FiveDo.CoverPackager -c Release -- `
  --source References/Screenshots `
  --output artifacts/covers/v1 `
  --pack-version 2026.11.07 `
  --overrides packaging/cover-overrides.json
```

The packager:

1. hashes and deduplicates source files by SHA-256;
2. encodes WebP q85 at up to 600 px height with SkiaSharp;
3. parses file names with `GameDatabaseNameParser`;
4. matches game IDs with `CoverCatalogIndex`;
5. applies `packaging/cover-overrides.json`;
6. writes `catalog.json`, `images/`, `packs/covers-v1.zip`, `SHA256SUMS.txt`, and
   `report.json`;
7. reloads the generated catalog, verifies every image hash, and checks that no game ID
   is mapped twice.

Review `report.json` before publishing. `unmappedGames` lists low-confidence and
missing games; `weakMappings` are scores between the map threshold and 0.95. Add manual
fixes to `packaging/cover-overrides.json`:

```json
{
  "version": 1,
  "gameIds": {
    "1AAE20E9": "Chiki Chiki Machine Mou Race - Kenken to Black Maou no Ijiwaru Daisakusen"
  }
}
```

The value may be a unique cover display name, a unique alias, or the 64-character
SHA-256 id. Unknown or ambiguous references fail the build.

Publish with rclone:

```powershell
packaging/publish-covers.ps1
# or preview:
packaging/publish-covers.ps1 -DryRun
```

The script expects an rclone R2 remote named `r2` and a bucket path such as
`r2:simplelauncher-thumbnails/5do/v1`. It sets the immutable cache header on images and
the short cache header on the catalog. The Cloudflare custom domain serves the bucket
root, so the public catalog URL is
`https://assets.purelogiccode.com/5do/v1/catalog.json`.

## 8. Tests

- `tests/FiveDo.Tests/Host/CoverTests.cs` covers annotation stripping, normalization,
  Jaro-Winkler, token/no-space/subtitle matching, sequel-number guards, catalog
  validation, cache hash verification, local resolution, remote catalog/image
  downloads, and settings normalization.
- `covers verify` checks every cached image against the catalog.
- The frontend smoke test and a manual library scan validate the UI integration.

## 9. Rights and takedown

Cover art is copyrighted by the respective publishers. The online pack is provided for
personal, non-commercial use and is not included in the 5DO application bundles. Rights
holders can request removal through the project issue tracker:

<https://github.com/purelogiccode/5DO/issues>

Do not upload the pack to a public bucket without a working takedown contact.
