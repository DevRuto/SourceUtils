import { refDebounced } from '@vueuse/core'
import { computed, type Ref } from 'vue'
import type { GokzReplayListEntry, GokzReplayListFilters } from '~~/shared/types/gokzReplay'
import type { GokzReplay } from '~/lib/gokzReplay'
import { formatModeName, formatReplayTime } from '~/lib/format'

function toGokzReplay(entry: GokzReplayListEntry): GokzReplay {
  return {
    id: String(entry.replayId),
    map: entry.mapName,
    player: entry.playerName,
    time: formatReplayTime(entry.time),
    mode: formatModeName(entry.modeName),
    stage: entry.stage,
    pro: entry.teleports === 0,
    url: `/api/gokz-replays/${entry.replayId}`
  }
}

export function useGokzReplays(limit = 30, filters: Ref<GokzReplayListFilters>) {
  const debouncedFilters = refDebounced(filters, 300)

  const query = computed(() => ({
    limit,
    map: debouncedFilters.value.map || undefined,
    player: debouncedFilters.value.player || undefined,
    mode: debouncedFilters.value.mode || undefined,
    steamid64: debouncedFilters.value.steamid64 || undefined,
    stage: debouncedFilters.value.stage,
    pro: debouncedFilters.value.pro === undefined ? undefined : String(debouncedFilters.value.pro)
  }))

  const { data, pending, error, refresh } = useFetch<GokzReplayListEntry[]>('/api/gokz-replays/list', {
    query
  })

  const replays = computed(() => (data.value ?? []).map(toGokzReplay))

  return { replays, pending, error, refresh }
}
