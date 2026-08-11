import type { GokzReplayListEntry } from '~~/shared/types/gokzReplay'
import type { GokzReplay } from '~/lib/gokzReplay'
import { formatReplayTime } from '~/lib/format'

function toGokzReplay(entry: GokzReplayListEntry): GokzReplay {
  return {
    id: String(entry.replayId),
    map: `Record #${entry.id}`,
    player: entry.steamid64,
    time: formatReplayTime(entry.time),
    mode: 'KZT',
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
