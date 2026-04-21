#!/usr/bin/env bash
set -euo pipefail

# Generates a mock media library under .dev-env/media for testing the plugin
# at realistic scale:
#   - 7 named Movies with fixed ratings (known fixtures, stable across runs)
#   - ~83 randomly-named Movies with varied ratings
#   - 10 randomly-named Series, each with 2 placeholder episodes
# All videos are 3-second ffmpeg testsrc clips. No real media is used.
#
# The script wipes the Movies/ and Shows/ directories on every run so repeat
# invocations don't accumulate duplicates. Re-run whenever you want fresh data.

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
MEDIA="$ROOT/.dev-env/media"
MOVIES="$MEDIA/Movies"
SHOWS="$MEDIA/Shows"

mkdir -p "$MEDIA" 2>/dev/null || true

# Jellyfin runs as root inside its container and may have written files here
# owned by root. Reclaim the tree so the rest of the script can write normally.
if [ -d "$MEDIA" ] && [ ! -w "$MEDIA" ]; then
  echo "==> Reclaiming media directory ownership..."
  MSYS_NO_PATHCONV=1 docker run --rm -v "$MEDIA":/m alpine:3.20 sh -c "chown -R $(id -u):$(id -g) /m"
fi

# ── 1. Seed video (re-used for every item) ────────────────────────────────
SEED="$MEDIA/_seed.mkv"
if [ ! -f "$SEED" ]; then
  echo "==> Generating seed video with ffmpeg..."
  MSYS_NO_PATHCONV=1 docker run --rm -v "$MEDIA":/work -w /work jrottenberg/ffmpeg:7.1-alpine \
    -hide_banner -loglevel error \
    -f lavfi -i "testsrc2=duration=3:size=320x240:rate=15" \
    -f lavfi -i "sine=frequency=440:duration=3" \
    -c:v libx264 -pix_fmt yuv420p -c:a aac -shortest \
    _seed.mkv
fi

# ── 2. Wipe any previous mock content ─────────────────────────────────────
# The dirs are bind-mounted into the Jellyfin container which writes as root.
# Delete via a throwaway alpine container so we don't hit permission errors.
if [ -d "$MOVIES" ] || [ -d "$SHOWS" ]; then
  echo "==> Wiping previous mock content..."
  MSYS_NO_PATHCONV=1 docker run --rm -v "$MEDIA":/m alpine:3.20 sh -c 'rm -rf /m/Movies /m/Shows'
fi
mkdir -p "$MOVIES" "$SHOWS"

# ── 3. Seven named movies (stable fixtures) ───────────────────────────────
write_movie_nfo() {
  local dir="$1" title="$2" year="$3" rating="$4"
  if [ -n "$rating" ]; then
    cat > "$dir/movie.nfo" <<EOF
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<movie>
  <title>$title</title>
  <year>$year</year>
  <mpaa>$rating</mpaa>
</movie>
EOF
  else
    cat > "$dir/movie.nfo" <<EOF
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<movie>
  <title>$title</title>
  <year>$year</year>
</movie>
EOF
  fi
}

declare -A NAMED=(
  ["Zero Horizon (2023)"]="PG-13"
  ["Mock Knight (2020)"]="R"
  ["Fjord Light (2018)"]="FSK 12"
  ["Silent Cove (2019)"]="15"
  ["Public Domain Sample (2021)"]="NR"
  ["Arctic Pulse (2017)"]="G"
  ["Classical Piece (2022)"]=""
)
for fname in "${!NAMED[@]}"; do
  dir="$MOVIES/$fname"
  mkdir -p "$dir"
  cp -f "$SEED" "$dir/$fname.mkv"
  year=$(echo "$fname" | grep -oE '[0-9]{4}')
  clean=$(echo "$fname" | sed "s/ ($year)//")
  write_movie_nfo "$dir" "$clean" "$year" "${NAMED[$fname]}"
done

# ── 4. Random-name pools ──────────────────────────────────────────────────
ADJ=(Silent Golden Frozen Burning Broken Hidden Glass Crimson Velvet Quiet \
     Distant Ancient Hollow Eternal Fading Silver Shadow Bright Iron Sacred \
     Lost Last First Blue Red Black White Deep Wild Lone Stormy Gentle Steady \
     Fierce Restless Endless Secret True Bold Polar Quick Northern Southern \
     Waking Dreaming Rising Falling Midnight Morning Evening Whispering)

