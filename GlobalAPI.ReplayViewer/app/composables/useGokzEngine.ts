declare global {
  interface Window {
    Gokz: any
    Facepunch: any
  }
}

const SCRIPT_URLS = [
  'https://cdnjs.cloudflare.com/ajax/libs/lz-string/1.4.4/lz-string.min.js',
  'https://cdnjs.cloudflare.com/ajax/libs/lz-string/1.4.4/base64-string.min.js',
  '/vendor/gokz/js/facepunch.webgame.js',
  '/vendor/gokz/js/sourceutils.js',
  '/vendor/gokz/js/replayviewer.js'
]

const STYLE_URLS = [
  '/vendor/gokz/styles/mapviewer.css',
  '/vendor/gokz/styles/replayviewer.css'
]

let enginePromise: Promise<typeof window.Gokz> | null = null

function loadScript(src: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const existing = document.querySelector<HTMLScriptElement>(`script[src="${src}"]`)
    if (existing) {
      if (existing.dataset.loaded === 'true') {
        resolve()
      } else {
        existing.addEventListener('load', () => resolve())
        existing.addEventListener('error', () => reject(new Error(`Failed to load script: ${src}`)))
      }
      return
    }

    const script = document.createElement('script')
    script.src = src
    script.async = false
    script.addEventListener('load', () => {
      script.dataset.loaded = 'true'
      resolve()
    })
    script.addEventListener('error', () => reject(new Error(`Failed to load script: ${src}`)))
    document.head.appendChild(script)
  })
}

function loadStylesheet(href: string): void {
  if (document.querySelector(`link[href="${href}"]`)) return
  const link = document.createElement('link')
  link.rel = 'stylesheet'
  link.href = href
  document.head.appendChild(link)
}

/**
 * Loads the vendored GOKZ replay viewer engine (facepunch.webgame.js /
 * sourceutils.js / replayviewer.js, plain globals, not ESM) exactly once and
 * resolves with the resulting `window.Gokz` namespace.
 */
export function useGokzEngine(): Promise<typeof window.Gokz> {
  if (!import.meta.client) {
    return Promise.reject(new Error('useGokzEngine can only be used on the client'))
  }

  if (!enginePromise) {
    STYLE_URLS.forEach(loadStylesheet)
    enginePromise = SCRIPT_URLS.reduce(
      (chain, src) => chain.then(() => loadScript(src)),
      Promise.resolve()
    ).then(() => window.Gokz)
  }

  return enginePromise
}
