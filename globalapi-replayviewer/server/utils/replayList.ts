import { promises as fs } from 'node:fs'
import { join } from 'node:path'

// The GlobalAPI replay list dump this repo ships at server/data/ (same
// process.cwd()-based convention as server/middleware/gokz-export-assets.ts).
// It's ~73k records / ~34MB, so it's read once and cached per server
// process instead of on every request.
const REPLAY_LIST_PATH = join(process.cwd(), 'server', 'data', 'replay_list.json')

export interface ReplayListRecord {
  replay_id: number
  record_id: number
  plugin_id: number
  steamid64: string
  player_name: string
  server_id: number
  server_name: string
  map_id: number
  map_name: string
  mode_id: number
  mode_name: string
  stage: number
  tickrate: number
  has_teleports: number
  time: number
  teleports: number
  created_on: string
}

let cache: Promise<ReplayListRecord[]> | undefined

async function load(): Promise<ReplayListRecord[]> {
  const raw = await fs.readFile(REPLAY_LIST_PATH, 'utf-8')

  // steamid64s are 17-digit numbers, past Number.MAX_SAFE_INTEGER, but the
  // dump stores them as bare JSON numbers - JSON.parse would silently round
  // them to the nearest representable double. Quote the field first so it
  // parses as an exact string instead.
  const quoted = raw.replace(/"steamid64":\s*(\d+)/g, '"steamid64":"$1"')
  const records = JSON.parse(quoted) as ReplayListRecord[]

  return [...records].sort((a, b) => b.record_id - a.record_id)
}

export function getReplayList(): Promise<ReplayListRecord[]> {
  cache ??= load()
  return cache
}

export function filterReplayList(records: ReplayListRecord[], filters: GokzReplayListFilters): ReplayListRecord[] {
  const map = filters.map?.trim().toLowerCase()
  const player = filters.player?.trim().toLowerCase()
  const mode = filters.mode?.trim().toLowerCase()

  return records.filter((record) => {
    if (map && !record.map_name.toLowerCase().includes(map)) return false
    if (player && !record.player_name.toLowerCase().includes(player)) return false
    if (mode && record.mode_name.toLowerCase() !== mode) return false
    if (filters.steamid64 && record.steamid64 !== filters.steamid64) return false
    if (filters.stage !== undefined && record.stage !== filters.stage) return false
    if (filters.pro !== undefined && (record.teleports === 0) !== filters.pro) return false
    return true
  })
}
