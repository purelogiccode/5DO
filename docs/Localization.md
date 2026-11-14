# Localization

**Snapshot:** 2026-11-14. UX-09 adds an externalized string catalog, a desktop setting,
and `--locale`/`--list-locales` for both executables. The embedded catalogs are English,
German, Brazilian Portuguese, French, Spanish, Italian, Japanese, Korean, and Simplified
Chinese.

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
CLI usage and error strings. Every translated catalog covers the whole shell, all visible
dialog labels, all CLI-facing strings, and the core shell tooltips. Tooltips in secondary
dialogs that are absent from a catalog fall back to English; adding a key is enough to
translate them.

| Catalog | Language | Display name |
|---|---|---|
| `de` | German | Deutsch |
| `en` | English (baseline) | English |
| `es` | Spanish | Español |
| `fr` | French | Français |
| `it` | Italian | Italiano |
| `ja` | Japanese | 日本語 |
| `ko` | Korean | 한국어 |
| `pt-br` | Brazilian Portuguese | Português (Brasil) |
| `zh` | Simplified Chinese | 简体中文 |

A language-only or unmatched regional code resolves to the shipped catalog for that
language, so `pt`, `pt-PT`, and `pt-BR` all select `pt-br`, while `zh-CN` and `zh-Hans`
select `zh`.

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
- any shipped catalog code (`de`, `en`, `es`, `fr`, `it`, `ja`, `ko`, `pt-br`, `zh`)
  selects that catalog;
- an unknown value falls back to English without changing emulation.

**Settings > General > UI language** edits the saved value. The catalog is applied when
the main window is constructed; changing it in Settings compares the active locale before
and after the dialog, so `--locale` overrides also switch immediately, and reapplies the
pass to the main window. The pass remembers each control's original English text, so
switching between locales retranslates from that source and selecting English restores it
without a restart. Existing dialogs are not reopened; they pick up the locale when opened
again. Dynamic status text produced by the running game is not retranslated mid-sentence;
static menus, labels, and tooltips are.

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
FRONTEND SMOKE OK 1.9.1
locale=de
firstMenuHeader=Datei
windowState=Normal
```

## 4. CLI

Both executables accept `--locale <code>` and `--list-locales`:

```powershell
5do --list-locales
5do --locale de --help
5do --locale ja --version
FiveDo.Frontend.Avalonia --list-locales
FiveDo.Frontend.Avalonia --locale pt-br --smoke-test --smoke-output marker.txt
```

The CLI localizes its banner, `Usage`, `Commands`, and `Options` labels, the unknown
command message, and the locale warning. Machine-readable JSON errors stay in English so
scripts have a stable contract; developer command help and diagnostics remain English.

## 5. Adding a locale

1. Copy `Locales/en.json` to `Locales/<code>.json`.
2. Set `locale` to the lowercase code and `displayName` to the native name.
3. Translate values. Keys and JSON structure stay unchanged. Use an existing translated
   catalog such as `de.json` as the key subset when translating the shell, dialogs, CLI
   strings, and core tooltips.
4. Build. The locale appears automatically in Settings and `--list-locales`.
5. Run `dotnet test --filter FullyQualifiedName~Localization`.

`LocalizationCatalog.Parse` rejects duplicate keys, empty values, malformed JSON, and
missing `locale`/`strings` objects. The test suite also checks that:

- every static view string exists in `en.json`;
- every translated catalog covers every non-tooltip key;
- the core shell tooltips translate through the reverse lookup;
- switching from German to another locale and back to English retranslates from the
  remembered English source;
- regional codes resolve to the language catalog, language-only codes such as `pt`
  resolve to their regional catalog, and unknown locales leave the current selection
  unchanged.

## 6. Evidence

- `LocalizationTests` — catalog loading, reverse lookup, fallback, normalization,
  malformed input, full non-tooltip coverage, and locale switching.
- `LocalizationCoverageTests` — every static Avalonia view string is externalized.
- CLI: `--list-locales` prints every embedded catalog; `--locale de --help` prints
  `Aufruf: 5do <Befehl> [Optionen]` and `Befehle:`.
- Desktop: the translated smoke marker above was produced on Windows x64 on
  2026-11-14. The main menu, Settings tabs, and visible dialog labels use the German
  catalog, and the first menu header is `Datei`. The same run with `--locale fr` writes
  `firstMenuHeader=Fichier`, and `--locale ja` writes `firstMenuHeader=ファイル`.
  Changing the language back to English restores the English labels in the same
  session.
