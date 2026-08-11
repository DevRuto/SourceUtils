import { promises as fs } from 'node:fs'
import { extname, join, normalize, sep } from 'node:path'

// Serves the SourceUtils.MapExport output (../output/maps, ../output/materials)
// directly, so re-running export-pages-kz.bat is immediately reflected with no
// copying. Not handled via nitro.publicAssets because JSON under these prefixes
// needs the transform below.
//
// SourceUtils.MapExport.Core wraps every resource path as `{"$url": "..."}`
// (see Url.cs/UrlConverter) so a modern client can resolve it against a
// deployment's urlPrefix at runtime. The vendored GOKZReplayViewer engine
// (public/vendor/gokz) predates that convention and expects plain string URL
// fields - without unwrapping, it string-concatenates the whole object into
// request URLs like "/[object Object]". So JSON served here has `$url`
// objects unwrapped back to plain strings before being sent.
const OUTPUT_DIR = join(process.cwd(), '..', 'output')

const CONTENT_TYPES: Record<string, string> = {
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.html': 'text/html; charset=utf-8'
}

function unwrapUrls(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(unwrapUrls)

  if (value && typeof value === 'object') {
    const keys = Object.keys(value as Record<string, unknown>)
    if (keys.length === 1 && keys[0] === '$url') {
      return (value as Record<string, unknown>).$url
    }

    const out: Record<string, unknown> = {}
    for (const key of keys) out[key] = unwrapUrls((value as Record<string, unknown>)[key])
    return out
  }

  return value
}

export default defineEventHandler(async (event) => {
  const path = event.path.split('?')[0]
  if (!path.startsWith('/maps/') && !path.startsWith('/materials/')) return

  const filePath = normalize(join(OUTPUT_DIR, decodeURIComponent(path)))
  if (!(filePath + sep).startsWith(OUTPUT_DIR + sep)) return

  let raw: Buffer
  try {
    raw = await fs.readFile(filePath)
  } catch {
    return
  }

  const contentType = CONTENT_TYPES[extname(path)] ?? 'application/octet-stream'
  setResponseHeader(event, 'content-type', contentType)

  if (path.endsWith('.json')) {
    const text = raw.toString('utf-8').replace(/^﻿/, '')
    return unwrapUrls(JSON.parse(text))
  }

  return raw
})
