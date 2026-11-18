#!/usr/bin/env bash
#
# Builds the native Linux bundles for one runtime identifier from a self-contained
# publish output:
#
#   SimpleLauncher-{version}-{rid}.AppImage   self-contained AppDir (bundled .NET runtime)
#   SimpleLauncher-{version}-{rid}.flatpak    Flatpak app bundle (org.freedesktop.Platform)
#
# The portable framework-dependent ZIP/tarball are produced by
# scripts/package-release-linux.ps1; this script only handles the formats that need
# Linux tooling (appimagetool, flatpak build-export/build-bundle).
#
# Usage:
#   ./scripts/package-linux-native.sh --version 5.8.4 --rid linux-x64 \
#       --payload artifacts/publish-linux/linux-x64/selfcontained \
#       --output artifacts/release
#
# The host needs: appimagetool (downloaded automatically), flatpak + flatpak-builder,
# and the org.freedesktop.{Platform,Sdk}//25.08 runtimes for the target architecture.
#
# Cross-arch note: an x86_64 host can build the aarch64 AppImage (appimagetool
# --runtime-file) and the aarch64 Flatpak (flatpak build-export --arch) because the
# payload is already compiled and no target code is executed.

set -euo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

APP_ID="com.purelogiccode.SimpleLauncher"
APP_NAME="Simple Launcher"
FLATPAK_RUNTIME_BRANCH="${FLATPAK_RUNTIME_BRANCH:-25.08}"

version=""
rid=""
payload=""
output=""
work=""
formats="appimage,flatpak"
icon=""

usage() {
    sed -n '2,32p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    cat <<'EOF'

Options:
  --version VERSION     Release version (e.g. 5.8.4)
  --rid RID             linux-x64 or linux-arm64
  --payload DIR         Self-contained publish directory (SimpleLauncher.Avalonia at its root)
  --output DIR          Output directory for the bundles (default: artifacts/release)
  --work DIR            Scratch directory (default: artifacts/native-linux)
  --formats LIST        Comma-separated: appimage,flatpak (default: both)
  --icon FILE           Application icon (default: SimpleLauncher.Avalonia/icon/icon2.png)
  --help                Show this help
EOF
}

die() {
    echo "error: $*" >&2
    exit 1
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version) version="${2:-}"; shift 2 ;;
        --rid) rid="${2:-}"; shift 2 ;;
        --payload) payload="${2:-}"; shift 2 ;;
        --output) output="${2:-}"; shift 2 ;;
        --work) work="${2:-}"; shift 2 ;;
        --formats) formats="${2:-}"; shift 2 ;;
        --icon) icon="${2:-}"; shift 2 ;;
        --help|-h) usage; exit 0 ;;
        *) die "unknown argument: $1" ;;
    esac
done

[[ -n "$version" ]] || die "--version is required"
[[ -n "$rid" ]] || die "--rid is required"
[[ -n "$payload" ]] || die "--payload is required"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "version must be Major.Minor.Patch"

case "$rid" in
    linux-x64)
        host_arch="$(uname -m)"
        [[ "$host_arch" == "x86_64" ]] || die "linux-x64 must be packaged on an x86_64 host"
        appimage_arch="x86_64"
        fp_arch="x86_64"
        sevenzip_name="7zz"
        ;;
    linux-arm64)
        host_arch="$(uname -m)"
        [[ "$host_arch" == "x86_64" || "$host_arch" == "aarch64" ]] || die "unsupported host architecture: $host_arch"
        appimage_arch="aarch64"
        fp_arch="aarch64"
        sevenzip_name="7zz_arm64"
        ;;
    *)
        die "unsupported rid '$rid' (expected linux-x64 or linux-arm64)"
        ;;
esac

payload="$(cd "$payload" && pwd)"
output="${output:-$repo_dir/artifacts/release}"
work="${work:-$repo_dir/artifacts/native-linux}"
mkdir -p "$output" "$work"
output="$(cd "$output" && pwd)"
work="$(cd "$work" && pwd)"

