import React from 'react'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../../auth/AuthContext'
import AppRoutes from '../../routes/AppRoutes'
import type { EmployeeTicket, TicketDetails } from '../../api/employeeTicketsApi'

const id = '167ba134-e417-4ae5-a561-5d17d8789522'
const fetchMock = vi.fn()
const ticket: EmployeeTicket = { id, title: 'Customer request', customerEmail: 'customer@example.invalid', assignedEmployeeUserId: null,
  assignedEmployeeEmail: null, status: 0, priority: 2, createdAt: '2026-10-22T10:00:00Z', updatedAt: '2026-10-22T10:00:00Z' }
const details: TicketDetails = { ticket, description: 'The synthetic description', canStartWork: true,
  history: [{ id: 'created', action: 0, actorUserId: 'customer', occurredAt: ticket.createdAt, assignedEmployeeUserId: null }] }
function jwt(role = 'Employee') {
  return `h.${btoa(JSON.stringify({ sub: 'employee', role, exp: Math.floor(Date.now() / 1000) + 3600 }))}.s`
}
function session(role = 'Employee') { localStorage.setItem('sd_auth', JSON.stringify({ token: jwt(role) })) }
function start(path = '/employee') {
  render(<MemoryRouter initialEntries={[path]}><AuthProvider><AppRoutes /></AuthProvider></MemoryRouter>)
}
function response(value: unknown, status = 200) { return new Response(status === 204 ? null : JSON.stringify(value), { status }) }
function page(items = [ticket], hasMore = false, number = 1) { return { items, page: number, pageSize: 20, hasMore } }
beforeEach(() => { localStorage.clear(); fetchMock.mockReset(); vi.stubGlobal('fetch', fetchMock) })
afterEach(() => { cleanup(); localStorage.clear(); vi.unstubAllGlobals() })

describe('employee login and protected workspace', () => {
  it('redirects anonymous access to login without requesting ticket data', async () => {
    start(`/employee/tickets/${id}`)
    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })
  it.each(['Customer', 'Administrator'])('denies employee workspace to %s', async role => {
    session(role); start()
    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Support requests' })).not.toBeInTheDocument())
    expect(fetchMock).not.toHaveBeenCalled()
  })
  it('logs in through existing authentication and lands on bounded employee list', async () => {
    fetchMock.mockResolvedValueOnce(response({ accessToken: jwt(), expiresAt: '2099-01-01T00:00:00Z' }))
      .mockResolvedValueOnce(response(page()))
    start('/login')
    await userEvent.type(screen.getByLabelText('Email'), 'employee@example.invalid')
    await userEvent.type(screen.getByLabelText('Password'), 'synthetic-test-only')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByRole('link', { name: 'Customer request' })).toBeInTheDocument()
    expect(fetchMock.mock.calls[0][0]).toMatch(/\/api\/auth\/login$/)
    expect(fetchMock.mock.calls[1][0]).toMatch(/\/api\/employee\/tickets\?page=1&pageSize=20$/)
  })
})

describe('ticket list and details', () => {
  beforeEach(() => session())
  it('shows customer, state, assignment and bounded pagination, then opens details', async () => {
    fetchMock.mockResolvedValueOnce(response(page([ticket], true)))
      .mockResolvedValueOnce(response(page([ticket], false, 2)))
      .mockResolvedValueOnce(response(details))
    start()
    expect(await screen.findByText('customer@example.invalid')).toBeInTheDocument()
    expect(screen.getByText('Open')).toBeInTheDocument()
    expect(screen.getByText('High')).toBeInTheDocument()
    expect(screen.getByText('Unassigned')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Next page' }))
    expect(await screen.findByText('Page 2')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByRole('button', { name: 'Next page' })).toBeDisabled())
    expect(fetchMock.mock.calls[1][0]).toMatch(/page=2&pageSize=20$/)
    await userEvent.click(screen.getByRole('link', { name: 'Customer request' }))
    expect(await screen.findByText('The synthetic description')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Request history' })).toBeInTheDocument()
    expect(screen.getByText('Created', { selector: 'strong' })).toBeInTheDocument()
    expect(screen.getByText(/AI recommendations are not available/)).toHaveTextContent('does not indicate whether analysis has been saved')
    expect(screen.queryByText(/analysis pending/i)).not.toBeInTheDocument()
  })
  it('shows loading, empty and retryable list states', async () => {
    let complete!: (value: Response) => void
    fetchMock.mockReturnValueOnce(new Promise<Response>(resolve => { complete = resolve }))
      .mockResolvedValueOnce(response(page()))
    start(); expect(screen.getByRole('status')).toHaveTextContent('Loading requests')
    complete(response(page([])))
    expect(await screen.findByText('No requests on this page.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Refresh requests' }))
    expect(await screen.findByRole('link', { name: 'Customer request' })).toBeInTheDocument()
  })
  it.each([403, 500])('safely handles list HTTP %s errors', async status => {
    fetchMock.mockResolvedValue(response({ detail: 'private stack trace' }, status)); start()
    expect(await screen.findByRole('alert')).not.toHaveTextContent('private stack trace')
    expect(screen.getByRole('button', { name: 'Refresh requests' })).toBeEnabled()
  })
  it('handles missing ticket without rendering mutation controls', async () => {
    fetchMock.mockResolvedValue(response({}, 404)); start(`/employee/tickets/${id}`)
    expect(await screen.findByRole('alert')).toHaveTextContent('could not be found')
    expect(screen.queryByRole('button', { name: 'Start Work' })).not.toBeInTheDocument()
  })
})

