import type { UserResponse } from '../types/user'
import { get, post } from './apiClient'

export async function getUsers(token?: string): Promise<UserResponse[]> {
  return get<UserResponse[]>('/api/users', token)
}

export type CreateUserRequest = {
  FirstName?: string | null
  LastName?: string | null
  Email?: string | null
  Role: 'Administrator' | 'Employee' | 'Customer'
}

export async function createUser(req: CreateUserRequest, token?: string): Promise<UserResponse> {
  return post<UserResponse>('/api/users', req, token)
}
