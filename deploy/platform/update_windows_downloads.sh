#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
DEFAULT_VERSION="$(tr -d '\r\n' < "$REPOSITORY_ROOT/VERSION")"
if [[ -z "$DEFAULT_VERSION" ]]; then
  echo "Repository VERSION is empty" >&2
  exit 2
fi
VERSION="${1:-$DEFAULT_VERSION}"
DESTINATION="${2:-/home/assistant/apps/videograbber-downloads}"
REPOSITORY="${VG_RELEASE_REPOSITORY:-velkomi/video-grabber}"
TAG="v${VERSION}"
BASE_URL="https://github.com/${REPOSITORY}/releases/download/${TAG}"

if ! command -v curl >/dev/null 2>&1; then
  echo "curl is required" >&2
  exit 2
fi
if ! command -v sha256sum >/dev/null 2>&1; then
  echo "sha256sum is required" >&2
  exit 2
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

manifest="VideoGrabber-${VERSION}.sha256.txt"
setup="VideoGrabber-Setup.exe"
portable="VideoGrabber.Managed-win-x64-${VERSION}.zip"

echo "Downloading verified VideoGrabber ${VERSION} artifacts..."
curl -fL --retry 3 --retry-delay 2 --connect-timeout 15   "${BASE_URL}/${manifest}" -o "${tmp}/${manifest}"
curl -fL --retry 3 --retry-delay 2 --connect-timeout 15   "${BASE_URL}/${setup}" -o "${tmp}/${setup}"
curl -fL --retry 3 --retry-delay 2 --connect-timeout 15   "${BASE_URL}/${portable}" -o "${tmp}/${portable}"

(
  cd "$tmp"
  grep -F " *${setup}" "$manifest" | sha256sum -c -
  grep -F " *${portable}" "$manifest" | sha256sum -c -
)

mkdir -p "$DESTINATION"
test -w "$DESTINATION" || {
  echo "Destination is not writable: $DESTINATION" >&2
  exit 3
}

install -m 0644 "${tmp}/${setup}" "${DESTINATION}/VideoGrabber-Setup.exe.new"
install -m 0644 "${tmp}/${portable}" "${DESTINATION}/VideoGrabber-Windows.zip.new"
install -m 0644 "${tmp}/${manifest}" "${DESTINATION}/VideoGrabber-Windows.sha256.txt.new"

mv -f "${DESTINATION}/VideoGrabber-Setup.exe.new"   "${DESTINATION}/VideoGrabber-Setup.exe"
mv -f "${DESTINATION}/VideoGrabber-Windows.zip.new"   "${DESTINATION}/VideoGrabber-Windows.zip"
mv -f "${DESTINATION}/VideoGrabber-Windows.sha256.txt.new"   "${DESTINATION}/VideoGrabber-Windows.sha256.txt"

printf '%s\n' "$VERSION" > "${DESTINATION}/VideoGrabber.version"

echo "WINDOWS_DOWNLOADS_UPDATED=${VERSION}"
echo "SETUP=${DESTINATION}/VideoGrabber-Setup.exe"
echo "PORTABLE=${DESTINATION}/VideoGrabber-Windows.zip"
