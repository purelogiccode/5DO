#!/usr/bin/env bash
#
# Build (or update) a signed RPM repository from 5DO .rpm packages.
#
# dnf verifies repodata/repomd.xml when repo_gpgcheck=1, so the repository signature
# is what makes `dnf install 5do` trustworthy. Package header signatures added by
# build-rpm.sh --sign-key are checked when gpgcheck=1.
#
# Usage:
#   bash packaging/linux/build-rpm-repo.sh \
#       --repo-dir artifacts/mirror/rpm \
#       --base-url https://mirror.example.org/5do/rpm \
#       --key-id 5DO0123456789ABCDEF \
#       artifacts/installers
#
set -euo pipefail

repo_dir=""
base_url=""
key_id="${LINUX_GPG_KEY_ID:-}"
unsigned="false"
gpgcheck="1"
inputs=()

usage() {
    sed -n '2,17p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --repo-dir) repo_dir="${2:-}"; shift 2 ;;
        --base-url) base_url="${2:-}"; shift 2 ;;
        --key-id) key_id="${2:-}"; shift 2 ;;
        --unsigned) unsigned="true"; shift ;;
        --gpgcheck) gpgcheck="${2:-}"; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        --) shift; inputs+=("$@"); break ;;
        -*) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
        *) inputs+=("$1"); shift ;;
    esac
done

if [[ -z "$repo_dir" ]]; then
    echo "--repo-dir is required." >&2
    exit 2
fi
if [[ -z "$base_url" ]]; then
    echo "--base-url is required (it is written into 5do.repo)." >&2
    exit 2
fi
base_url="${base_url%/}"

createrepo=""
for candidate in createrepo_c createrepo; do
    if command -v "$candidate" >/dev/null 2>&1; then
        createrepo="$candidate"
        break
    fi
done
if [[ -z "$createrepo" ]]; then
    echo "createrepo_c is required (install createrepo-c or createrepo)." >&2
    exit 1
fi

if [[ "$unsigned" != "true" ]]; then
    if [[ -z "$key_id" ]]; then
        echo "--key-id or LINUX_GPG_KEY_ID is required (or pass --unsigned)." >&2
        exit 2
    fi
    if ! command -v gpg >/dev/null 2>&1; then
        echo "gpg is required to sign the repository metadata." >&2
        exit 1
    fi
fi

rpm_files=()
if [[ ${#inputs[@]} -gt 0 ]]; then
    for input in "${inputs[@]}"; do
        if [[ -d "$input" ]]; then
            while IFS= read -r -d '' file; do
                rpm_files+=("$file")
            done < <(find "$input" -maxdepth 1 -type f -name '*.rpm' -print0 | sort -z)
        elif [[ -f "$input" ]]; then
            rpm_files+=("$input")
        else
            echo "Not a file or directory: $input" >&2
            exit 1
        fi
    done
fi

if [[ ${#rpm_files[@]} -eq 0 ]]; then
    echo "No .rpm packages were passed; rebuilding indexes from the existing repository."
fi

mkdir -p "$repo_dir"
repo_dir="$(cd "$repo_dir" && pwd)"

for package in "${rpm_files[@]}"; do
    arch=""
    if command -v rpm >/dev/null 2>&1; then
        arch="$(rpm -qp --qf '%{ARCH}' "$package" 2>/dev/null || true)"
    fi
    if [[ -z "$arch" ]]; then
        case "$(basename "$package")" in
            *x86_64.rpm) arch="x86_64" ;;
            *aarch64.rpm) arch="aarch64" ;;
            *) echo "Cannot determine the architecture of $package" >&2; exit 1 ;;
        esac
    fi
    mkdir -p "$repo_dir/$arch"
    cp -f "$package" "$repo_dir/$arch/"
    printf 'Published %s -> %s/\n' "$(basename "$package")" "$arch"
done

if [[ -z "$(find "$repo_dir" -type f -name '*.rpm' -print -quit)" ]]; then
    echo "No .rpm packages found in $repo_dir." >&2
    exit 1
fi

for arch_dir in "$repo_dir"/*/; do
    [[ -d "$arch_dir" ]] || continue
    arch="$(basename "$arch_dir")"
    "$createrepo" --database --update "$arch_dir"
    if [[ "$unsigned" != "true" ]]; then
        rm -f "$arch_dir/repodata/repomd.xml.asc"
        gpg --batch --yes --armor --local-user "$key_id" --detach-sign \
            --output "$arch_dir/repodata/repomd.xml.asc" "$arch_dir/repodata/repomd.xml"
        gpg --batch --verify "$arch_dir/repodata/repomd.xml.asc" \
            "$arch_dir/repodata/repomd.xml" >/dev/null 2>&1
        printf 'Signed %s\n' "$arch_dir/repodata/repomd.xml"
    fi
done

cat >"$repo_dir/5do.repo" <<EOF
[5do]
name=5DO 3DO emulator
baseurl=$base_url/\$basearch
enabled=1
gpgcheck=$gpgcheck
repo_gpgcheck=$([[ "$unsigned" == "true" ]] && echo 0 || echo 1)
gpgkey=$base_url/5do-archive-keyring.gpg
EOF

cat >"$repo_dir/README.md" <<EOF
# 5DO RPM repository

\`\`\`sh
sudo rpm --import $base_url/5do-archive-keyring.gpg
sudo curl -fsSL $base_url/5do.repo -o /etc/yum.repos.d/5do.repo
sudo dnf install 5do
\`\`\`

\`repo_gpgcheck=1\` verifies \`repodata/repomd.xml\`; \`gpgcheck=$gpgcheck\` verifies
each package header signature. The repository metadata is signed by the 5DO release
key, and the keyring is published as \`5do-archive-keyring.gpg\` next to this README.
EOF

if [[ "$unsigned" == "true" ]]; then
    echo "WARNING: building an UNSIGNED RPM repository; dnf will reject it with" >&2
    echo "         repo_gpgcheck=1." >&2
fi

printf 'RPM repository ready: %s\n' "$repo_dir"
