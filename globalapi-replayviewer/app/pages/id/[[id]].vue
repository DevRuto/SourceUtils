<script setup lang="ts">
import { nextTick, ref, useTemplateRef, watch } from 'vue'
import { Button } from '~/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '~/components/ui/card'
import { Input } from '~/components/ui/input'
import { Label } from '~/components/ui/label'
import { formatReplayTime } from '~/lib/format'
import type { GokzReplay } from '~/lib/gokzReplay'
import type { GokzReplayListFilters } from '~~/shared/types/gokzReplay'

interface LoadedReplayInfo {
  mapName: string
  playerName: string
  time: number
  mode: number
  modeName: string
  teleportsUsed: number
  course: number
}

const viewer = useTemplateRef('viewer')
const selectedId = ref<string | null>(null)
const customUrl = ref('')
const loadedInfo = ref<LoadedReplayInfo | null>(null)
const isPlaying = ref(false)

const route = useRoute()
const router = useRouter()

// Filters live in the URL query string (?map=...&player=...) rather than
// component-local state, so they're shareable/bookmarkable and survive
// navigating to/from a replay.
function filtersFromQuery(query: typeof route.query): GokzReplayListFilters {
  return {
    map: typeof query.map === 'string' ? query.map : undefined,
    player: typeof query.player === 'string' ? query.player : undefined,
    mode: typeof query.mode === 'string' ? query.mode : undefined,
    steamid64: typeof query.steamid64 === 'string' ? query.steamid64 : undefined,
    stage: typeof query.stage === 'string' ? Number(query.stage) : undefined,
    pro: query.pro === 'true' ? true : undefined
  }
}

function queryFromFilters(filters: GokzReplayListFilters) {
  return {
    map: filters.map || undefined,
    player: filters.player || undefined,
    mode: filters.mode || undefined,
    steamid64: filters.steamid64 || undefined,
    stage: filters.stage !== undefined ? String(filters.stage) : undefined,
    pro: filters.pro ? 'true' : undefined
  }
}

const replayFilters = ref<GokzReplayListFilters>(filtersFromQuery(route.query))
const { replays, pending: replaysPending } = useGokzReplays(30, replayFilters)

// The vendored engine can't load a second replay into an existing viewer
// instance (its Map.unload() is unimplemented and throws), so every replay
// load forces <GokzViewer> to remount via this key instead of reusing one.
const viewerKey = ref(0)

async function playReplay(url: string) {
  loadedInfo.value = null
  viewerKey.value++
  await nextTick()
  viewer.value?.loadReplay(url)
}

watch(
  replayFilters,
  (value) => {
    router.replace({ query: { ...queryFromFilters(value) } })
  },
  { deep: true }
)

function loadFromList(replay: GokzReplay) {
  // Just navigate - the route watcher below is the single place that sets
  // selectedId and calls playReplay. Triggering both from here too used to
  // race it: a slow-to-resolve navigation from an earlier click could fire
  // the watcher with a stale id after a newer click's viewer instance was
  // already created, replaying an old URL into it and re-tripping the
  // engine's "Map unloading not implemented" crash.
  router.replace({ path: `/id/${replay.id}`, query: route.query })
}

function loadFromUrl() {
  if (!customUrl.value.trim()) return
  selectedId.value = null
  playReplay(customUrl.value.trim())
  router.replace({ path: '/id', query: route.query })
}

function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  selectedId.value = null
  playReplay(URL.createObjectURL(file))
  input.value = ''
  router.replace({ path: '/id', query: route.query })
}

// The single place that loads a replay named in the URL (/id/<id>) - by id
// directly, rather than waiting for it to show up in the (paginated/
// filtered) recent-replays list, since the linked replay is very often
// older than whatever page of "recent" results happens to be loaded. Runs
// for every route change (not just the first), since the page component is
// kept alive across navigations (app.vue's static page-key) rather than
// remounted, and also runs when `viewer` changes on its own (e.g. right
// after this watcher's own playReplay() remounts <GokzViewer>) - the
// selectedId check is what makes that second firing a no-op instead of a
// duplicate load.
watch(
  [() => route.params.id, viewer],
  ([wanted, viewerInstance]) => {
    if (typeof wanted !== 'string' || !viewerInstance || selectedId.value === wanted) return

    selectedId.value = wanted
    playReplay(`/api/gokz-replays/${wanted}`)
  },
  { immediate: true }
)

function onReplayLoaded(info: LoadedReplayInfo) {
  loadedInfo.value = info
}
</script>

<template>
  <div class="flex h-full w-full flex-col bg-background text-foreground">
    <header class="flex h-14 shrink-0 items-center justify-between border-b px-4">
      <h1 class="text-sm font-semibold">GlobalAPI.ReplayViewer</h1>
      <p v-if="loadedInfo" class="text-sm text-muted-foreground">
        {{ loadedInfo.playerName }} &middot; {{ loadedInfo.mapName }} &middot;
        {{ formatReplayTime(loadedInfo.time) }} &middot; {{ loadedInfo.modeName?.toUpperCase() }} &middot;
        {{ loadedInfo.teleportsUsed === 0 ? 'PRO' : 'NUB' }}
      </p>
      <p v-else class="text-sm text-muted-foreground">No replay loaded</p>
    </header>

    <div class="flex flex-1 gap-4 overflow-hidden p-4">
      <div class="relative min-w-0 flex-1 overflow-hidden rounded-lg border bg-black">
        <GokzViewer
          :key="viewerKey"
          ref="viewer"
          @replay-loaded="onReplayLoaded"
          @playing-changed="(value) => (isPlaying = value)"
        />
      </div>

      <div class="flex w-80 shrink-0 flex-col gap-4 overflow-hidden">
        <GokzReplayList
          v-model:filters="replayFilters"
          :replays="replays"
          :selected-id="selectedId"
          :loading="replaysPending"
          class="min-h-0"
          @select="loadFromList"
        />

        <Card>
          <CardHeader>
            <CardTitle class="text-sm font-medium text-muted-foreground">Load from URL</CardTitle>
          </CardHeader>
          <CardContent class="flex flex-col gap-2">
            <Label for="replay-url" class="sr-only">Replay URL</Label>
            <Input
              id="replay-url"
              v-model="customUrl"
              placeholder="https://example.com/my-run.replay"
              @keydown.enter="loadFromUrl"
            />
            <Button size="sm" @click="loadFromUrl">Load replay</Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle class="text-sm font-medium text-muted-foreground">Upload a replay</CardTitle>
          </CardHeader>
          <CardContent>
            <input
              type="file"
              accept=".replay"
              class="h-8 w-full rounded-lg border border-input bg-transparent px-2.5 py-1 text-sm outline-none file:mr-2 file:h-6 file:rounded-md file:border-0 file:bg-secondary file:px-2 file:text-xs file:font-medium file:text-secondary-foreground"
              @change="onFileChange"
            >
          </CardContent>
        </Card>
      </div>
    </div>
  </div>
</template>
