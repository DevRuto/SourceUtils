import type { GokzReplayListEntry } from '~~/shared/types/gokzReplay'
import type { GokzReplay } from '~/lib/gokzReplay'
import { formatReplayTime } from '~/lib/format'

const MODE_LABELS: Record<string, GokzReplay['mode']> = {
  kz_timer: 'KZT',
  kz_simple: 'SKZ',
  kz_vanilla: 'VNL'
}

function toGokzReplay(entry: GokzReplayListEntry): GokzReplay {
  return {
    id: String(entry.replayId),
    map: entry.mapName ?? 'unknown map',
    player: entry.playerName ?? entry.steamid64,
    time: formatReplayTime(entry.time),
    mode: MODE_LABELS[entry.mode ?? ''] ?? 'KZT',
    pro: entry.teleports === 0,
    url: `/api/gokz-replays/${entry.replayId}`
  }
}

export function useGokzReplays(limit = 30) {
  const { data, pending, error, refresh } = useFetch<GokzReplayListEntry[]>('/api/gokz-replays/list', {
    query: { limit }
  })

  const replays = computed(() => (data.value ?? []).map(toGokzReplay))

  return { replays, pending, error, refresh }
}
