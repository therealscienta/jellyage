#!/bin/sh
set -eu

# The install directory mirrors Jellyfin's own <name>_<guid>_<version> convention so
# the dev harness looks like a real install. Every part of it is read out of
# meta.json rather than written here: the guid and version used to be hardcoded and
# silently drifted from meta.json across releases, leaving the directory claiming a
# version the plugin inside it was not.
META=/plugin/meta.json

json_field() {
  sed -n "s/.*\"$1\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p" "$META" | head -n 1
}

GUID=$(json_field guid)
VERSION=$(json_field version)

if [ -z "$GUID" ] || [ -z "$VERSION" ]; then
  echo "install-plugin: could not read guid/version from $META" >&2
  exit 1
fi

DEST="/config/plugins/AgeRatingConverter_${GUID}_${VERSION}"

mkdir -p "$DEST"
cp /plugin/Jellyfin.Plugin.AgeRating.dll "$DEST/"
cp "$META" "$DEST/"
# Jellyfin rewrites meta.json to Status=NotSupported on a load failure and
# keeps that state across restarts. Overwriting it here guarantees the plugin
# starts Active every time this installer runs.

echo "Installed Age Rating Converter $VERSION to $DEST"
ls -la "$DEST"
