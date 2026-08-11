// Proxies the KZ Global API replay list so the browser doesn't need to call
// kztimerglobal.com directly (no CORS headers there). Query params are
// forwarded as-is (steamid64, stage, etc. - see the GlobalAPI docs) with
// offset/limit defaulted and limit clamped to what the upstream allows.
//
// Deliberately a single upstream call: the list endpoint's entries used to
// be enriched with a per-entry /records/{id} lookup, but that fanned out to
// N+1 concurrent requests and got the app rate-limited. Callers just get the
// raw record id for now instead of a display-friendly player/map/mode.
import type { GokzReplayListEntry } from '~~/shared/types/gokzReplay'

const REPLAY_LIST_URL = 'https://kztimerglobal.com/api/v2.0/records/replay/list'
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

  return entries
    .filter(entry => entry.replay_id)
    .map((entry): GokzReplayListEntry => ({
      id: entry.id,
      replayId: entry.replay_id,
      steamid64: entry.steamid64,
      time: entry.time,
      teleports: entry.teleports,
      points: entry.points,
      createdOn: entry.created_on
    }))
})
