// Enriched replay-list entry shared between the server proxy
// (server/api/gokz-replays/list.get.ts) and the client composable that
// consumes it (app/composables/useGokzReplays.ts).
export interface GokzReplayListEntry {
  id: number
  replayId: number
  steamid64: string
  playerName: string | null
  mapName: string | null
  mode: string | null
  time: number
  teleports: number
  points: number
  createdOn: string
}
