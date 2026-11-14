# Localization

**Snapshot:** 2026-11-13. UX-09 adds an externalized string catalog, English and German
locales, a desktop setting, and `--locale`/`--list-locales` for both executables.

Localization changes presentation text only. It never touches emulation, input, audio,
save states, frame hashes, disc timing, or NVRAM.

## 1. Catalogs

Catalogs live in `src/FiveDo.Localization/Locales/*.json` and are embedded in
`FiveDo.Localization.dll`. A catalog is:

```json
{
  "locale": "de",
  "displayName": "Deutsch",
  "strings": {
    "ui.file": "Datei",
    "cli.usage": "Aufruf: 5do <Befehl> [Optionen]"
  }
}
```

`en.json` is the baseline. It contains every static user-visible string in the Avalonia
views (labels, menu items, headers, window titles, placeholders, and tooltips) plus the
CLI usage and error strings. `de.json` translates the whole shell, all visible dialog
labels, all CLI-facing strings, and the core shell tooltips. Tooltips in secondary
dialogs that are absent from `de.json` fall back to English; adding a key is enough to
translate them.

The JSON is parsed with `JsonDocument` rather than reflection serialization, so the
catalog is trimming- and NativeAOT-safe. The application is built with invariant
globalization; locale selection is an explicit catalog choice, not a `CultureInfo`
change.

## 2. Lookup order

`Localizer.Translate(key)` returns:

1. the active locale's translation;
2. the English baseline for a missing key;
3. the key itself when neither catalog defines it.

`Localizer.TranslateText(english)` performs a reverse lookup of an existing English
string through the baseline and then the active catalog. Untranslated text is returned
unchanged. This lets the existing desktop code keep its literals while the catalogs stay
the single source of translations.

`Localizer.ResolveSystemLocale()` checks `FIVEDO_LOCALE`, `LANGUAGE`, `LC_ALL`,
`LC_MESSAGES`, and `LANG`, then `CurrentUICulture`, and falls back to `en`. Regional codes
such as `de-DE` resolve to the language catalog. `--locale` overrides the saved setting
for one launch.

## 3. Desktop

The environment variable `FIVEDO_LOCALE` and the `--locale` argument affect the desktop
before the first window is realized. Otherwise `AppSettings.Language` is used:

- `system` (default) follows the host environment;
- `en` and `de` select a catalog;
- an unknown value falls back to English without changing emulation.

**Settings > General > UI language** edits the saved value. The catalog is applied when
the main window is constructed; changing it in Settings reapplies the pass to the main
window immediately. Existing dialogs are not reopened. Dynamic status text produced by
the running game is not retranslated mid-sentence; static menus, labels, and tooltips are.

`App.Initialize` installs a `Control.LoadedEvent` class handler that walks each window's
logical tree after it is realized. The pass sets:

- `Window.Title`;
- `TextBlock.Text`;
- content strings on buttons, check boxes, labels, tabs, and menu items;
- `HeaderedContentControl`/`HeaderedItemsControl` headers;
- `TextBox.PlaceholderText`;
- attached `ToolTip.Tip` strings.

Properties with a data binding are skipped, so a view-model value is never frozen by a
local set.

**Proof of a translated tree.** `--smoke-test --locale de` writes a marker that includes
the catalog locale and the first menu header read from the realized window:

```text
FRONTEND SMOKE OK 1.9.0
locale=de
firstMenuHeader=Datei
windowState=Normal
```

## 4. CLI

Both executables accept `--locale <code>` and `--list-locales`:

```powershell
5do --list-locales
5do --locale de --help
FiveDo.Frontend.Avalonia --list-locales
FiveDo.Frontend.Avalonia --locale de --smoke-test --smoke-output marker.txt
```

The CLI localizes its banner, `Usage`, `Commands`, and `Options` labels, the unknown
command message, and the locale warning. Machine-readable JSON errors stay in English so
scripts have a stable contract; developer command help and diagnostics remain English.

## 5. Adding a locale

1. Copy `Locales/en.json` to `Locales/<code>.json`.
2. Set `locale` to the lowercase code and `displayName` to the native name.
3. Translate values. Keys and JSON structure stay unchanged.
4. Build. The locale appears automatically in Settings and `--list-locales`.
5. Run `dotnet test --filter FullyQualifiedName~Localization`.

`LocalizationCatalog.Parse` rejects duplicate keys, empty values, malformed JSON, and
missing `locale`/`strings` objects. The test suite also checks that:

- every static view string exists in `en.json`;
- every non-tooltip key has a German translation;
- the core shell tooltips translate through the reverse lookup;
- regional codes resolve to the language catalog and unknown locales leave the current
  selection unchanged.

## 6. Evidence

- `LocalizationTests` — catalog loading, reverse lookup, fallback, normalization,
  malformed input, and German coverage.
- `LocalizationCoverageTests` — every static Avalonia view string is externalized.
- CLI: `--list-locales` prints `de	Deutsch` and `en	English`; `--locale de --help`
  prints `Aufruf: 5do <Befehl> [Optionen]` and `Befehle:`.
- Desktop: the translated smoke marker above was produced on Windows x64 on
  2026-11-14. The main menu, Settings tabs, and visible dialog labels use the German
  catalog, and the first menu header is `Datei`.
