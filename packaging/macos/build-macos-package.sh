#!/usr/bin/env bash
#
# Build 5DO.app, a .dmg, and a .pkg from a published macOS bundle.
# Signing and notarization are opt-in: pass --sign-identity and --notary-profile.
#
# Usage:
#   bash packaging/macos/build-macos-package.sh \
#       --bundle artifacts/bundle/osx-arm64 --rid osx-arm64 --version 1.6.0 \
#       --output artifacts/installers
#
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"

bundle=""
rid="osx-arm64"
version=""
output_dir=""
sign_identity="${MACOS_SIGN_IDENTITY:-}"
notary_profile="${MACOS_NOTARY_PROFILE:-}"

usage() {
    sed -n '2,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --bundle) bundle="${2:-}"; shift 2 ;;
        --rid) rid="${2:-}"; shift 2 ;;
        --version) version="${2:-}"; shift 2 ;;
        --output) output_dir="${2:-}"; shift 2 ;;
        --sign-identity) sign_identity="${2:-}"; shift 2 ;;
        --notary-profile) notary_profile="${2:-}"; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
done

if [[ -z "$bundle" ]]; then
    echo "--bundle is required." >&2
    exit 2
fi
if [[ ! -d "$bundle" ]]; then
    echo "Bundle directory not found: $bundle" >&2
    exit 1
fi
bundle="$(cd "$bundle" && pwd)"

case "$rid" in
    osx-x64) mac_arch="x86_64" ;;
    osx-arm64) mac_arch="arm64" ;;
    *) echo "Unsupported --rid: $rid (expected osx-x64 or osx-arm64)" >&2; exit 2 ;;
esac

if [[ -z "$version" ]]; then
    version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo_root/Directory.Build.props" | head -n1)"
fi
version="${version#v}"
if [[ -z "$version" ]]; then
    version="0.0.0"
fi
bundle_version="$(printf '%s' "$version" | sed 's/[^0-9.]//g; s/^\.//; s/\.$//')"
if [[ -z "$bundle_version" ]]; then
    bundle_version="1.0"
fi

if [[ ! -f "$bundle/FiveDo.Frontend.Avalonia" ]]; then
    echo "Bundle is missing FiveDo.Frontend.Avalonia: $bundle" >&2
    exit 1
fi
if ! file "$bundle/FiveDo.Frontend.Avalonia" | grep -qi "Mach-O"; then
    echo "FiveDo.Frontend.Avalonia is not a Mach-O executable; publish the macOS bundle first." >&2
    exit 1
fi

for tool in hdiutil pkgbuild; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "$tool is required (run this script on macOS)." >&2
        exit 1
    fi
done

if [[ -z "$output_dir" ]]; then
    output_dir="$repo_root/artifacts/installers"
fi
mkdir -p "$output_dir"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

app="$work/5DO.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -a "$bundle"/. "$app/Contents/MacOS/"
find "$app/Contents/MacOS" -type f \( -name '*.pdb' -o -name '*.xml' \) -delete

sed \
    -e "s|@VERSION@|$bundle_version|g" \
    -e 's|@BUNDLE_ID@|com.purelogiccode.5do|g' \
    "$script_dir/Info.plist.in" >"$app/Contents/Info.plist"

printf 'APPL????' >"$app/Contents/PkgInfo"
chmod +x "$app/Contents/MacOS/FiveDo.Frontend.Avalonia" "$app/Contents/MacOS/FiveDo.Cli" || true

if [[ -n "$sign_identity" ]]; then
    if ! command -v codesign >/dev/null 2>&1; then
        echo "codesign is required for --sign-identity." >&2
        exit 1
    fi

    echo "Signing 5DO.app with $sign_identity"
    codesign \
        --force \
        --deep \
        --options runtime \
        --timestamp \
        --entitlements "$script_dir/entitlements.plist" \
        --sign "$sign_identity" \
        "$app"
else
    echo "No --sign-identity supplied; building an unsigned app."
fi

if [[ -n "$notary_profile" && -z "$sign_identity" ]]; then
    echo "--notary-profile requires --sign-identity (or MACOS_SIGN_IDENTITY)." >&2
    exit 1
fi

dmg_root="$work/dmg"
mkdir -p "$dmg_root"
cp -a "$app" "$dmg_root/"
ln -s /Applications "$dmg_root/Applications"

dmg="$output_dir/5do-${version}-${rid}.dmg"
rm -f "$dmg"
hdiutil create \
    -volname "5DO $version" \
    -srcfolder "$dmg_root" \
    -ov \
    -format UDZO \
    "$dmg" >/dev/null

pkg_root="$work/pkg"
mkdir -p "$pkg_root"
cp -a "$app" "$pkg_root/"
pkg="$output_dir/5do-${version}-${rid}.pkg"
rm -f "$pkg"
pkgbuild \
    --root "$pkg_root" \
    --identifier com.purelogiccode.5do \
    --version "$bundle_version" \
    --install-location /Applications \
    "$pkg" >/dev/null

if [[ -n "$notary_profile" ]]; then
    for artifact in "$dmg" "$pkg"; do
        echo "Notarizing $artifact"
        xcrun notarytool submit "$artifact" --keychain-profile "$notary_profile" --wait
        xcrun stapler staple "$artifact"
    done
fi

sha256sum "$dmg" "$pkg" >"$output_dir/SHA256SUMS-macos-$rid.txt" 2>/dev/null \
    || shasum -a 256 "$dmg" "$pkg" >"$output_dir/SHA256SUMS-macos-$rid.txt"

printf 'Built %s\n' "$dmg" "$pkg"
printf 'Architecture: %s\n' "$mac_arch"
