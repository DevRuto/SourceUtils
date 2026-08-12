// Replay-list entry shared between the server proxy
// (server/api/gokz-replays/list.get.ts) and the client composable that
// consumes it (app/composables/useGokzReplays.ts).
export interface GokzReplayListEntry {
  id: number
  replayId: number
  steamid64: string
  playerName: string
  mapName: string
  modeName: string
  stage: number
  time: number
  teleports: number
  createdOn: string
}

// Query filters accepted by GET /api/gokz-replays/list, shared so the
// composable's params stay in sync with what the server actually reads.
export interface GokzReplayListFilters {
  map?: string
  player?: string
  mode?: string
  steamid64?: string
  stage?: number
  pro?: boolean
}
