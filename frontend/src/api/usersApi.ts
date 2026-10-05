import type { UserResponse } from '../types/user'
import { get, post } from './apiClient'

export async function getUsers(token?: string): Promise<UserResponse[]> {
  return get<UserResponse[]>('/api/users', token)
}

export interface ProvisionUserAccessResponse {
  activationToken: string
  expiresAt: string
}

export type CreateUserRequest = {
  FirstName?: string | null
  LastName?: string | null
  Email?: string | null
  // Role must be numeric to match backend UserRole enum: 0=Customer,1=Employee,2=Administrator
  Role: number
}

export async function createUser(req: CreateUserRequest, token?: string): Promise<UserResponse> {
  return post<UserResponse>('/api/users', req, token)
}

export async function provisionUserAccess(userId: string, token?: string): Promise<ProvisionUserAccessResponse> {
  return post<ProvisionUserAccessResponse>(`/api/users/${userId}/access-provisioning`, null, token)
}
