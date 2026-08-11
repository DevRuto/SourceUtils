// Proxies a single replay file download from the KZ Global API. Needed for
// the same reason as list.get.ts (no CORS on kztimerglobal.com) - the
// vendored replay viewer engine fetches replay URLs directly in-browser.
const UPSTREAM = 'https://kztimerglobal.com/api/v2.0/records/replay'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id || !/^\d+$/.test(id)) {
    throw createError({ statusCode: 400, statusMessage: 'Invalid replay id' })
  }

  const buffer = await $fetch<ArrayBuffer>(`${UPSTREAM}/${id}`, { responseType: 'arrayBuffer' })

  setResponseHeader(event, 'content-type', 'application/octet-stream')
  return Buffer.from(buffer)
})
