#!/usr/bin/env bash
set -euo pipefail

# Polls MAPSDIR (under GAMEDIR, including its workshop/<id>/ subdirectories - see MapLocator.cs)
# for .bsp files that don't have exported output yet, and runs SourceUtils.MapExport on each one
# found. Already-exported maps are skipped without even invoking the exporter, so this is safe to
# leave running indefinitely alongside a game server / workshop sync that drops in new maps over
# time - see docker-compose.yml's mapexport-watch service.

GAMEDIR="${GAMEDIR:?GAMEDIR is required}"
MAPSDIR="${MAPSDIR:-maps}"
OUTDIR="${OUTDIR:?OUTDIR is required}"
URL_PREFIX="${URL_PREFIX:-}"
POLL_SECONDS="${POLL_SECONDS:-30}"

ABS_MAPSDIR="$GAMEDIR/$MAPSDIR"

export_map() {
    local name="$1"
    echo "[watch-maps] New map detected: $name"
    dotnet SourceUtils.MapExport.dll \
        --gamedir "$GAMEDIR" \
        --mapsdir "$MAPSDIR" \
        --maps "$name" \
        --outdir "$OUTDIR" \
        --overwrite --verbose \
        --url-prefix "$URL_PREFIX"
}

echo "[watch-maps] Watching '$ABS_MAPSDIR' for new maps every ${POLL_SECONDS}s"

while true; do
    if [ -d "$ABS_MAPSDIR" ]; then
        while IFS= read -r -d '' bsp; do
            name="$(basename "$bsp" .bsp)"
            if [ ! -f "$OUTDIR/maps/$name/index.json" ]; then
                # A failed export (e.g. a .bsp still being written by a workshop sync) must not
                # take down the loop - index.json won't exist yet, so it's simply retried next poll.
                export_map "$name" || echo "[watch-maps] Failed to export $name, will retry next poll" >&2
            fi
        done < <(find "$ABS_MAPSDIR" -maxdepth 2 -iname '*.bsp' -print0)
    fi
    sleep "$POLL_SECONDS"
done
