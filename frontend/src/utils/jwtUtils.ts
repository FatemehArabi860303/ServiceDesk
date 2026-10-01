export function parseJwtPayload(token: string): Record<string, unknown> | null {
  try {
    const parts = token.split('.')
    if (parts.length < 2) return null
    let payload = parts[1]

    // Base64URL -> Base64
    payload = payload.replace(/-/g, '+').replace(/_/g, '/')

    // Add padding
    const pad = payload.length % 4
    if (pad === 2) payload += '=='
    else if (pad === 3) payload += '='
    else if (pad === 1) return null

    // atob gives a binary string; decode to UTF-8
    const binary = atob(payload)
    const bytes = new Uint8Array(binary.length)
    for (let i = 0; i < binary.length; i++) {
      bytes[i] = binary.charCodeAt(i)
    }
    const decoded = new TextDecoder().decode(bytes)
    return JSON.parse(decoded) as Record<string, unknown>
  } catch {
    return null
  }
}
