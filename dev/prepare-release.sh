#!/usr/bin/env bash
set -euo pipefail

# Local analogue of .github/workflows/release.yml for the test harness.
# Builds the plugin DLL, zips it, computes MD5, and writes a manifest.json
# pointing at the local manifest-server container. Output lands in
# .dev-env/dist/, which is bind-mounted into the manifest-server.
#
# Re-runnable — each call produces a fresh artifact at the same version,
# with the timestamp updated in manifest.json so Jellyfin refreshes its
# cache.

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
DIST="$ROOT/.dev-env/dist"

# Single source of truth for the plugin version in the test env: build.yaml.
VERSION=$(grep -E '^version:' "$ROOT/build.yaml" | sed -E 's/.*"([^"]+)".*/\1/')
TARGET_ABI=$(grep -E '^targetAbi:' "$ROOT/build.yaml" | sed -E 's/.*"([^"]+)".*/\1/')
GUID=$(grep -E '^guid:' "$ROOT/build.yaml" | sed -E 's/.*"([^"]+)".*/\1/')

ZIP_NAME="Jellyfin.Plugin.AgeRating_${VERSION}.zip"

mkdir -p "$DIST" 2>/dev/null || true
if [ -d "$DIST" ] && [ ! -w "$DIST" ]; then
  echo "==> Reclaiming dist directory ownership..."
  docker run --rm -v "$DIST":/m alpine:3.20 sh -c "chown -R $(id -u):$(id -g) /m"
fi
mkdir -p "$DIST"

# ── 1. Build the plugin (Release) via the .NET 9 SDK container ─────────────
echo "==> Building plugin..."
docker run --rm \
  -v "$ROOT":/src \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet build Jellyfin.Plugin.AgeRating.sln \
    -c Release \
    "/p:Version=${VERSION}" \
    /property:GenerateFullPaths=true \
    /consoleloggerparameters:NoSummary

DLL="$ROOT/Jellyfin.Plugin.AgeRating/bin/Release/net9.0/Jellyfin.Plugin.AgeRating.dll"
if [ ! -f "$DLL" ]; then
  echo "!! Build did not produce $DLL" >&2
  exit 1
fi

# ── 2. Package as zip (flat; Jellyfin extracts into its own plugin dir) ────
echo "==> Zipping $ZIP_NAME..."
rm -f "$DIST/$ZIP_NAME"
(cd "$(dirname "$DLL")" && zip -j "$DIST/$ZIP_NAME" "$(basename "$DLL")" >/dev/null)

CHECKSUM=$(md5sum "$DIST/$ZIP_NAME" | awk '{print $1}' | tr 'A-Z' 'a-z')
SIZE=$(stat -c%s "$DIST/$ZIP_NAME")
TIMESTAMP="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

# ── 3. Write the local manifest.json ─────────────────────────────────────
# sourceUrl uses the hostname Jellyfin's container sees on the compose
# network. Not reachable from the host browser — but Jellyfin downloads it
# itself, so that's fine.
MANIFEST="$DIST/manifest.json"
cat > "$MANIFEST" <<EOF
[
  {
    "guid": "$GUID",
    "name": "Age Rating Converter",
    "description": "Converts media age ratings between rating systems. Writes to CustomRating so conversions survive metadata refreshes.",
    "overview": "Target-system-driven age rating conversion for Jellyfin.",
    "owner": "dennis",
    "category": "Metadata",
    "imageUrl": "",
    "versions": [
      {
        "version": "$VERSION",
        "changelog": "Local test build.",
        "targetAbi": "$TARGET_ABI",
        "sourceUrl": "http://manifest-server/$ZIP_NAME",
        "checksum": "$CHECKSUM",
        "timestamp": "$TIMESTAMP"
      }
    ]
  }
]
EOF

echo
echo "==> Release artifacts ready in $DIST:"
echo "      $ZIP_NAME   ($SIZE bytes, MD5 $CHECKSUM)"
echo "      manifest.json"
echo
echo "    Jellyfin (inside the compose network) will fetch:"
echo "      http://manifest-server/manifest.json"
echo
