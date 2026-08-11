export interface MockReplay {
  id: string
  map: string
  player: string
  time: string
  mode: 'KZT' | 'SKZ' | 'VNL'
  pro: boolean
  url: string
}

/**
 * Placeholder replay list. Only one real .replay file ships with this repo
 * (kz_reach_v2.replay), so every entry below points at the same file under a
 * different display label - once loaded, the header shows the replay's real
 * embedded stats rather than these labels.
 */
export const mockReplays: MockReplay[] = [
  {
    id: 'kz_reach_v2-1',
    map: 'kz_reach_v2',
    player: 'Slumpfy',
    time: '1:38.516',
    mode: 'KZT',
    pro: true,
    url: '/replays/kz_reach_v2/example.replay'
  },
  {
    id: 'kz_reach_v2-2',
    map: 'kz_reach_v2',
    player: 'Slumpfy',
    time: '1:38.336',
    mode: 'SKZ',
    pro: false,
    url: '/replays/kz_reach_v2/example.replay'
  },
  {
    id: 'kz_reach_v2-3',
    map: 'kz_reach_v2',
    player: 'Slumpfy',
    time: '1:53.344',
    mode: 'SKZ',
    pro: true,
    url: '/replays/kz_reach_v2/example.replay'
  }
]
