#!/bin/sh
set -eu

DEST="/config/plugins/AgeRatingConverter_3b4a2e9f-7c1d-4e8b-a562-9f3d1c8e4a07_1.0.0.0"

mkdir -p "$DEST"
cp /plugin/Jellyfin.Plugin.AgeRating.dll "$DEST/"
cp /plugin/meta.json "$DEST/"
# Jellyfin rewrites meta.json to Status=NotSupported on a load failure and
# keeps that state across restarts. Overwriting it here guarantees the plugin
# starts Active every time this installer runs.

echo "Installed Age Rating Converter to $DEST"
ls -la "$DEST"
