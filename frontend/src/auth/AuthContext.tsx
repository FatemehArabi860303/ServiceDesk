import React, { createContext, useContext, useState } from 'react'
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

function userFromToken(token: string): AuthenticatedUser | null {
  const claims = parseJwtPayload(token)
  if (!claims || typeof claims.sub !== 'string' || !claims.sub) return null
  if (typeof claims.exp === 'number' && claims.exp * 1000 <= Date.now()) return null
  const role = claims.role ?? claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
  if (role !== 'Administrator' && role !== 'Employee' && role !== 'Customer') return null
  return { id: claims.sub, role }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>(() => {
    const stored = authStorage.load()
    if (stored?.token) {
      const expired = stored.expiresAt !== undefined &&
        (!Number.isFinite(Date.parse(stored.expiresAt)) || Date.parse(stored.expiresAt) <= Date.now())
      const user = expired ? null : userFromToken(stored.token)
      if (user) return { isAuthenticated: true, token: stored.token, user }
      else authStorage.clear()
    }
    return { isAuthenticated: false, token: null, user: null }
  })

  async function login(email: string, password: string) {
    const resp = await loginApi({ Email: email, Password: password })
    const token = resp.accessToken
    const user = userFromToken(token)
    if (!user) throw new Error('Unable to sign in. Please try again.')
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
