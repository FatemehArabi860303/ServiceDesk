import { post } from './apiClient'

export type LoginRequest = {
  Email: string
  Password: string
}

// Backend returns camelCase JSON
export type LoginResponse = {
  accessToken: string
  expiresAt: string
}

export async function login(req: LoginRequest): Promise<LoginResponse> {
  try {
    return await post<LoginResponse>('/api/auth/login', req)
  } catch (err: any) {
    // Map 401 to a user-friendly message
    if (err?.status === 401) {
      throw new Error('Invalid email or password.')
    }
    throw new Error(err?.message ?? 'Login failed.')
  }
}
