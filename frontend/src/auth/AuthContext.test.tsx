import React from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import { describe, vi, test, expect } from 'vitest'
import { AuthProvider, useAuth } from './AuthContext'
import * as authApi from '../api/authApi'

function Consumer() {
  const auth = useAuth()
  return (
    <div>
      <div data-testid="auth">{String(auth.isAuthenticated)}</div>
      <div data-testid="role">{auth.user?.role ?? ''}</div>
    </div>
  )
}

function Trigger() {
  const auth = useAuth()
  React.useEffect(() => {
    auth.login('a', 'b').catch(() => {})
  }, [])
  return null
}

test('login updates auth state for administrator', async () => {
  const tokenPayload = { sub: '42', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Administrator' }
  const b64 = (obj: any) => {
    const s = JSON.stringify(obj)
    let binary = ''
    for (let i = 0; i < s.length; i++) binary += String.fromCharCode(s.charCodeAt(i))
    const b = btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
    return b
  }

  const fakeToken = `h.${b64(tokenPayload)}.s`
  vi.spyOn(authApi, 'login').mockResolvedValue({ accessToken: fakeToken, expiresAt: new Date().toISOString() } as any)

  render(
    <AuthProvider>
      <Trigger />
      <Consumer />
    </AuthProvider>
  )

  await waitFor(() => {
    expect(screen.getByTestId('auth').textContent).toBe('true')
    expect(screen.getByTestId('role').textContent).toBe('Administrator')
  })
})
