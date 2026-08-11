// Proxies the KZ Global API replay list so the browser doesn't need to call
// kztimerglobal.com directly (no CORS headers there). Query params are
// forwarded as-is (steamid64, stage, etc. - see the GlobalAPI docs) with
// offset/limit defaulted and limit clamped to what the upstream allows.
//
// The list endpoint alone only returns steamid64 + record_filter_id, not
// display-friendly fields, so each entry is enriched with player/map/mode
// info from /records/{id}.
import type { GokzReplayListEntry } from '~~/shared/types/gokzReplay'

const REPLAY_LIST_URL = 'https://kztimerglobal.com/api/v2.0/records/replay/list'
const RECORD_URL = 'https://kztimerglobal.com/api/v2.0/records'
const MAX_LIMIT = 100

interface UpstreamReplayListEntry {
  id: number
  steamid64: string
  time: number
  teleports: number
  points: number
  created_on: string
  replay_id: number
}

interface UpstreamRecord {
  player_name: string
  map_name: string
  mode: string
}

export default defineEventHandler(async (event) => {
  const query = getQuery(event)

  const limit = Math.min(Math.max(Number(query.limit) || 20, 1), MAX_LIMIT)
  const offset = Math.max(Number(query.offset) || 0, 0)

  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (key === 'limit' || key === 'offset' || value === undefined) continue
    params.set(key, String(value))
  }
  params.set('offset', String(offset))
  params.set('limit', String(limit))

  const entries = await $fetch<UpstreamReplayListEntry[]>(`${REPLAY_LIST_URL}?${params.toString()}`)

  return Promise.all(
    entries
      .filter(entry => entry.replay_id)
      .map(async (entry): Promise<GokzReplayListEntry> => {
        const record = await $fetch<UpstreamRecord>(`${RECORD_URL}/${entry.id}`).catch(() => null)

        return {
          id: entry.id,
          replayId: entry.replay_id,
          steamid64: entry.steamid64,
          playerName: record?.player_name ?? null,
          mapName: record?.map_name ?? null,
          mode: record?.mode ?? null,
          time: entry.time,
          teleports: entry.teleports,
          points: entry.points,
          createdOn: entry.created_on
        }
      })
  )
})
