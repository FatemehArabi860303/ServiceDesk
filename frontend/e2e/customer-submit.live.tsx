import React from 'react'
import { render, screen, cleanup, fireEvent, configure } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, expect, test } from 'vitest'
import { AuthProvider } from '../src/auth/AuthContext'
import AppRoutes from '../src/routes/AppRoutes'

afterEach(() => { cleanup(); localStorage.clear() })

// A failing live-login assertion must never dump credential-bearing DOM to the console.
configure({ getElementError: () => new Error('Live UI expectation failed; credential-bearing DOM is intentionally omitted.') })

test('customer logs in and creates a real ticket through the WebApi', async () => {
  // Vitest exposes process-backed import.meta.env; non-VITE secrets stay test-only.
  const email = import.meta.env.SERVICEDESK_TEST_CUSTOMER_EMAIL
  const password = import.meta.env.SERVICEDESK_TEST_CUSTOMER_PASSWORD
  if (!email || !password || !import.meta.env.VITE_API_BASE_URL) {
    throw new Error('Supply an isolated API URL and development customer credentials through the environment.')
  }
  localStorage.clear()
  render(<MemoryRouter initialEntries={['/login']}><AuthProvider><AppRoutes /></AuthProvider></MemoryRouter>)
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: email } })
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: password } })
  await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
  await screen.findByRole('heading', { name: 'How can we help?' }, { timeout: 15000 })
  await userEvent.click(screen.getByRole('link', { name: /Submit a Request/ }))
  fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Customer UI authenticated acceptance' } })
  fireEvent.change(screen.getByLabelText('Description'), { target: { value: 'Synthetic request submitted through the React UI. No personal information.' } })
  fireEvent.change(screen.getByLabelText('Priority'), { target: { value: '1' } })
  await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))
  await screen.findByRole('heading', { name: 'Your request has been submitted' }, { timeout: 15000 })
  const identifier = screen.getByText(/^[0-9a-f]{8}-[0-9a-f-]{27}$/i)
  expect(identifier).toBeInTheDocument()
  // Synthetic ticket identifier only; never print the login/session or request payload.
  console.info(`Created ticket ID: ${identifier.textContent}`)
})
