# FAQ

## Does 5DO include a BIOS or games?

No. Supply legally obtained files. Never upload BIOS ROMs, game images, or extracted
assets to the wiki, issues, discussions, or pull requests.

## Which disc formats work?

ISO (2048-byte sectors), BIN (2352-byte sectors), CUE+BIN, CHD (including parent/child),
and M3U/M3U8 playlists for multi-disc games.

## Is there a download?

Yes. **v1.0.0** is available on
[GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.0.0). There is
one bundle per system (Windows x64, Linux x64, macOS x64), and each bundle contains
both the desktop app and the CLI. You can also build from source.

## Do I need .NET installed?

No for the downloaded bundles. Each bundle includes a private .NET 10 runtime under
`runtime/`, shared by the two single-file apphosts. Keep the `runtime/` folder next to
`FiveDo.Cli` and `FiveDo.Frontend.Avalonia`.

The .NET 10 SDK is only required to build from source.

## How compatible is it?

The first full sweep covered 594 images. After triage, 593 are loadable/rendered, one
known no-video case remains (`Crash 'n Burn` Japan), and no not-loadable rows remain.
This is documented compatibility, not full-library compatibility. See
[Compatibility](Compatibility).

## Does it run on Linux and macOS?

**Windows x64 is the validated target.** Linux x64 passed the full test suite and
CLI/frontend smoke tests, with GPU presentation documented as preview/best-effort.
macOS x64 is built and published; runtime validation is post-1.0. arm64 and Raspberry
Pi are post-1.0.

## Why is it called 5DO?

C# identifiers cannot start with a digit. The solution and namespaces use the
`FiveDo` prefix.

## Are save states compatible with 4DO?

No. 5DO uses `.tdos` v3. Versions 1 and 2 remain readable. See
[Save states](https://purelogiccode.github.io/5DO/SaveStates.html).

## Does it collect telemetry?

No. Diagnostics bundles are local and scrub user paths and account names. See
[Privacy](https://purelogiccode.github.io/5DO/Privacy.html).

## Can I use it commercially?

No. 5DO is donationware under the FreeDO modified GNU LGPL. Donations do not grant
commercial rights. See [Legal](Legal).

## How do I report a bug?

Use the bug or compatibility issue form with version, OS, renderer, BIOS/disc details,
exact steps, and a privacy-scrubbed diagnostics bundle. See
[Contributing](Contributing).
