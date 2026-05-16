#!/usr/bin/env bash
# Build the Transmux .deb package for Linux.
# Requires: dotnet-sdk-10.0, dpkg-dev, binutils
# Runtime dependency: ffmpeg (declared in Depends: — not bundled)
set -euo pipefail

# ── Dependency check ─────────────────────────────────────────────────────────

check_dep() {
  if ! command -v "$1" &>/dev/null; then
    echo "Missing required tool: $1" >&2
    echo "Install with: sudo apt-get install -y $2" >&2
    exit 1
  fi
}

check_dep dpkg-deb   dpkg-dev
check_dep ar         binutils
check_dep dotnet     dotnet-sdk-10.0

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

APP_PROJECT="src/Transmux.App/Transmux.App.csproj"
CONFIGURATION="${CONFIGURATION:-Release}"
RID="${RID:-linux-x64}"
PUBLISH_DIR="artifacts/publish/${RID}"
PACKAGE_ROOT="artifacts/pkg/transmux-deb"
DEB_DIR="artifacts/packages"
VERSION="${VERSION:-0.0.0-dev}"

case "${RID}" in
  linux-x64)  DEB_ARCH="amd64" ;;
  linux-arm64) DEB_ARCH="arm64" ;;
  *)
    echo "Unsupported RID: ${RID}" >&2
    exit 1
    ;;
esac

DEB_FILE="${DEB_DIR}/transmux_${VERSION}_${DEB_ARCH}.deb"
TMP_DEB_FILE="${DEB_DIR}/.transmux_${VERSION}_${DEB_ARCH}.deb.tmp"

# ── Build ─────────────────────────────────────────────────────────────────────

dotnet restore Transmux.sln
dotnet build Transmux.sln -c "${CONFIGURATION}" --no-restore
dotnet publish "${APP_PROJECT}" -c "${CONFIGURATION}" -r "${RID}" --self-contained true -o "${PUBLISH_DIR}" \
  -p:Version="${VERSION}" -p:InformationalVersion="${VERSION}"

# ── Stage deb layout ─────────────────────────────────────────────────────────

rm -rf "${PACKAGE_ROOT}" "${DEB_DIR}"
mkdir -p \
  "${PACKAGE_ROOT}/DEBIAN" \
  "${PACKAGE_ROOT}/opt/transmux" \
  "${PACKAGE_ROOT}/usr/bin" \
  "${PACKAGE_ROOT}/usr/share/applications" \
  "${PACKAGE_ROOT}/usr/share/icons/hicolor/scalable/apps" \
  "${DEB_DIR}"

cp -R "${PUBLISH_DIR}/." "${PACKAGE_ROOT}/opt/transmux/"
cp "src/Transmux.App/Assets/Icons/transmux.svg" \
   "${PACKAGE_ROOT}/usr/share/icons/hicolor/scalable/apps/transmux.svg"

# Wrapper launcher
cat > "${PACKAGE_ROOT}/opt/transmux/transmux" <<'LAUNCHER'
#!/bin/sh
exec "/opt/transmux/Transmux" "$@"
LAUNCHER

ln -s /opt/transmux/transmux "${PACKAGE_ROOT}/usr/bin/transmux"

# ── DEBIAN/control ────────────────────────────────────────────────────────────

cat > "${PACKAGE_ROOT}/DEBIAN/control" <<CONTROL
Package: transmux
Version: ${VERSION}
Section: video
Priority: optional
Architecture: ${DEB_ARCH}
Depends: ffmpeg
Maintainer: Piyush Doorwar
Description: Transmux audio/video converter
 A clean desktop converter for audio and video files, powered by Lumyn.
 Drop in a file, pick an output format, and convert.
CONTROL

# ── Desktop entry ─────────────────────────────────────────────────────────────

cat > "${PACKAGE_ROOT}/usr/share/applications/transmux.desktop" <<DESKTOP
[Desktop Entry]
Name=Transmux
Comment=Convert audio and video files
Exec=/opt/transmux/transmux %F
Icon=transmux
Terminal=false
Type=Application
StartupWMClass=Transmux
Categories=AudioVideo;AudioVideoEditing;
DESKTOP

# ── postinst / postrm ─────────────────────────────────────────────────────────

cat > "${PACKAGE_ROOT}/DEBIAN/postinst" <<'POSTINST'
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database /usr/share/applications || true
fi
POSTINST

cat > "${PACKAGE_ROOT}/DEBIAN/postrm" <<'POSTRM'
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database /usr/share/applications || true
fi
POSTRM

chmod 755 "${PACKAGE_ROOT}/DEBIAN"
chmod +x "${PACKAGE_ROOT}/DEBIAN/postinst"
chmod +x "${PACKAGE_ROOT}/DEBIAN/postrm"
chmod +x "${PACKAGE_ROOT}/opt/transmux/Transmux"
chmod +x "${PACKAGE_ROOT}/opt/transmux/transmux"

# ── Build deb ─────────────────────────────────────────────────────────────────

rm -f "${TMP_DEB_FILE}" "${DEB_FILE}"
dpkg-deb --root-owner-group -Zgzip --build "${PACKAGE_ROOT}" "${TMP_DEB_FILE}"

if ! ar t "${TMP_DEB_FILE}" | grep -q '^data\.tar'; then
  echo "Package validation failed: missing data.tar" >&2
  exit 1
fi

mv "${TMP_DEB_FILE}" "${DEB_FILE}"

echo "Linux artifacts:"
find artifacts -type f -name '*.deb' -print
