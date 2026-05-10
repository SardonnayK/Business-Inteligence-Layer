const BASE_URL = import.meta.env.VITE_API_URL ?? ''

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

function getStoredToken(): string | null {
  try {
    const raw = localStorage.getItem('bi_auth')
    if (!raw) return null
    return JSON.parse(raw).token ?? null
  } catch {
    return null
  }
}

export async function apiFetch<T>(
  path: string,
  options: RequestInit & { tenantId?: string } = {},
): Promise<T> {
  const { tenantId, ...fetchOptions } = options

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(fetchOptions.headers as Record<string, string> | undefined),
  }

  if (tenantId) headers['X-Tenant-Id'] = tenantId

  const token = getStoredToken()
  if (token) headers['Authorization'] = `Bearer ${token}`

  const response = await fetch(`${BASE_URL}${path}`, {
    ...fetchOptions,
    headers,
  })

  if (response.status === 401) {
    localStorage.removeItem('bi_auth')
    window.location.href = '/login'
    throw new ApiError(401, 'Session expired. Please log in again.')
  }

  if (!response.ok) {
    const text = await response.text().catch(() => response.statusText)
    throw new ApiError(response.status, text || `HTTP ${response.status}`)
  }

  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('application/json') && response.status === 204) {
    return undefined as unknown as T
  }

  return response.json() as Promise<T>
}
