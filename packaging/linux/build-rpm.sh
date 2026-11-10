#!/usr/bin/env bash
#
# Build an unsigned RPM package from a published linux bundle.
#
# Usage:
#   bash packaging/linux/build-rpm.sh --bundle artifacts/bundle/linux-x64 \
#       --rid linux-x64 --version 1.6.0 --output artifacts/installers
#
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"

bundle=""
rid="linux-x64"
version=""
output_dir=""

usage() {
    sed -n '2,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --bundle) bundle="${2:-}"; shift 2 ;;
        --rid) rid="${2:-}"; shift 2 ;;
        --version) version="${2:-}"; shift 2 ;;
        --output) output_dir="${2:-}"; shift 2 ;;
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

if [[ -z "$version" ]]; then
    version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo_root/Directory.Build.props" | head -n1)"
fi
version="${version#v}"
if [[ -z "$version" ]]; then
    version="0.0.0"
fi
rpm_version="${version//-/_}"

case "$rid" in
    linux-x64) rpm_arch="x86_64" ;;
    linux-arm64) rpm_arch="aarch64" ;;
    *) echo "Unsupported --rid: $rid (expected linux-x64 or linux-arm64)" >&2; exit 2 ;;
esac

for executable in FiveDo.Frontend.Avalonia FiveDo.Cli; do
    if [[ ! -x "$bundle/$executable" ]]; then
        echo "Bundle is missing executable $executable: $bundle" >&2
        exit 1
    fi
done

if ! command -v rpmbuild >/dev/null 2>&1; then
    echo "rpmbuild is required (install rpm-build)." >&2
    exit 1
fi

if [[ -z "$output_dir" ]]; then
    output_dir="$repo_root/artifacts/installers"
fi
mkdir -p "$output_dir"

top="$(mktemp -d)"
trap 'rm -rf "$top"' EXIT
mkdir -p "$top/BUILD" "$top/RPMS" "$top/SOURCES" "$top/SPECS" "$top/SRPMS"

cat >"$top/SPECS/5do.spec" <<EOF
Name:           5do
Version:        $rpm_version
Release:        1%{?dist}
Summary:        3DO Interactive Multiplayer emulator
License:        FreeDO modified LGPL
URL:            https://github.com/purelogiccode/5DO
BuildArch:      $rpm_arch
Requires:       dotnet-runtime-10.0

%description
5DO is a FreeDO-derived 3DO emulator with a headless CLI and an Avalonia desktop
frontend. This package installs both apphosts under /opt/5do and registers the
5do desktop entry, icon, and disc-image MIME type.

%install
rm -rf %{buildroot}
mkdir -p %{buildroot}/opt/5do
mkdir -p %{buildroot}/usr/bin
mkdir -p %{buildroot}/usr/share/applications
mkdir -p %{buildroot}/usr/share/mime/packages
mkdir -p %{buildroot}/usr/share/icons/hicolor/scalable/apps
mkdir -p %{buildroot}/usr/share/doc/5do
cp -a %{_bundle_dir}/. %{buildroot}/opt/5do/
rm -f %{buildroot}/opt/5do/5do.desktop
find %{buildroot}/opt/5do -type f \\( -name '*.pdb' -o -name '*.xml' \\) -delete
find %{buildroot}/opt/5do -type d -exec chmod 0755 {} +
find %{buildroot}/opt/5do -type f -exec chmod 0644 {} +
chmod 0755 %{buildroot}/opt/5do/FiveDo.Frontend.Avalonia %{buildroot}/opt/5do/FiveDo.Cli
ln -sf /opt/5do/FiveDo.Frontend.Avalonia %{buildroot}/usr/bin/5do
ln -sf /opt/5do/FiveDo.Cli %{buildroot}/usr/bin/5do-cli
install -m 0644 %{_repo_dir}/packaging/5do.desktop %{buildroot}/usr/share/applications/5do.desktop
install -m 0644 %{_repo_dir}/packaging/linux/5do.xml %{buildroot}/usr/share/mime/packages/5do.xml
install -m 0644 %{_repo_dir}/packaging/5do.svg %{buildroot}/usr/share/icons/hicolor/scalable/apps/5do.svg
for document in LICENSE THIRD-PARTY-NOTICES.md README.md; do
    if [ -f "%{_repo_dir}/\$document" ]; then
        install -m 0644 "%{_repo_dir}/\$document" "%{buildroot}/usr/share/doc/5do/\$document"
    fi
done

%files
/opt/5do
/usr/bin/5do
/usr/bin/5do-cli
/usr/share/applications/5do.desktop
/usr/share/mime/packages/5do.xml
/usr/share/icons/hicolor/scalable/apps/5do.svg
/usr/share/doc/5do

%post
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database -q /usr/share/applications || true
fi
if command -v update-mime-database >/dev/null 2>&1; then
    update-mime-database /usr/share/mime || true
fi

%postun
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database -q /usr/share/applications || true
fi
if command -v update-mime-database >/dev/null 2>&1; then
    update-mime-database /usr/share/mime || true
fi

%changelog
* $(date -u '+%a %b %d %Y') 5DO <noreply@localhost> - $rpm_version-1
- Automated package build
EOF

rpmbuild -bb \
    --define "_topdir $top" \
    --define "_bundle_dir $bundle" \
    --define "_repo_dir $repo_root" \
    "$top/SPECS/5do.spec"

rpm_file="$(find "$top/RPMS" -type f -name '*.rpm' | head -n1)"
if [[ -z "$rpm_file" ]]; then
    echo "rpmbuild produced no package." >&2
    exit 1
fi

archive="$output_dir/5do-${version}-1.${rpm_arch}.rpm"
cp "$rpm_file" "$archive"
sha256sum "$archive" >"$archive.sha256"
printf 'Built %s\n' "$archive"
