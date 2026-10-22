import React, { useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { getEmployeeTicket, priorityLabel, startEmployeeWork, statusLabel } from '../../api/employeeTicketsApi'
import type { TicketDetails } from '../../api/employeeTicketsApi'
import useEmployeeError from './useEmployeeError'

export default function EmployeeTicketPage() {
  const { ticketId = '' } = useParams()
  const { token } = useAuth()
  const explain = useEmployeeError()
  const [data, setData] = useState<TicketDetails | null>(null)
  const [loading, setLoading] = useState(true)
  const [revision, setRevision] = useState(0)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [confirmation, setConfirmation] = useState<string | null>(null)
  const inFlight = useRef(false)
  const generation = useRef(0)
  useEffect(() => {
    const current = ++generation.current
    setData(null); setLoading(true); setBusy(false); setError(null); setConfirmation(null)
    getEmployeeTicket(ticketId, token!).then(result => { if (generation.current === current) setData(result) })
      .catch(failure => { if (generation.current === current) setError(explain(failure)) })
      .finally(() => { if (generation.current === current) setLoading(false) })
    return () => { generation.current++ }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ticketId, token, revision])

  async function start() {
    if (inFlight.current || !data?.canStartWork) return
    inFlight.current = true; setBusy(true); setError(null); setConfirmation(null)
    const current = generation.current
    try {
      await startEmployeeWork(ticketId, token!)
      if (generation.current !== current) return
      setConfirmation('Work started successfully.')
      // Clear stale eligibility immediately, including when the following read fails.
      setData(null)
      const refreshed = await getEmployeeTicket(ticketId, token!)
      if (generation.current === current) setData(refreshed)
    } catch (failure) {
      if (generation.current !== current) return
      setError(explain(failure)); setData(null)
      if ([403, 409].includes((failure as { status?: number })?.status ?? 0)) {
        try {
          const refreshed = await getEmployeeTicket(ticketId, token!)
          if (generation.current === current) setData(refreshed)
        } catch (refreshFailure) {
          if (generation.current === current) setError(`${explain(failure)} ${explain(refreshFailure)}`)
        }
      }
    } finally {
      inFlight.current = false
      if (generation.current === current) setBusy(false)
    }
  }

  return <section className="customer-card" aria-labelledby="ticket-heading">
    <Link to="/employee">Back to requests</Link>
    <h1 id="ticket-heading">Request details</h1>
    {loading && <p role="status">Loading request…</p>}
    {confirmation && <p role="status">{confirmation}</p>}
    {error && <p role="alert" className="error">{error}</p>}
    {!loading && !data && <button className="secondary-button" disabled={busy} onClick={() => setRevision(value => value + 1)}>Reload request</button>}
    {data && <>
      <h2>{data.ticket.title}</h2><p className="ticket-description">{data.description}</p>
      <dl className="ticket-facts">
        <dt>Ticket ID</dt><dd>{data.ticket.id}</dd><dt>Customer</dt><dd>{data.ticket.customerEmail}</dd>
        <dt>Status</dt><dd>{statusLabel(data.ticket.status)}</dd><dt>Priority</dt><dd>{priorityLabel(data.ticket.priority)}</dd>
        <dt>Assigned employee</dt><dd>{data.ticket.assignedEmployeeEmail ?? 'Unassigned'}</dd>
        <dt>Created</dt><dd>{new Date(data.ticket.createdAt).toLocaleString()}</dd>
        <dt>Updated</dt><dd>{new Date(data.ticket.updatedAt).toLocaleString()}</dd>
      </dl>
      <aside className="ai-advisory" aria-labelledby="ai-heading"><h2 id="ai-heading">AI recommendations</h2>
        <p>AI recommendations are not available in this workspace. This does not indicate whether analysis has been saved. You can review the request and start eligible work without them.</p>
      </aside>
      <div className="customer-actions">
        <button disabled={busy || !data.canStartWork} onClick={start}>{busy ? 'Starting work…' : 'Start Work'}</button>
        {!data.canStartWork && <p>This request is not currently available for you to start.</p>}
        {busy && <span role="status">Starting work. Please wait.</span>}
      </div>
      <h2>Request history</h2><ol className="ticket-history">{data.history.map(item => <li key={item.id}>
        <strong>{['Created', 'Assigned', 'Work started', 'Resolved', 'Closed'][item.action] ?? 'Activity'}</strong>
        {' · '}<time dateTime={item.occurredAt}>{new Date(item.occurredAt).toLocaleString()}</time>
        <small>Actor: {item.actorUserId}</small>
      </li>)}</ol>
    </>}
  </section>
}
