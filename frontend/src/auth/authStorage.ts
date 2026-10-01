const KEY = 'sd_auth'

export type StoredAuth = {
  token: string
  expiresAt?: string
}

export const authStorage = {
  save(item: StoredAuth) {
    localStorage.setItem(KEY, JSON.stringify(item))
  },
  load(): StoredAuth | null {
    const raw = localStorage.getItem(KEY)
    if (!raw) return null
    try {
      return JSON.parse(raw) as StoredAuth
    } catch {
      return null
    }
  },
  clear() {
    localStorage.removeItem(KEY)
  }
}
