# Library Metadata and Media

**Snapshot:** 2026-11-13. UX-28 adds an external metadata lookup with a deterministic
cache and manual media attachments.

5DO already applies the embedded 4DO game database, file-name facts, and the optional
offline `library-metadata.json`. The external provider does not replace those sources:
it fills the same offline document, which keeps the library deterministic and works
without a network connection after the first lookup.

## 1. Catalog format

The external catalog and `library-metadata.json` use the same schema:

```json
{
  "Version": 1,
  "Records": [
    {
      "GameId": "TETU0001",
      "Path": "",
      "Title": "Battle Chess",
      "Developer": "Interplay",
      "Publisher": "Interplay",
      "ReleaseYear": "1993",
      "Genre": "Strategy",
      "Regions": "US",
      "Players": 2,
      "Description": "Optional description.",
      "Media": ["C:\\media\\manual.pdf"]
    }
  ]
}
```

Records are matched by `GameId` first, then by full `Path`. Empty fields keep the value
from the database or file name. `Players` only overrides when it is greater than zero.
The metadata is presentation-only; it never changes the emulated machine.

`Media` is a list of absolute paths the user attached manually. The emulator stores the
paths and never copies, rewrites, or deletes the referenced files. A missing media file
is ignored at display time.

## 2. Lookup and cache

`5do metadata lookup` downloads a catalog from an HTTP(S) or `file:` URL, validates the
version and size (8 MB maximum), writes a canonical copy to
`<data>/metadata-cache.json`, and searches it. The cache is written atomically through
the same `AtomicFile` path as settings. Re-running the same lookup with the same
catalog produces the same cache bytes.

If the request fails (offline host, DNS failure, timeout, malformed JSON, unsupported
version), a valid cache is used and the command reports a warning. If neither the
catalog nor a cache is available, the lookup returns no match and exits 1. The library UI
is never blocked by a lookup.

`--write` stores the best record in `library-metadata.json` (or an explicit
`--write <path>`) through `LibraryMetadataStore.Upsert`, so the next library scan shows
the metadata. Existing user media attachments and the local disc path are preserved.
Without `--write`, the catalog remains external and only the CLI prints the result.

The lookup is a metadata provider, not a scraper: it only downloads the configured URL
and sends no library, game, or user data.

## 3. Manual media

```powershell
5do metadata media "Game (USA).cue" --add "C:\media\manual.pdf"
5do metadata media "Game (USA).cue" --list
5do metadata media --game-id TETU0001 --remove "C:\media\manual.pdf"
```

The first `--add` creates a minimal metadata record when the game has none. Paths are
normalized to absolute paths and duplicate paths are ignored case-insensitively.
`--metadata <path>` targets a different metadata document, which is useful for tests and
named profiles.

## 4. Verification

- `MetadataCatalogClientTests` cover a file-URI lookup, cache write, offline fallback,
  exact game-ID preference, unsupported catalog version, media attach/detach idempotence,
  an unknown game, an empty-title/path record that must not match every query, and
  null-record/media normalization.
- `LibraryStateTests` continue to cover the offline overlay and per-game precedence.
- Game Metadata window smoke: the **Media** list is populated from the record, the
  **Add...** picker appends files, **Remove** deletes the selection, **Open** launches the
  selected file, and **Save** preserves the list in the metadata document.
- CLI smoke (2026-11-13): a file catalog populated `Test Game` / `Test Studio`; after the
  source file was removed, the same lookup returned `fromCache: true` with the record and
  a warning; `--add`, `--list`, and `--remove` round-tripped the media path.

## 5. Limitations

- There is no built-in third-party metadata service or API key. Point `--url` at a
  catalog you are allowed to use.
- The desktop Game Metadata window edits the local fields and attaches/removes media
  through the same metadata document; the CLI manages the same list for automation.
  Editing and saving the dialog preserves existing attachments.
- Catalog matching is exact and containment-based after normalization. It deliberately
  does not use the fuzzy cover matcher, so ambiguous titles do not silently overwrite the
  wrong game.
