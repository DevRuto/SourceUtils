<script setup lang="ts">
import { Card, CardContent, CardHeader, CardTitle } from '~/components/ui/card'
import { ScrollArea } from '~/components/ui/scroll-area'
import type { MockReplay } from '~/lib/mockReplays'

defineProps<{
  replays: MockReplay[]
  selectedId?: string | null
}>()

const emit = defineEmits<{
  select: [replay: MockReplay]
}>()
</script>

<template>
  <Card class="flex flex-1 flex-col overflow-hidden">
    <CardHeader>
      <CardTitle class="text-sm font-medium text-muted-foreground">Example replays</CardTitle>
    </CardHeader>
    <CardContent class="flex-1 overflow-hidden px-0">
      <ScrollArea class="h-full px-6">
        <ul class="flex flex-col gap-1 pb-4">
          <li v-for="replay in replays" :key="replay.id">
            <button
              type="button"
              class="w-full rounded-md border px-3 py-2 text-left text-sm transition-colors hover:bg-accent hover:text-accent-foreground"
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
