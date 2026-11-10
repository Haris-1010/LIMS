/**
 * Single HTTP client for the LIMS API.
 * - Same-origin requests with the httpOnly session cookie (never readable by JavaScript).
 * - Anti-forgery token kept in memory and sent as X-XSRF-TOKEN on every state-changing request.
 * - Errors become ApiError with the server's stable `code` (translated by the UI via errors.<code>).
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  /** Field validation errors from ASP.NET (field name → messages). */
  readonly fieldErrors: Record<string, string[]>
  /** Extra problem fields, e.g. lockedUntilUtc. */
  readonly data: Record<string, unknown>

  constructor(status: number, code: string, message: string, fieldErrors: Record<string, string[]> = {}, data: Record<string, unknown> = {}) {
    super(message)
    this.status = status
    this.code = code
    this.fieldErrors = fieldErrors
    this.data = data
  }
}

export const UNAUTHORIZED_EVENT = 'lims:unauthorized'

let csrfToken: string | null = null
let csrfRequest: Promise<string> | null = null

async function fetchCsrf(): Promise<string> {
  csrfRequest ??= fetch('/api/auth/csrf', { credentials: 'same-origin' })
    .then((r) => {
      if (!r.ok) throw new ApiError(r.status, 'network', 'Could not reach the server.')
      return r.json() as Promise<{ token: string }>
    })
    .then((d) => (csrfToken = d.token))
    .finally(() => (csrfRequest = null))
  return csrfRequest
}

/** Call after sign-in/out: anti-forgery tokens are bound to the signed-in user. */
export function resetCsrf() {
  csrfToken = null
}

type Query = Record<string, string | number | boolean | null | undefined>

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  query?: Query
  /** Don't broadcast 401 (used by the session probe itself). */
  silent401?: boolean
}

function buildUrl(path: string, query?: Query) {
  if (!query) return path
  const params = new URLSearchParams()
  for (const [k, v] of Object.entries(query)) if (v !== undefined && v !== null && v !== '') params.set(k, String(v))
  const qs = params.toString()
  return qs ? `${path}?${qs}` : path
}

export async function api<T = void>(path: string, opts: RequestOptions = {}, retried = false): Promise<T> {
  const method = opts.method ?? 'GET'
  const headers: Record<string, string> = { Accept: 'application/json' }
  // A FormData body (file upload) sets its own multipart content type.
  const isForm = typeof FormData !== 'undefined' && opts.body instanceof FormData
  if (opts.body !== undefined && !isForm) headers['Content-Type'] = 'application/json'
  if (method !== 'GET') headers['X-XSRF-TOKEN'] = csrfToken ?? (await fetchCsrf())

  let res: Response
  try {
    res = await fetch(buildUrl(path, opts.query), {
      method,
      headers,
      credentials: 'same-origin',
      body: opts.body === undefined ? undefined : isForm ? (opts.body as FormData) : JSON.stringify(opts.body),
    })
  } catch {
    throw new ApiError(0, 'network', 'Could not reach the server. Check the network connection.')
  }

  if (res.ok) {
    if (res.status === 204) return undefined as T
    const text = await res.text()
    return (text ? JSON.parse(text) : undefined) as T
  }

  let problem: { code?: string; title?: string; errors?: Record<string, string[]>; [k: string]: unknown } = {}
  try {
    problem = await res.json()
  } catch {
    /* non-JSON error body */
  }
  const code = problem.code ?? (problem.errors ? 'validation' : res.status === 403 ? 'forbidden' : res.status === 429 ? 'too_many_requests' : `http_${res.status}`)

  // Expired/rotated anti-forgery token: fetch a new one and retry once.
  if (code === 'csrf_invalid' && !retried) {
    resetCsrf()
    return api<T>(path, opts, true)
  }
  if (res.status === 401 && !opts.silent401) window.dispatchEvent(new Event(UNAUTHORIZED_EVENT))

  throw new ApiError(res.status, code, problem.title ?? res.statusText, problem.errors ?? {}, problem)
}
