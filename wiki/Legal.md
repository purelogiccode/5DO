# Legal

## License

5DO is distributed under the **FreeDO modified GNU LGPL**. The FreeDO additional terms
take precedence over the GNU LGPL where they conflict. The 2026-11-25 `REL-13` review
retained those terms as non-commercial donationware; the 4DO game database is a separate
**GPL-2.0** data component with its complete text in
[`GPL-2.0.txt`](https://github.com/purelogiccode/5DO/blob/master/docs/GPL-2.0.txt).
Official Debian/Ubuntu/Fedora/openSUSE archives and Flathub are not eligible under the
retained terms. Read the full
[LICENSE](https://github.com/purelogiccode/5DO/blob/master/LICENSE) and
[third-party notices](https://github.com/purelogiccode/5DO/blob/master/THIRD-PARTY-NOTICES.md).

## Donationware

5DO is **donationware**: non-commercial, source-available software funded by voluntary
donations rather than sales. Donations do not grant commercial rights.

## No copyrighted content

This repository and wiki do not include BIOS ROMs, game images, or extracted game
assets. Do not upload them to issues, discussions, pull requests, the wiki, or
diagnostics bundles. Use hashes, synthetic fixtures, and descriptions instead.

## Trademarks

"3DO" and "Panasonic" are trademarks of their respective owners. 5DO is not affiliated
with or endorsed by Panasonic, The 3DO Company, or any rights holder.

## Distribution

The current release bundles contain the **self-contained** single-file CLI and desktop
app, the OpenAL Soft runtime, `README.md`, `whatsnew.md`, `LICENSE`,
`THIRD-PARTY-NOTICES.md`, and the legal/OpenAL notices. The .NET 11 runtime is embedded
in the apphosts. The OpenAL Soft binary stays outside the single-file apphosts so it
remains replaceable. Linux packages and the unsigned Windows installer scaffold exist;
the Windows installer is not a published release asset.

Read [Licensing](https://purelogiccode.github.io/5DO/Licensing.html),
[OpenAL Soft notice](https://purelogiccode.github.io/5DO/OpenALSoft.html), and
[Release legal review](https://purelogiccode.github.io/5DO/ReleaseLegalReview.html)
before distributing builds.