describe('atomic start work', () => {
  beforeEach(() => session())
  it('prevents duplicates, waits for API confirmation and refreshes details and list', async () => {
    let complete!: (value: Response) => void
    fetchMock.mockResolvedValueOnce(response(details))
      .mockReturnValueOnce(new Promise<Response>(resolve => { complete = resolve }))
      .mockResolvedValueOnce(response({ ...details, canStartWork: false,
        ticket: { ...ticket, status: 1, assignedEmployeeUserId: 'employee', assignedEmployeeEmail: 'employee@example.invalid' } }))
      .mockResolvedValueOnce(response(page([{ ...ticket, status: 1, assignedEmployeeUserId: 'employee', assignedEmployeeEmail: 'employee@example.invalid' }])))
    start(`/employee/tickets/${id}`)
    const button = await screen.findByRole('button', { name: 'Start Work' })
    fireEvent.click(button); fireEvent.click(button)
    expect(screen.getByRole('button', { name: 'Starting work…' })).toBeDisabled()
    expect(fetchMock).toHaveBeenCalledTimes(2)
    expect(screen.queryByText('Work started successfully.')).not.toBeInTheDocument()
    complete(response(null, 204))
    expect(await screen.findByText('In progress')).toBeInTheDocument()
    expect(screen.getByText('employee@example.invalid')).toBeInTheDocument()
    expect(screen.getByText('Work started successfully.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Start Work' })).toBeDisabled()
    expect(fetchMock.mock.calls[1][0]).toMatch(/\/api\/employee\/tickets\/.+\/start-work$/)
    expect(JSON.parse(fetchMock.mock.calls[1][1].body)).toEqual({})
    await userEvent.click(screen.getByRole('link', { name: 'Back to requests' }))
    expect(await screen.findByText('In progress')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledTimes(4)
  })
  it('uses server eligibility to disable an unavailable request', async () => {
    fetchMock.mockResolvedValue(response({ ...details, canStartWork: false, ticket: { ...ticket, assignedEmployeeUserId: 'another' } }))
    start(`/employee/tickets/${id}`)
    expect(await screen.findByRole('button', { name: 'Start Work' })).toBeDisabled()
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
  it('allows an own-assigned Open request when the server authorizes starting it', async () => {
    fetchMock.mockResolvedValue(response({ ...details, ticket: { ...ticket, assignedEmployeeUserId: 'employee', assignedEmployeeEmail: 'employee@example.invalid' } }))
    start(`/employee/tickets/${id}`)
    expect(await screen.findByRole('button', { name: 'Start Work' })).toBeEnabled()
    expect(screen.getByText('employee@example.invalid')).toBeInTheDocument()
  })
  it('refreshes after conflict without overwriting or claiming success', async () => {
    fetchMock.mockResolvedValueOnce(response(details)).mockResolvedValueOnce(response({ detail: 'private error' }, 409))
      .mockResolvedValueOnce(response({ ...details, canStartWork: false,
        ticket: { ...ticket, status: 1, assignedEmployeeUserId: 'other', assignedEmployeeEmail: 'other@example.invalid' } }))
    start(`/employee/tickets/${id}`)
    await userEvent.click(await screen.findByRole('button', { name: 'Start Work' }))
    expect(await screen.findByText('other@example.invalid')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Review its refreshed state')
    expect(screen.getByRole('alert')).not.toHaveTextContent('private error')
    expect(screen.queryByText('Work started successfully.')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Start Work' })).toBeDisabled()
  })
  it.each([401, 403, 404, 500, 'network'])('handles start-work %s safely', async status => {
    fetchMock.mockResolvedValueOnce(response(details))
    if (status === 'network') fetchMock.mockRejectedValueOnce(new TypeError('private error'))
    else fetchMock.mockResolvedValueOnce(response({ detail: 'private error' }, status as number))
    if (status === 403) fetchMock.mockResolvedValueOnce(response({ ...details, canStartWork: false }))
    start(`/employee/tickets/${id}`)
    await userEvent.click(await screen.findByRole('button', { name: 'Start Work' }))
    if (status === 401) {
      expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
      expect(localStorage.getItem('sd_auth')).toBeNull()
    } else {
      expect(await screen.findByRole('alert')).not.toHaveTextContent('private error')
    }
    expect(screen.queryByText('Work started successfully.')).not.toBeInTheDocument()
  })
})
