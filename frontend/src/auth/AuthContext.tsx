import React, { createContext, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { login as loginApi } from '../api/authApi'
import { authStorage } from './authStorage'
import { parseJwtPayload } from '../utils/jwtUtils'
import type { AuthState, AuthenticatedUser } from './authTypes'

type AuthContextValue = AuthState & {
  login(email: string, password: string): Promise<void>
  logout(): void
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({
    isAuthenticated: false,
    token: null,
    user: null
  })

  useEffect(() => {
    const stored = authStorage.load()
    if (stored?.token) {
      const claims = parseJwtPayload(stored.token)
      const user = claims ? { id: claims.sub, role: claims.role as any } : null
      setState({ isAuthenticated: !!user, token: stored.token, user })
    }
  }, [])

  async function login(email: string, password: string) {
    const resp = await loginApi({ Email: email, Password: password })
    const token = resp.accessToken
    const claims = parseJwtPayload(token)
    if (!claims) throw new Error('Failed to parse token.')

    const roleClaim =
      (claims as any).role ?? (claims as any)['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']

    if (typeof roleClaim !== 'string') throw new Error('Token does not contain role claim.')

    // Validate role against known roles
    const role = (['Administrator', 'Employee', 'Customer'] as const).includes(roleClaim as any)
      ? (roleClaim as any)
      : null

    if (!role) throw new Error('Unauthorized role.')

    const user: AuthenticatedUser = { id: String((claims as any).sub), role }
    authStorage.save({ token, expiresAt: resp.expiresAt })
    setState({ isAuthenticated: true, token, user })
  }

  function logout() {
    authStorage.clear()
    setState({ isAuthenticated: false, token: null, user: null })
  }

  const value: AuthContextValue = {
    ...state,
    login,
    logout
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
