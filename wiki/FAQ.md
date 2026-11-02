# FAQ

## Does 5DO include a BIOS or games?

No. Supply legally obtained files. Never upload BIOS ROMs, game images, or extracted
assets to the wiki, issues, discussions, or pull requests.

## Which disc formats work?

ISO (2048-byte sectors), BIN (2352-byte sectors), CUE+BIN, CHD (including parent/child),
and M3U/M3U8 playlists for multi-disc games.

## Is there a download?

Not yet. The project is pre-1.0. Build from source or use the portable archives from a
successful [CI run](https://github.com/purelogiccode/5DO/actions/workflows/ci.yml).

## How compatible is it?

The first full sweep covered 594 images. After triage, 593 are loadable/rendered, one
known no-video case remains (`Crash 'n Burn` Japan), and no not-loadable rows remain.
This is documented compatibility, not full-library compatibility. See
[Compatibility](Compatibility).

## Does it run on Linux and macOS?

Builds are published for Windows, Linux, and macOS on x64/arm64. **Windows x64 is the
validated target**; Linux/macOS runtime validation is ongoing.

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