[[ -f "$payload/SimpleLauncher.Avalonia" ]] || die "payload is missing SimpleLauncher.Avalonia: $payload"
[[ -f "$payload/appsettings.json" ]] || die "payload is missing appsettings.json: $payload"

echo "== Simple Launcher $version ($rid) =="
echo "   payload: $payload"
echo "   output:  $output"
echo "   formats: $formats"

# The payload may come from a Windows publish (no Unix modes) or from a Linux publish.
# Everything the launcher executes needs the executable bit restored before packaging.
chmod +x "$payload/SimpleLauncher.Avalonia"
if [[ -d "$payload/tools" ]]; then
    find "$payload/tools" -type f -path "*/$rid/*" -exec chmod +x {} +
    if [[ -f "$payload/tools/SevenZip/$sevenzip_name" ]]; then
        chmod +x "$payload/tools/SevenZip/$sevenzip_name"
    fi
fi

ICON_SOURCE="${icon:-$repo_dir/SimpleLauncher.Avalonia/icon/icon2.png}"
[[ -f "$ICON_SOURCE" ]] || die "application icon not found: $ICON_SOURCE"

make_desktop_file() {
    local destination="$1"
    cat > "$destination" <<EOF
[Desktop Entry]
Type=Application
Name=$APP_NAME
Comment=Launch retro games with your emulators
Exec=$2
Icon=$3
Terminal=false
Categories=Game;Emulator;
Keywords=games;emulator;retro;rom;
StartupNotify=true
StartupWMClass=SimpleLauncher.Avalonia
EOF
}

make_appdata_file() {
    local destination="$1"
    cat > "$destination" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<component type="desktop-application">
  <id>$APP_ID</id>
  <name>$APP_NAME</name>
  <summary>Launch retro games with your emulators</summary>
  <developer id="com.purelogiccode">
    <name>PureLogicCode</name>
  </developer>
  <metadata_license>CC0-1.0</metadata_license>
  <project_license>GPL-3.0-or-later</project_license>
  <description>
    <p>
      Simple Launcher organizes ROM folders, downloads emulators and launches retro games
      with the emulator configured for each system.
    </p>
  </description>
  <launchable type="desktop-id">$APP_ID.desktop</launchable>
  <url type="homepage">https://github.com/purelogiccode/SimpleLauncher</url>
  <url type="bugtracker">https://github.com/purelogiccode/SimpleLauncher/issues</url>
  <releases>
    <release version="$version" date="$(date -u +%Y-%m-%d)"/>
  </releases>
</component>
EOF
}

# ---------------------------------------------------------------------------
# AppImage
# ---------------------------------------------------------------------------
build_appimage() {
    local appdir="$work/AppDir-$rid"
    local output_file="$output/SimpleLauncher-$version-$rid.AppImage"

    echo "-- AppImage ($appimage_arch)"
    rm -rf "$appdir"
    mkdir -p "$appdir/usr/bin"
    cp -a "$payload/." "$appdir/usr/bin/"

    cat > "$appdir/AppRun" <<'EOF'
#!/bin/sh
# AppImage entry point: the payload lives beside the launcher in usr/bin.
exec "$APPDIR/usr/bin/SimpleLauncher.Avalonia" "$@"
EOF
    chmod +x "$appdir/AppRun"

    cp "$ICON_SOURCE" "$appdir/simplelauncher.png"
    make_desktop_file "$appdir/simplelauncher.desktop" "SimpleLauncher.Avalonia %U" "simplelauncher"

    local tool="$work/appimagetool-$host_arch.AppImage"
    if [[ ! -x "$tool" ]]; then
        echo "   downloading appimagetool ($host_arch)"
        curl -fsSL --retry 3 --retry-delay 2 \
            -o "$tool" \
            "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-$host_arch.AppImage"
        chmod +x "$tool"
    fi

    local runtime_args=()
    if [[ "$appimage_arch" != "$host_arch" ]]; then
        local runtime="$work/runtime-$appimage_arch"
        if [[ ! -x "$runtime" ]]; then
            echo "   downloading the $appimage_arch AppImage runtime"
            curl -fsSL --retry 3 --retry-delay 2 \
                -o "$runtime" \
                "https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-$appimage_arch"
            chmod +x "$runtime"
        fi
        runtime_args=(--runtime-file "$runtime")
    fi

    rm -f "$output_file"
    ARCH="$appimage_arch" APPIMAGE_EXTRACT_AND_RUN=1 \
        "$tool" "${runtime_args[@]}" --no-appstream "$appdir" "$output_file"

    chmod +x "$output_file"
    echo "   $output_file ($(du -h "$output_file" | cut -f1))"
}

