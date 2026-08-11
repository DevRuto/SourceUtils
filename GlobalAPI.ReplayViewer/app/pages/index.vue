<script setup lang="ts">
import { ref, useTemplateRef } from 'vue'
import { Button } from '~/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '~/components/ui/card'
import { Input } from '~/components/ui/input'
import { Label } from '~/components/ui/label'
import { formatReplayTime } from '~/lib/format'
import type { GokzReplay } from '~/lib/gokzReplay'

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

const { replays, pending: replaysPending } = useGokzReplays()

function loadFromList(replay: GokzReplay) {
  selectedId.value = replay.id
  viewer.value?.loadReplay(replay.url)
}

function loadFromUrl() {
  if (!customUrl.value.trim()) return
  selectedId.value = null
  viewer.value?.loadReplay(customUrl.value.trim())
}

function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  selectedId.value = null
  viewer.value?.loadReplay(URL.createObjectURL(file))
  input.value = ''
}

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
          ref="viewer"
          @replay-loaded="onReplayLoaded"
          @playing-changed="(value) => (isPlaying = value)"
        />
      </div>

      <div class="flex w-80 shrink-0 flex-col gap-4 overflow-hidden">
        <GokzReplayList
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
