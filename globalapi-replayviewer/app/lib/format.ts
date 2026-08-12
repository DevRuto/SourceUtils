export function formatReplayTime(seconds: number): string {
  const mins = Math.floor(seconds / 60)
  const secs = seconds - mins * 60
  const secsString = secs.toFixed(3).padStart(6, '0')
  return `${mins}:${secsString}`
}

const MODE_LABELS: Record<string, string> = {
  kz_timer: 'KZT',
  kz_simple: 'SKZ',
  kz_vanilla: 'VNL'
}

export function formatModeName(modeName: string): string {
  return MODE_LABELS[modeName] ?? modeName.replace(/^kz_/, '').toUpperCase()
}
