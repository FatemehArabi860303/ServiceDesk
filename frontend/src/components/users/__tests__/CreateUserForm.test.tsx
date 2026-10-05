import React from 'react'
import { render, screen, waitFor, cleanup } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import * as usersApi from '../../../api/usersApi'
import { AuthProvider } from '../../../auth/AuthContext'
import CreateUserForm from '../CreateUserForm'
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'

vi.mock('../../../api/usersApi')

describe('CreateUserForm', () => {
  beforeEach(() => {
    const mocked = vi.mocked(usersApi.createUser)
    mocked.mockResolvedValue({ id: '3', firstName: 'New', lastName: 'User', email: 'new@example.com', role: 1, isActive: true, createdAt: '', updatedAt: null } as any)
  })

  afterEach(() => {
    cleanup()
    vi.clearAllMocks()
  })

  it('submits create user request and calls onCreated', async () => {
    const onCreated = vi.fn()
    const createdIdCapture: string[] = []
    render(
      <AuthProvider>
        <MemoryRouter>
          <CreateUserForm onCreated={(id) => createdIdCapture.push(id)} />
        </MemoryRouter>
      </AuthProvider>
    )

    await userEvent.type(screen.getByLabelText(/Email/i), 'new@example.com')
    await userEvent.selectOptions(screen.getByLabelText(/Role/i), '1')

    await userEvent.click(screen.getByRole('button', { name: /Create/i }))

    await waitFor(() => expect(createdIdCapture.length).toBe(1))

    expect(usersApi.createUser).toHaveBeenCalledWith(expect.objectContaining({ Email: 'new@example.com', Role: 1 }), undefined)
  })
})
