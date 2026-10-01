export type UserRole = 'Administrator' | 'Employee' | 'Customer'

export interface AuthenticatedUser {
  id: string
  role: UserRole
}

export interface AuthState {
  isAuthenticated: boolean
  token: string | null
  user: AuthenticatedUser | null
}
