export function formatReplayTime(seconds: number): string {
  const mins = Math.floor(seconds / 60)
  const secs = seconds - mins * 60
  const secsString = secs.toFixed(3).padStart(6, '0')
  return `${mins}:${secsString}`
}
