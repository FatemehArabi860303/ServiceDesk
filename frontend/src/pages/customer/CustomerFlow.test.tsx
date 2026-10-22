import React from 'react'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../../auth/AuthContext'
import AppRoutes from '../../routes/AppRoutes'

const id = '86b7a134-e417-4ae5-a561-5d17d8789522'
const fetchMock = vi.fn()
function token(role = 'Customer', longClaim = false) {
  return `h.${btoa(JSON.stringify({ sub: 'customer-a', [longClaim ? 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role' : 'role']: role, exp: Math.floor(Date.now() / 1000) + 3600 }))}.s`
}
function session(role = 'Customer', expired = false, longClaim = false) {
  localStorage.setItem('sd_auth', JSON.stringify({ token: token(role, longClaim), expiresAt: new Date(Date.now() + (expired ? -3600000 : 3600000)).toISOString() }))
}
function start(path = '/customer/requests/new') {
  render(<MemoryRouter initialEntries={[path]}><AuthProvider><AppRoutes /></AuthProvider></MemoryRouter>)
}
function response(data: unknown, status = 200) {
  return new Response(JSON.stringify(data), { status })
}
async function fill() {
  await screen.findByRole('heading', { name: 'Submit a Request' })
  fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Printer offline' } })
  fireEvent.change(screen.getByLabelText('Description'), { target: { value: 'The office printer cannot be reached.' } })
  fireEvent.change(screen.getByLabelText('Priority'), { target: { value: '1' } })
}
beforeEach(() => { localStorage.clear(); vi.stubGlobal('fetch', fetchMock); fetchMock.mockReset() })
afterEach(() => { cleanup(); localStorage.clear(); vi.unstubAllGlobals() })

describe('customer authentication and navigation', () => {
  it('redirects anonymous visitors to login', async () => {
    start()
    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Title')).not.toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })
  it('logs in through the existing endpoint and opens the customer dashboard', async () => {
    fetchMock.mockResolvedValue(response({ accessToken: token(), expiresAt: new Date(Date.now() + 3600000).toISOString() }))
    start('/login')
    await userEvent.type(screen.getByLabelText('Email'), 'customer@example.invalid')
    await userEvent.type(screen.getByLabelText('Password'), 'synthetic-test-only')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByRole('heading', { name: 'How can we help?' })).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/api\/auth\/login$/), expect.objectContaining({ method: 'POST' }))
    await userEvent.click(screen.getByRole('link', { name: /Submit a Request/ }))
    expect(await screen.findByLabelText('Title')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('link', { name: 'Cancel' }))
    expect(await screen.findByRole('heading', { name: 'How can we help?' })).toBeInTheDocument()
  })
  it('restores a customer session with the backend URI role claim and supports sign out', async () => {
    session('Customer', false, true); start('/customer')
    expect(await screen.findByRole('heading', { name: 'How can we help?' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }))
    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(localStorage.getItem('sd_auth')).toBeNull()
  })
  it('returns a customer to the protected form originally requested after login', async () => {
    fetchMock.mockResolvedValue(response({ accessToken: token(), expiresAt: new Date(Date.now() + 3600000).toISOString() }))
    start('/customer/requests/new')
    await userEvent.type(await screen.findByLabelText('Email'), 'customer@example.invalid')
    await userEvent.type(screen.getByLabelText('Password'), 'synthetic-test-only')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByLabelText('Title')).toBeInTheDocument()
  })
  it.each(['Administrator', 'Employee'])('denies the customer form to %s', async role => {
    fetchMock.mockResolvedValue(response({ items: [], page: 1, pageSize: 20, hasMore: false }))
    session(role); start()
    await waitFor(() => expect(screen.queryByLabelText('Title')).not.toBeInTheDocument())
    expect(fetchMock.mock.calls.every(([, options]) => options?.method !== 'POST')).toBe(true)
  })
  it('rejects an expired stored session', async () => {
    session('Customer', true); start()
    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(localStorage.getItem('sd_auth')).toBeNull()
  })
  it.each([401, 500])('shows safe login failure for HTTP %s', async status => {
    fetchMock.mockResolvedValue(response({ message: 'private diagnostic and credentials' }, status)); start('/login')
    await userEvent.type(screen.getByLabelText('Email'), 'customer@example.invalid')
    await userEvent.type(screen.getByLabelText('Password'), 'synthetic-test-only')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByRole('alert')).not.toHaveTextContent('private diagnostic')
    expect(screen.queryByRole('heading', { name: 'How can we help?' })).not.toBeInTheDocument()
  })
})

