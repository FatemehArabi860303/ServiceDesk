import type { UserRole } from '../auth/authTypes'

export type UserResponse = {
  id: string
  firstName: string | null
  lastName: string | null
  email: string
  role: number
  isActive: boolean
  createdAt: string
  updatedAt: string | null
}

export function roleNumberToUserRole(role: number): UserRole {
  switch (role) {
    case 2:
      return 'Administrator'
    case 1:
      return 'Employee'
    default:
      return 'Customer'
  }
}
