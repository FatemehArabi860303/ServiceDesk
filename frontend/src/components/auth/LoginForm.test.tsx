import React from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import LoginForm from './LoginForm'
import * as AuthContext from '../../auth/AuthContext'
import { MemoryRouter } from 'react-router-dom'

describe('LoginForm', () => {
  test('renders fields and button', () => {
    // provide a stubbed auth context
    vi.spyOn(AuthContext, 'useAuth').mockReturnValue({
      isAuthenticated: false,
      token: null,
      user: null,
      login: vi.fn(async () => {}),
      logout: vi.fn()
    } as any)

    render(
      <MemoryRouter>
        <LoginForm />
      </MemoryRouter>
    )

    expect(screen.getByLabelText(/email/i)).toBeInTheDocument()
    const pw = screen.getByLabelText(/password/i) as HTMLInputElement
    expect(pw).toBeInTheDocument()
    expect(pw.type).toBe('password')
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument()
  })

  test('shows validation and does not call api when empty', async () => {
    const loginMock = vi.fn()
    vi.spyOn(AuthContext, 'useAuth').mockReturnValue({
      isAuthenticated: false,
      token: null,
      user: null,
      login: loginMock,
      logout: vi.fn()
    } as any)

    render(
      <MemoryRouter>
        <LoginForm />
      </MemoryRouter>
    )
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/email is required/i)
    expect(loginMock).not.toHaveBeenCalled()
  })

  test('calls login on valid submission and disables inputs while loading', async () => {
    let resolve: () => void
    const p = new Promise<void>(r => (resolve = r))
    const loginMock = vi.fn(() => p)

    vi.spyOn(AuthContext, 'useAuth').mockReturnValue({
      isAuthenticated: false,
      token: null,
      user: null,
      login: loginMock,
      logout: vi.fn()
    } as any)

    render(
      <MemoryRouter>
        <LoginForm />
      </MemoryRouter>
    )
    await userEvent.type(screen.getByLabelText(/email/i), 'admin@example.com')
    await userEvent.type(screen.getByLabelText(/password/i), 'Password123')
    const btn = screen.getByRole('button', { name: /sign in/i })
    userEvent.click(btn)

    expect(loginMock).toHaveBeenCalledWith('admin@example.com', 'Password123')
    expect(btn).toBeDisabled()

    // resolve to avoid unhandled promise
    resolve!()
  })
})