# ---------------------------------------------------------------------------
# Flatpak
# ---------------------------------------------------------------------------
build_flatpak() {
    local builddir="$work/flatpak-build-$rid"
    local repo="$work/flatpak-repo-$rid"
    local output_file="$output/SimpleLauncher-$version-$rid.flatpak"

    echo "-- Flatpak ($fp_arch, org.freedesktop.Platform//$FLATPAK_RUNTIME_BRANCH)"
    rm -rf "$builddir" "$repo"

    flatpak build-init --arch="$fp_arch" \
        "$builddir" "$APP_ID" \
        org.freedesktop.Sdk org.freedesktop.Platform "$FLATPAK_RUNTIME_BRANCH"

    install -d "$builddir/files/share/simplelauncher"
    cp -a "$payload/." "$builddir/files/share/simplelauncher/"

    install -d "$builddir/files/bin"
    cat > "$builddir/files/bin/simplelauncher" <<'EOF'
#!/bin/sh
exec /app/share/simplelauncher/SimpleLauncher.Avalonia "$@"
EOF
    chmod +x "$builddir/files/bin/simplelauncher"

    install -Dm644 "$ICON_SOURCE" \
        "$builddir/files/share/icons/hicolor/128x128/apps/$APP_ID.png"
    install -d "$builddir/files/share/applications"
    make_desktop_file "$builddir/files/share/applications/$APP_ID.desktop" \
        "simplelauncher %U" "$APP_ID"
    install -d "$builddir/files/share/metainfo"
    make_appdata_file "$builddir/files/share/metainfo/$APP_ID.metainfo.xml"

    # The emulator runs on the host (flatpak-spawn --host), hence:
    #   --filesystem=host                    ROM folders, emulator installs, BIOS/config
    #   --device=dri, --device=all           rendering + gamepads (Flatpak 1.14 has no
    #                                        narrower "input" device type)
    #   --talk-name=org.freedesktop.Flatpak  host command spawning
    #   --talk-name=org.a11y.Bus             screen-reader/AT-SPI access
    # FUSE mounting stays disabled inside the sandbox (see FuseValidation); archive and
    # Xbox/CHD launches use the existing extraction/conversion fallbacks.
    flatpak build-finish \
        --command=simplelauncher \
        --share=network \
        --share=ipc \
        --socket=wayland \
        --socket=fallback-x11 \
        --socket=pulseaudio \
        --device=dri \
        --device=all \
        --filesystem=host \
        --talk-name=org.freedesktop.Flatpak \
        --talk-name=org.a11y.Bus \
        "$builddir"

    flatpak build-export --arch="$fp_arch" "$repo" "$builddir"
    rm -f "$output_file"
    flatpak build-bundle --arch="$fp_arch" "$repo" "$output_file" "$APP_ID"

    echo "   $output_file ($(du -h "$output_file" | cut -f1))"
}

IFS=',' read -r -a requested_formats <<< "$formats"
for format in "${requested_formats[@]}"; do
    case "${format// /}" in
        appimage) build_appimage ;;
        flatpak) build_flatpak ;;
        "") ;;
        *) die "unknown format '$format' (expected appimage or flatpak)" ;;
    esac
done

echo "Done: $output"
