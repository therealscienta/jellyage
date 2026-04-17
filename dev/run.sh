#!/usr/bin/env bash
set -euo pipefail

# Builds the Age Rating Converter plugin and runs Jellyfin 10.11 in Docker.
# Mock media is generated on first run.
#
# Two install modes, controlled by docker-compose profiles:
#
#   direct    (default) — plugin-installer sidecar drops the built DLL
#                         straight into /config/plugins/<name>_<version>/
#                         before Jellyfin starts. Fast for dev iteration.
#
#   manifest  (--via-manifest) — builds a release artifact (.zip + manifest.json)
#                         under .dev-env/dist/, serves it via the manifest-server
#                         container on the compose network, and leaves Jellyfin
#                         without the plugin preinstalled. Admin then pastes
#                         http://manifest-server/manifest.json into Dashboard →
#                         Plugins → Repositories to exercise the real install path.
#
# Usage:
#   ./dev/run.sh                   # build + start (direct mode)
#   ./dev/run.sh --via-manifest    # start in manifest mode (for release-flow test)
#   ./dev/run.sh --reset           # wipe .dev-env state, rebuild, restart (direct mode)
#   ./dev/run.sh --stop            # stop containers, keep state
#   ./dev/run.sh --down            # stop + remove containers, keep state
#   ./dev/run.sh --logs            # follow Jellyfin logs

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
DEV_ENV="$ROOT/.dev-env"
COMPOSE=(docker compose -f "$HERE/docker-compose.yml")

do_reset() {
  echo "==> Stopping and wiping .dev-env state..."
  "${COMPOSE[@]}" --profile direct --profile manifest down -v --remove-orphans 2>/dev/null || true
  # Jellyfin runs as root, so state files are root-owned. Use a container to
  # delete them AND reclaim ownership of the $DEV_ENV parent dir so the next
  # mkdir in ensure_media can create fresh subdirs as the host user.
  mkdir -p "$DEV_ENV" 2>/dev/null || true
  docker run --rm -v "$DEV_ENV":/t alpine:3.20 sh -c "
    rm -rf /t/config /t/cache /t/media /t/dist || true
    chown -R $(id -u):$(id -g) /t
  " || true
}

do_stop() {
  "${COMPOSE[@]}" --profile direct --profile manifest stop
}

do_down() {
  "${COMPOSE[@]}" --profile direct --profile manifest down --remove-orphans
}

do_logs() {
  "${COMPOSE[@]}" logs -f jellyfin
}

ensure_media() {
  mkdir -p "$DEV_ENV" 2>/dev/null || true
  # If a previous Jellyfin run left $DEV_ENV owned by root, reclaim it so
  # the mkdir below can create subdirs as the host user.
  if [ ! -w "$DEV_ENV" ]; then
    docker run --rm -v "$DEV_ENV":/t alpine:3.20 chown -R "$(id -u):$(id -g)" /t
  fi
  mkdir -p "$DEV_ENV/config" "$DEV_ENV/cache" "$DEV_ENV/media"
  if [ ! -d "$DEV_ENV/media/Movies" ] || [ -z "$(ls -A "$DEV_ENV/media/Movies" 2>/dev/null)" ]; then
    "$HERE/generate-media.sh"
  else
    echo "==> Mock media already present at $DEV_ENV/media/Movies (skipping generation)"
  fi
}

wait_for_jellyfin() {
  echo "==> Waiting for Jellyfin to respond..."
  until curl -fsS -o /dev/null http://localhost:8096/System/Info/Public 2>/dev/null; do
    sleep 2
  done
}

do_up_direct() {
  ensure_media
  echo "==> Building plugin and starting Jellyfin (direct install mode)..."
  "${COMPOSE[@]}" --profile direct up -d --build
  wait_for_jellyfin

  cat <<EOF

Jellyfin is up:  http://localhost:8096
Plugin is preinstalled via the direct-drop installer sidecar.
  1. Complete setup wizard; add a library at /media/Movies.
  2. Sidebar should show "Age Ratings" entry.
  3. Dashboard → Plugins → Age Rating Converter for settings/mappings.
EOF
}

do_up_manifest() {
  ensure_media
  mkdir -p "$DEV_ENV/dist"

  echo "==> Preparing release artifacts for manifest-server..."
  "$HERE/prepare-release.sh"

  echo "==> Starting Jellyfin + manifest-server (manifest install mode)..."
  "${COMPOSE[@]}" --profile manifest up -d --build
  wait_for_jellyfin

  cat <<EOF

Jellyfin is up:       http://localhost:8096
Manifest is served at http://manifest-server/manifest.json (compose network).

Round-trip install:
  1. In Jellyfin → Dashboard → Plugins → Repositories → Add:
       Repository Name:  jellyage (local)
       Repository URL:   http://manifest-server/manifest.json
  2. Dashboard → Plugins → Catalog → Metadata → Age Rating Converter → Install
  3. Restart Jellyfin when prompted.
  4. The plugin should load and the sidebar should show "Age Ratings".

To rebuild + reinstall after code changes, rerun ./dev/run.sh --via-manifest
(the .zip + manifest.json are regenerated; Jellyfin will offer an update once
its cache expires, or use "Check for updates" in the UI).
EOF
}

case "${1:-up}" in
  up|"")                   do_up_direct ;;
  --via-manifest|manifest) do_up_manifest ;;
  --reset|reset)           do_reset; do_up_direct ;;
  --stop|stop)             do_stop ;;
  --down|down)             do_down ;;
  --logs|logs)             do_logs ;;
  *)
    echo "Unknown option: $1" >&2
    echo "Usage: $0 [up|--via-manifest|--reset|--stop|--down|--logs]" >&2
    exit 2
    ;;
esac
