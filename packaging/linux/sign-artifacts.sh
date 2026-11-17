#!/usr/bin/env bash
#
# Add GPG detached signatures to release artifacts and export the public keyring.
#
# Linux has no operating-system code signature for an ELF binary or a .deb: trust is
# anchored in the GPG key that signs the release checksums and the repository
# metadata. This script produces those per-file signatures and the keyring users
# install under /etc/apt/keyrings or /etc/pki/rpm-gpg.
#
# Usage:
#   bash packaging/linux/sign-artifacts.sh --key-id 5DO0123456789ABCDEF \
#       --keyring-out artifacts/signing/5do-archive-keyring.gpg \
#       artifacts/installers
#
# Environment:
#   LINUX_GPG_KEY_ID      default for --key-id
#   LINUX_GPG_HOME        optional GNUPGHOME override
#
set -euo pipefail

key_id="${LINUX_GPG_KEY_ID:-}"
keyring_out=""
digest="SHA512"
optional="false"
inputs=()

usage() {
    sed -n '2,18p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --key-id) key_id="${2:-}"; shift 2 ;;
        --keyring-out) keyring_out="${2:-}"; shift 2 ;;
        --digest) digest="${2:-}"; shift 2 ;;
        --optional) optional="true"; shift ;;
        -h|--help) usage; exit 0 ;;
        --) shift; inputs+=("$@"); break ;;
        -*) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
        *) inputs+=("$1"); shift ;;
    esac
done

if [[ -n "${LINUX_GPG_HOME:-}" ]]; then
    export GNUPGHOME="$LINUX_GPG_HOME"
fi

if [[ ${#inputs[@]} -eq 0 ]]; then
    echo "At least one file or directory is required." >&2
    exit 2
fi

if [[ -z "$key_id" ]]; then
    if [[ "$optional" == "true" ]]; then
        echo "No GPG key configured; skipping artifact signatures."
        exit 0
    fi
    echo "--key-id or LINUX_GPG_KEY_ID is required." >&2
    exit 2
fi

if ! command -v gpg >/dev/null 2>&1; then
    echo "gpg is required." >&2
    exit 1
fi

files=()
for input in "${inputs[@]}"; do
    if [[ -d "$input" ]]; then
        while IFS= read -r -d '' file; do
            files+=("$file")
        done < <(find "$input" -maxdepth 1 -type f \
            ! -name '*.asc' \
            ! -name '*.sha256' \
            ! -name '*.zsync' \
            ! -name 'SHA256SUMS*' \
            -print0 | sort -z)
    elif [[ -f "$input" ]]; then
        files+=("$input")
    else
        echo "Not a file or directory: $input" >&2
        exit 1
    fi
done

if [[ ${#files[@]} -eq 0 ]]; then
    echo "No signable files found." >&2
    exit 1
fi

fingerprint="$(gpg --batch --with-colons --fingerprint "$key_id" | awk -F: '/^fpr:/ { print $10 }')"
fingerprint="${fingerprint%%$'\n'*}"
if [[ -z "$fingerprint" ]]; then
    echo "GPG key not found in the current keyring: $key_id" >&2
    exit 1
fi
echo "Signing with $fingerprint"

for file in "${files[@]}"; do
    signature="$file.asc"
    rm -f "$signature"
    gpg --batch --yes --armor --digest-algo "$digest" \
        --local-user "$fingerprint" \
        --detach-sign --output "$signature" "$file"
    gpg --batch --verify "$signature" "$file" >/dev/null 2>&1
    printf 'Signed %s\n' "$file"
done

if [[ -n "$keyring_out" ]]; then
    mkdir -p "$(dirname "$keyring_out")"
    gpg --batch --export --armor "$fingerprint" >"$keyring_out.asc"
    gpg --batch --export "$fingerprint" >"$keyring_out"
    printf '%s\n' "$fingerprint" >"$keyring_out.fingerprint"
    printf 'Exported keyring %s and %s.asc\n' "$keyring_out" "$keyring_out"
    printf 'Fingerprint %s\n' "$fingerprint"
fi