describe('customer request submission', () => {
  beforeEach(() => session())
  it('requires title, description and the API priority field, focusing the first error', async () => {
    start(); await screen.findByLabelText('Title')
    await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))
    expect(screen.getByText('Enter a title for your request.')).toBeInTheDocument()
    expect(screen.getByText('Describe what you need help with.')).toBeInTheDocument()
    expect(screen.getByText('Choose a priority.')).toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toHaveFocus()
    expect(fetchMock).not.toHaveBeenCalled()
  })
  it.each([['Title', 200], ['Description', 5000]] as const)('validates the %s maximum and applies the native limit', async (label, limit) => {
    start(); await fill()
    const field = screen.getByLabelText(label)
    expect(field).toHaveAttribute('maxlength', String(limit))
    fireEvent.change(field, { target: { value: 'x'.repeat(limit + 1) } })
    await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))
    expect(screen.getByRole('alert')).toHaveTextContent(/characters or fewer/)
    expect(fetchMock).not.toHaveBeenCalled()
  })
  it('accepts maximum lengths and sends only the existing DTO fields, then confirms the returned ID', async () => {
    fetchMock.mockResolvedValue(response({ id }, 201)); start(); await fill()
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'x'.repeat(200) } })
    fireEvent.change(screen.getByLabelText('Description'), { target: { value: 'y'.repeat(5000) } })
    await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))
    expect(await screen.findByText(id)).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Your request has been submitted' })).toHaveFocus()
    expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/api\/tickets$/), {
      method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${JSON.parse(localStorage.getItem('sd_auth')!).token}` },
      body: JSON.stringify({ title: 'x'.repeat(200), description: 'y'.repeat(5000), priority: 1 })
    })
    await userEvent.click(screen.getByRole('button', { name: 'Create another request' }))
    expect(screen.getByLabelText('Title')).toHaveValue('')
    expect(screen.getByLabelText('Description')).toHaveValue('')
  })
  it('prevents duplicate submits and does not confirm before the API returns', async () => {
    let complete!: (value: Response) => void
    fetchMock.mockReturnValue(new Promise<Response>(resolve => { complete = resolve }))
    start(); await fill()
    const form = screen.getByLabelText('Title').closest('form')!
    fireEvent.submit(form); fireEvent.submit(form)
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(screen.getByRole('button', { name: 'Submitting…' })).toBeDisabled()
    expect(screen.getByLabelText('Title')).toBeDisabled()
    expect(screen.queryByText(id)).not.toBeInTheDocument()
    complete(response({ id }, 201))
    expect(await screen.findByText(id)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('link', { name: 'Return to customer page' }))
    expect(await screen.findByRole('heading', { name: 'How can we help?' })).toBeInTheDocument()
  })
  it.each([400, 403, 500])('handles HTTP %s without showing server details and retains input', async status => {
    fetchMock.mockResolvedValue(response({ message: 'private diagnostic', detail: 'stack trace' }, status))
    start(); await fill(); await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))
    const alert = await screen.findByRole('alert')
    expect(alert).not.toHaveTextContent(/private diagnostic|stack trace/)
    expect(screen.getByLabelText('Title')).toHaveValue('Printer offline')
    expect(screen.queryByText(id)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Submit request' })).toBeEnabled()
  })
  it('returns to login on an expired/revoked session', async () => {
    fetchMock.mockResolvedValue(response({}, 401)); start(); await fill()
    await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))
    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(localStorage.getItem('sd_auth')).toBeNull()
    expect(screen.getByRole('status')).toHaveTextContent('Your session has expired.')
  })
  it('handles a network failure safely without claiming success', async () => {
    fetchMock.mockRejectedValue(new TypeError('private network diagnostic')); start(); await fill()
    await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('We could not connect to ServiceDesk.')
    expect(screen.getByRole('alert')).not.toHaveTextContent('private network diagnostic')
    expect(screen.queryByText(id)).not.toBeInTheDocument()
  })
  it('does not claim success if the server omits the ticket ID', async () => {
    fetchMock.mockResolvedValue(response({}, 201)); start(); await fill()
    await userEvent.click(screen.getByRole('button', { name: 'Submit request' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('We could not confirm your request.')
  })
})
