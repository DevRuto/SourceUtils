<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { useGokzEngine } from '~/composables/useGokzEngine'

const props = withDefaults(
  defineProps<{
    mapBaseUrl?: string
  }>(),
  {
    mapBaseUrl: '/maps'
  }
)

const emit = defineEmits<{
  'replay-loaded': [
    info: {
      mapName: string
      playerName: string
      time: number
      mode: number
      modeName: string
      teleportsUsed: number
      course: number
    }
  ]
  'playing-changed': [isPlaying: boolean]
}>()

const container = ref<HTMLElement | null>(null)
let viewer: any = null
let pendingUrl: string | null = null

onMounted(async () => {
  const Gokz = await useGokzEngine()
  if (!container.value) return

  viewer = new Gokz.ReplayViewer(container.value)
  viewer.mapBaseUrl = props.mapBaseUrl
  viewer.showMessage('Choose a replay from the list to get started.')

  viewer.replayLoaded.addListener((replay: any) => {
    // The engine only ever sets the message overlay text (never clears it),
    // so hide the "choose a replay" placeholder ourselves once one loads.
    if (viewer.messageElem) viewer.messageElem.style.display = 'none'

    console.log('[gokz] replay metadata', replay)

    emit('replay-loaded', {
      mapName: replay.mapName,
      playerName: replay.playerName,
      time: replay.time,
      mode: replay.mode,
      modeName: Gokz.GlobalMode[replay.mode],
      teleportsUsed: replay.teleportsUsed,
      course: replay.course
    })
  })

  viewer.isPlayingChanged.addListener((isPlaying: boolean) => {
    emit('playing-changed', isPlaying)
  })

  viewer.animate()

  if (pendingUrl) {
    loadReplay(pendingUrl)
    pendingUrl = null
  }
})

onUnmounted(() => {
  // The vendored engine's Map.unload() is unimplemented - it throws if you
  // load a second replay into the same viewer instance - so switching
  // replays works by remounting this whole component (see :key in the
  // parent page) rather than reusing one. But the engine also has no
  // dispose/cancelAnimationFrame path, so the outgoing instance's render
  // loop would otherwise keep running forever against a detached canvas.
  // Losing the GL context is what actually stops it (WebGL calls become
  // no-ops per spec once the context is lost) and frees it immediately
  // instead of waiting on GC - WebGL contexts are capped per tab (~16 in
  // Chrome), and remounting on every replay would exhaust that quickly
  // otherwise.
  viewer?.context?.getExtension('WEBGL_lose_context')?.loseContext()
})

function loadReplay(url: string) {
  if (!viewer) {
    pendingUrl = url
    return
  }
  viewer.isPlaying = true
  viewer.loadReplay(url)
}

defineExpose({ loadReplay })
</script>

<template>
  <div ref="container" class="map-viewer h-full w-full" />
</template>