NOUN=(Canyon Harbor Lake River Forest Mountain Desert Valley Horizon Ocean \
      Summit Bridge Garden Throne Empire Journey Matter Theory Kingdom Station \
      Circuit Pulse Tide Stranger Witness Ghost Runner Hunter Keeper Watch \
      Signal Anthem Resonance Threshold Passage Archive Requiem Current Beacon \
      Relic Spectacle Parable Fable Compass Lantern Meridian Covenant Orchid \
      Harvest Sovereign)

# Rating pool — weighted by inclusion count. Empty string means no <mpaa> tag
# (becomes "(none)" in the UI). "TV-14" and "TV-MA" are unmapped, so they show
# in the "All" view but not under "Has pending change".
RATINGS=(G G PG PG PG-13 PG-13 PG-13 R R R NC-17 \
         U 12 12A 15 15 18 R18 \
         "FSK 0" "FSK 6" "FSK 12" "FSK 12" "FSK 16" "FSK 18" \
         NR "Not Rated" Unrated "" "" "" \
         "TV-14" "TV-MA")

rand_item() {
  local -n arr=$1
  echo "${arr[RANDOM % ${#arr[@]}]}"
}

declare -A SEEN=()
make_unique_name() {
  local suffix="$1"
  local attempt
  for _ in 1 2 3 4 5; do
    attempt="$(rand_item ADJ) $(rand_item NOUN)${suffix}"
    if [ -z "${SEEN[$attempt]:-}" ]; then
      SEEN[$attempt]=1
      echo "$attempt"
      return
    fi
  done
  # fall back to a numbered suffix on persistent collision
  attempt="${attempt} ${RANDOM}"
  SEEN[$attempt]=1
  echo "$attempt"
}

# Mark the named fixtures as seen so random generation never duplicates them.
for fname in "${!NAMED[@]}"; do
  SEEN["$(echo "$fname" | sed 's/ ([0-9]*)$//')"]=1
done

# ── 5. 83 random movies ───────────────────────────────────────────────────
for _ in $(seq 1 83); do
  name="$(make_unique_name '')"
  year=$((1970 + RANDOM % 55))
  rating="$(rand_item RATINGS)"
  title="$name ($year)"
  dir="$MOVIES/$title"
  mkdir -p "$dir"
  cp -f "$SEED" "$dir/$title.mkv"
  write_movie_nfo "$dir" "$name" "$year" "$rating"
done

# ── 6. 10 random series (each with a season + 2 episodes) ────────────────
write_tvshow_nfo() {
  local dir="$1" title="$2" year="$3" rating="$4"
  if [ -n "$rating" ]; then
    cat > "$dir/tvshow.nfo" <<EOF
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<tvshow>
  <title>$title</title>
  <year>$year</year>
  <mpaa>$rating</mpaa>
</tvshow>
EOF
  else
    cat > "$dir/tvshow.nfo" <<EOF
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<tvshow>
  <title>$title</title>
  <year>$year</year>
</tvshow>
EOF
  fi
}

for _ in $(seq 1 10); do
  name="$(make_unique_name ' Chronicles')"
  year=$((1995 + RANDOM % 30))
  rating="$(rand_item RATINGS)"
  title="$name ($year)"
  show_dir="$SHOWS/$title"
  season_dir="$show_dir/Season 01"
  mkdir -p "$season_dir"
  write_tvshow_nfo "$show_dir" "$name" "$year" "$rating"
  cp -f "$SEED" "$season_dir/$name S01E01.mkv"
  cp -f "$SEED" "$season_dir/$name S01E02.mkv"
done

rm -f "$SEED"

movie_count=$(find "$MOVIES" -mindepth 1 -maxdepth 1 -type d | wc -l)
show_count=$(find "$SHOWS" -mindepth 1 -maxdepth 1 -type d | wc -l)
echo "==> Generated $movie_count movies and $show_count series."
echo "   Movies: /media/Movies (inside container)"
echo "   Shows:  /media/Shows (add a TV Shows library pointing here to see Series in the plugin)"
