import React from 'react'
import { render, screen, waitFor, cleanup } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import * as usersApi from '../../../api/usersApi'
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'

vi.mock('../../../api/usersApi')

// Mock the auth context so ProtectedRoute sees an authenticated Administrator
vi.mock('../../../auth/AuthContext', () => ({
  useAuth: () => ({
    isAuthenticated: true,
    token: 'fake-token',
    user: { id: '42', role: 'Administrator' },
    login: async () => {},
    logout: () => {}
  }),
  AuthProvider: ({ children }: any) => children
}))

import AppRoutes from '../../../routes/AppRoutes'

const mockUsers = [
  { id: '1', firstName: 'Alice', lastName: 'Admin', email: 'alice@example.com', role: 2, isActive: true, createdAt: '', updatedAt: null },
  { id: '2', firstName: 'Bob', lastName: 'Builder', email: 'bob@example.com', role: 1, isActive: false, createdAt: '', updatedAt: null }
]

function renderWithAuth(ui: React.ReactElement) {
  return render(ui)
}

describe('Admin Users pages', () => {
  beforeEach(() => {
    // Seed authStorage with an Administrator token so ProtectedRoute allows access
    const payload = { sub: '42', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Administrator' }
    const b64 = (obj: any) => {
      const s = JSON.stringify(obj)
      let binary = ''
      for (let i = 0; i < s.length; i++) binary += String.fromCharCode(s.charCodeAt(i))
      const b = btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
      return b
    }
    const fakeToken = `h.${b64(payload)}.s`
    localStorage.setItem('sd_auth', JSON.stringify({ token: fakeToken, expiresAt: new Date(Date.now() + 1000 * 60 * 60).toISOString() }))

    const mocked = vi.mocked(usersApi.getUsers)
    mocked.mockResolvedValue(mockUsers)

    // Mock provisionUserAccess for users list provisioning
    const mockedProv = vi.mocked((usersApi as any).provisionUserAccess)
    mockedProv.mockResolvedValue({ activationToken: 'token-abc', expiresAt: new Date(Date.now() + 1000 * 60 * 60).toISOString() } as any)
  })

  afterEach(() => {
    cleanup()
    vi.clearAllMocks()
    // Also clear authStorage to ensure ProtectedRoute uses a fresh state
    localStorage.clear()
  })

  it('renders users after loading', async () => {
    renderWithAuth(
      <MemoryRouter initialEntries={["/admin/users"]}>
        <AppRoutes />
      </MemoryRouter>
    )

    expect(screen.getByText(/Loading users/i)).toBeInTheDocument()

    await waitFor(() => expect(screen.getByText('Alice')).toBeInTheDocument())
    expect(screen.getByText('Bob')).toBeInTheDocument()
  })

  it('navigates to create page when Add User clicked', async () => {
    renderWithAuth(
      <MemoryRouter initialEntries={["/admin/users"]}>
        <AppRoutes />
      </MemoryRouter>
    )

    await waitFor(() => expect(screen.getByText('Alice')).toBeInTheDocument())

    const add = screen.getByText(/Add User/i)
    await userEvent.click(add)

    // Create page appears
    expect(screen.getByText(/Create User/i)).toBeInTheDocument()

    // Now navigate back and provision from users list
    // Simulate navigating back to users
    // Render users list again to test provision action
    render(
      <MemoryRouter initialEntries={["/admin/users"]}>
        <AppRoutes />
      </MemoryRouter>
    )

    await waitFor(() => expect(screen.getByText('Alice')).toBeInTheDocument())
    const provision = screen.getAllByText(/Provision Access/i)[0]
    await userEvent.click(provision)
    // Provision modal should display token
    await waitFor(() => expect(screen.getByText(/User Access Provisioned/i)).toBeInTheDocument())
  })
})
