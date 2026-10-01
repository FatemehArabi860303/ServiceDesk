import { parseJwtPayload } from './jwtUtils'
import { test, expect } from 'vitest'

function base64UrlEncode(obj: any) {
  const json = JSON.stringify(obj)
  const encoder = new TextEncoder()
  const bytes = encoder.encode(json)
  let binary = ''
  for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i])
  let b64 = btoa(binary)
  return b64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

test('parses jwt payload with microsoft role claim', () => {
  const payload = { sub: '123', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Administrator' }
  const token = `hdr.${base64UrlEncode(payload)}.sig`
  const parsed = parseJwtPayload(token)
  expect(parsed).not.toBeNull()
  expect((parsed as any).sub).toBe('123')
  expect((parsed as any)[
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
  ]).toBe('Administrator')
})

test('returns null for malformed token part', () => {
  expect(parseJwtPayload('a.b')).toBeNull()
  expect(parseJwtPayload('a')).toBeNull()
})
