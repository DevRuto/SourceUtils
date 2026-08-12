// Serves the recent-replays list from the local replay_list.json dump
// (server/utils/replayList.ts) instead of the upstream GlobalAPI
// /records/replay/list endpoint. That endpoint only returns bare numeric
// ids (steamid64, record id, replay id) with no player/map/mode - filtering
// or displaying anything descriptive meant fanning out to a per-entry
// /records/{id} lookup, which got the app rate-limited (see the old
// [id].get.ts-adjacent history). The local dump already has all of that
// baked in, so filtering here is a plain in-memory scan.
import type { GokzReplayListEntry } from '~~/shared/types/gokzReplay'

const MAX_LIMIT = 200

function asString(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() ? value.trim() : undefined
}

export default defineEventHandler(async (event): Promise<GokzReplayListEntry[]> => {
  const query = getQuery(event)

  const limit = Math.min(Math.max(Number(query.limit) || 20, 1), MAX_LIMIT)
  const offset = Math.max(Number(query.offset) || 0, 0)

  const records = await getReplayList()
  const filtered = filterReplayList(records, {
    map: asString(query.map),
    player: asString(query.player),
    mode: asString(query.mode),
    steamid64: asString(query.steamid64),
    stage: query.stage !== undefined ? Number(query.stage) : undefined,
    pro: query.pro !== undefined ? query.pro === 'true' : undefined
  })

  return filtered.slice(offset, offset + limit).map((record): GokzReplayListEntry => ({
    id: record.record_id,
    replayId: record.replay_id,
    steamid64: record.steamid64,
    playerName: record.player_name,
    mapName: record.map_name,
    modeName: record.mode_name,
    stage: record.stage,
    time: record.time,
    teleports: record.teleports,
    createdOn: record.created_on
  }))
})
