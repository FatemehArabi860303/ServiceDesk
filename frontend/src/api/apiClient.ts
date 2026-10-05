export interface ApiError {
  status: number
  message: string
}

const apiBase = import.meta.env.VITE_API_BASE_URL ?? ''

export async function post<T>(path: string, body: unknown, token?: string): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (token) headers['Authorization'] = `Bearer ${token}`

  const res = await fetch(`${apiBase}${path}`, {
    method: 'POST',
    headers,
    body: JSON.stringify(body)
  })

  const text = await res.text()
  let data: any = undefined
  try {
    data = text ? JSON.parse(text) : undefined
  } catch {
    // ignore
  }

  if (!res.ok) {
    const message = data?.Message ?? data?.message ?? res.statusText
    const err = { status: res.status, message } as ApiError
    throw err
  }

  return data as T
}

export async function get<T>(path: string, token?: string): Promise<T> {
  const headers: Record<string, string> = {}
  if (token) headers['Authorization'] = `Bearer ${token}`

  const res = await fetch(`${apiBase}${path}`, { headers })
  const text = await res.text()
  let data: any = undefined
  try {
    data = text ? JSON.parse(text) : undefined
  } catch {
    // ignore
  }

  if (!res.ok) {
    const message = data?.Message ?? data?.message ?? res.statusText
    const err = { status: res.status, message } as ApiError
    throw err
  }

  return data as T
}
