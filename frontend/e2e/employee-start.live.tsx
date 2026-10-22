import React from 'react'
import { cleanup, configure, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, expect, test } from 'vitest'
import { AuthProvider } from '../src/auth/AuthContext'
import AppRoutes from '../src/routes/AppRoutes'
import { getEmployeeTicket } from '../src/api/employeeTicketsApi'

configure({ getElementError: () => new Error('Live employee UI expectation failed; credential-bearing DOM omitted.') })
afterEach(() => { cleanup(); localStorage.clear() })

test('employee logs in, reviews a synthetic request and atomically starts real work', async () => {
  const email = import.meta.env.SERVICEDESK_TEST_EMPLOYEE_EMAIL
  const password = import.meta.env.SERVICEDESK_TEST_EMPLOYEE_PASSWORD
  const ticketId = import.meta.env.SERVICEDESK_TEST_TICKET_ID
  if (!email || !password || !ticketId || !import.meta.env.VITE_API_BASE_URL)
    throw new Error('Supply an isolated API URL, employee credentials and synthetic ticket ID through process environment.')
  localStorage.clear()
  render(<MemoryRouter initialEntries={['/login']}><AuthProvider><AppRoutes /></AuthProvider></MemoryRouter>)
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: email } })
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: password } })
  await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
  await screen.findByRole('heading', { name: 'Support requests' }, { timeout: 15000 })
  // This opt-in check expects its owned ticket on the first page of an isolated database.
  await userEvent.click(await screen.findByRole('link', { name: 'Employee UI Test Request' }, { timeout: 15000 }))
  await screen.findByText(ticketId, {}, { timeout: 15000 })
  expect(screen.getByText(/AI recommendations are not available/)).toBeInTheDocument()
  await userEvent.click(await screen.findByRole('button', { name: 'Start Work' }))
  await screen.findByText('Work started successfully.', {}, { timeout: 15000 })
  await screen.findByText('In progress', {}, { timeout: 15000 })
  expect(screen.getByRole('button', { name: 'Start Work' })).toBeDisabled()
  const storedSession = JSON.parse(localStorage.getItem('sd_auth')!)
  const saved = await getEmployeeTicket(ticketId, storedSession.token)
  expect(saved.ticket.status).toBe(1)
  expect(saved.ticket.assignedEmployeeEmail?.toLowerCase()).toBe(email.toLowerCase())
  expect(saved.history.map(item => item.action)).toEqual([0, 1, 2])
  await userEvent.click(screen.getByRole('link', { name: 'Back to requests' }))
  await screen.findByText('In progress', {}, { timeout: 15000 })
})
