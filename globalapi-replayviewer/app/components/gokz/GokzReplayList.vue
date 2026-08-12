<script setup lang="ts">
import { Card, CardContent, CardHeader, CardTitle } from '~/components/ui/card'
import { Input } from '~/components/ui/input'
import { ScrollArea } from '~/components/ui/scroll-area'
import type { GokzReplay } from '~/lib/gokzReplay'
import type { GokzReplayListFilters } from '~~/shared/types/gokzReplay'

defineProps<{
  replays: GokzReplay[]
  selectedId?: string | null
  loading?: boolean
}>()

const emit = defineEmits<{
  select: [replay: GokzReplay]
}>()

const filters = defineModel<GokzReplayListFilters>('filters', { default: () => ({}) })

const MODES = [
  { value: '', label: 'All modes' },
  { value: 'kz_timer', label: 'KZT' },
  { value: 'kz_simple', label: 'SKZ' },
  { value: 'kz_vanilla', label: 'VNL' }
]

function onProChange(event: Event) {
  const checked = (event.target as HTMLInputElement).checked
  filters.value.pro = checked ? true : undefined
}
</script>

<template>
  <Card class="flex flex-1 flex-col overflow-hidden">
    <CardHeader class="gap-3">
      <CardTitle class="text-sm font-medium text-muted-foreground">Recent replays</CardTitle>
      <div class="flex flex-col gap-2">
        <div class="flex gap-2">
          <Input v-model="filters.map" placeholder="Map" class="h-8 text-xs" />
          <Input v-model="filters.player" placeholder="Player" class="h-8 text-xs" />
        </div>
        <div class="flex items-center gap-2">
          <select
            v-model="filters.mode"
            class="dark:bg-input/30 border-input focus-visible:border-ring h-8 flex-1 rounded-lg border bg-transparent px-2 text-xs outline-none"
          >
            <option v-for="mode in MODES" :key="mode.value" :value="mode.value">{{ mode.label }}</option>
          </select>
          <label class="flex items-center gap-1.5 whitespace-nowrap text-xs text-muted-foreground">
            <input type="checkbox" :checked="filters.pro === true" @change="onProChange">
            Pro only
          </label>
        </div>
      </div>
    </CardHeader>
    <CardContent class="flex-1 overflow-hidden px-0">
      <p v-if="loading" class="px-6 text-sm text-muted-foreground">Loading&hellip;</p>
      <p v-else-if="!replays.length" class="px-6 text-sm text-muted-foreground">No replays found.</p>
      <ScrollArea v-else class="h-full px-6">
        <ul class="flex flex-col gap-1 pb-4">
          <li v-for="replay in replays" :key="replay.id">
            <button
              type="button"
              class="w-full rounded-md border px-3 py-2.5 text-left text-sm transition-colors hover:bg-accent hover:text-accent-foreground"
              :class="selectedId === replay.id ? 'border-primary bg-accent' : 'border-transparent'"
              @click="emit('select', replay)"
            >
              <div class="flex items-center justify-between gap-2 font-medium">
                <span>{{ replay.player }}</span>
                <span class="text-muted-foreground">{{ replay.time }}</span>
              </div>
              <div class="mt-0.5 flex items-center gap-2 text-xs text-muted-foreground">
                <span>{{ replay.map }}</span>
                <span>&middot;</span>
                <span>{{ replay.mode }}</span>
                <span v-if="replay.stage">&middot;</span>
                <span v-if="replay.stage">Stage {{ replay.stage }}</span>
                <span>&middot;</span>
                <span>{{ replay.pro ? 'PRO' : 'NUB' }}</span>
              </div>
            </button>
          </li>
        </ul>
      </ScrollArea>
    </CardContent>
  </Card>
</template>
