import React, { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { createTicket } from '../../api/ticketsApi'
import type { TicketPriority } from '../../api/ticketsApi'

type Fields = { title: string; description: string; priority: string }
type Errors = Partial<Record<keyof Fields, string>>
const empty: Fields = { title: '', description: '', priority: '' }

function validate(fields: Fields): Errors {
  const errors: Errors = {}
  if (!fields.title.trim()) errors.title = 'Enter a title for your request.'
  else if (fields.title.length > 200) errors.title = 'Use 200 characters or fewer for the title.'
  if (!fields.description.trim()) errors.description = 'Describe what you need help with.'
  else if (fields.description.length > 5000) errors.description = 'Use 5,000 characters or fewer for the description.'
  if (!['0', '1', '2', '3'].includes(fields.priority)) errors.priority = 'Choose a priority.'
  return errors
}

export default function SubmitRequestPage() {
  const { token, logout } = useAuth()
  const navigate = useNavigate()
  const [fields, setFields] = useState<Fields>(empty)
  const [errors, setErrors] = useState<Errors>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [ticketId, setTicketId] = useState<string | null>(null)
  const inFlight = useRef(false)
  const heading = useRef<HTMLHeadingElement>(null)
  useEffect(() => { heading.current?.focus() }, [ticketId])

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (inFlight.current) return
    setFailure(null)
    const nextErrors = validate(fields)
    setErrors(nextErrors)
    const firstError = Object.keys(nextErrors)[0]
    if (firstError) { document.getElementById(`request-${firstError}`)?.focus(); return }
    if (!token) { logout(); return }
    inFlight.current = true
    setBusy(true)
    try {
      const ticket = await createTicket({ ...fields, priority: Number(fields.priority) as TicketPriority }, token)
      setTicketId(ticket.id)
    } catch (error: unknown) {
      const status = (error as { status?: number })?.status
      if (status === 401) {
        logout()
        navigate('/login', { replace: true, state: { sessionExpired: true } })
        return
      }
      if (status === 403) setFailure('You do not have permission to submit this request. Sign in with an active customer account.')
      else if (status === 400) setFailure('The request could not be accepted. Check the title, description and priority, then try again.')
      else if (error instanceof TypeError) setFailure('We could not connect to ServiceDesk. Your submission may not have been confirmed. Check your connection before trying again.')
      else setFailure('We could not confirm your request. Please try again later. If you already submitted it, contact support before sending it again.')
    } finally {
      inFlight.current = false
      setBusy(false)
    }
  }

  function update(name: keyof Fields, value: string) {
    setFields(current => ({ ...current, [name]: value }))
    setErrors(current => ({ ...current, [name]: undefined }))
  }

  if (ticketId) return (
    <section className="customer-card confirmation" aria-labelledby="confirmation-heading">
      <div className="success-symbol" aria-hidden="true">✓</div>
      <p className="eyebrow">Request received</p>
      <h1 ref={heading} tabIndex={-1} id="confirmation-heading">Your request has been submitted</h1>
      <p role="status">Your support request was created successfully.</p>
      <div className="ticket-reference"><span>Ticket ID</span><strong>{ticketId}</strong></div>
      <div className="customer-actions">
        <button onClick={() => { setFields(empty); setErrors({}); setTicketId(null) }}>Create another request</button>
        <Link to="/customer">Return to customer page</Link>
      </div>
    </section>
  )

  return (
    <section className="customer-card" aria-labelledby="request-heading">
      <p className="eyebrow">Customer support</p>
      <h1 ref={heading} tabIndex={-1} id="request-heading">Submit a Request</h1>
      <p className="form-intro">Tell us what’s happening so our support team can help. All fields are required.</p>
      <form noValidate onSubmit={submit} aria-busy={busy}>
        <fieldset disabled={busy} className="request-fields">
          <legend className="visually-hidden">Request details</legend>
          <div className="field">
            <label htmlFor="request-title">Title</label>
            <input id="request-title" name="title" required maxLength={200} value={fields.title}
              aria-invalid={!!errors.title} aria-describedby={`title-hint${errors.title ? ' title-error' : ''}`}
              onChange={event => update('title', event.target.value)} />
            <span className="field-hint" id="title-hint">A short summary · {fields.title.length}/200 characters</span>
            {errors.title && <span className="field-error" id="title-error" role="alert">{errors.title}</span>}
          </div>
          <div className="field">
            <label htmlFor="request-description">Description</label>
            <textarea id="request-description" name="description" required maxLength={5000} rows={7} value={fields.description}
              aria-invalid={!!errors.description} aria-describedby={`description-hint${errors.description ? ' description-error' : ''}`}
              onChange={event => update('description', event.target.value)} />
            <span className="field-hint" id="description-hint">Include what happened and what you expected · {fields.description.length.toLocaleString()}/5,000 characters</span>
            {errors.description && <span className="field-error" id="description-error" role="alert">{errors.description}</span>}
          </div>
          <div className="field priority-field">
            <label htmlFor="request-priority">Priority</label>
            <select id="request-priority" name="priority" required value={fields.priority} aria-invalid={!!errors.priority}
              aria-describedby={errors.priority ? 'priority-error' : undefined} onChange={event => update('priority', event.target.value)}>
              <option value="">Choose a priority</option><option value="0">Low</option><option value="1">Medium</option>
              <option value="2">High</option><option value="3">Critical</option>
            </select>
            {errors.priority && <span className="field-error" id="priority-error" role="alert">{errors.priority}</span>}
          </div>
        </fieldset>
        {failure && <p className="error" role="alert">{failure}</p>}
        <div className="customer-actions">
          <button type="submit" disabled={busy}>{busy ? 'Submitting…' : 'Submit request'}</button>
          {busy ? <span role="status">Sending your request. Please wait.</span> : <Link to="/customer">Cancel</Link>}
        </div>
      </form>
    </section>
  )
}
