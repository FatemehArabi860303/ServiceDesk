import React, { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { getEmployeeTickets, priorityLabel, statusLabel } from '../../api/employeeTicketsApi'
import type { TicketPage } from '../../api/employeeTicketsApi'
import useEmployeeError from './useEmployeeError'

export default function EmployeeTicketsPage() {
  const { token } = useAuth()
  const explain = useEmployeeError()
  const [page, setPage] = useState(1)
  const [revision, setRevision] = useState(0)
  const [data, setData] = useState<TicketPage | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    let current = true
    setLoading(true); setError(null); setData(null)
    getEmployeeTickets(page, token!).then(result => { if (current) setData(result) })
      .catch(failure => { if (current) setError(explain(failure)) })
      .finally(() => { if (current) setLoading(false) })
    return () => { current = false }
    // Error handling uses the current auth/navigation context at request initiation.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, token, revision])
  return <section className="customer-card" aria-labelledby="employee-heading">
    <p className="eyebrow">Deliver support</p><h1 id="employee-heading">Support requests</h1>
    <p>Review requests across customers and choose work to begin.</p>
    <button className="secondary-button" disabled={loading} onClick={() => setRevision(value => value + 1)}>Refresh requests</button>
    {loading && <p role="status">Loading requests…</p>}
    {error && <p role="alert" className="error">{error}</p>}
    {data && data.items.length === 0 && <p>No requests on this page.</p>}
    {data && data.items.length > 0 && <div className="ticket-table-scroll"><table className="ticket-table">
      <caption className="visually-hidden">Customer support requests</caption>
      <thead><tr>{['Request', 'Customer', 'Status', 'Priority', 'Assigned employee', 'Created'].map(label => <th scope="col" key={label}>{label}</th>)}</tr></thead>
      <tbody>{data.items.map(ticket => <tr key={ticket.id}>
        <td><Link to={`/employee/tickets/${ticket.id}`}>{ticket.title}</Link><small>{ticket.id}</small></td>
        <td>{ticket.customerEmail}</td><td>{statusLabel(ticket.status)}</td><td>{priorityLabel(ticket.priority)}</td>
        <td>{ticket.assignedEmployeeEmail ?? 'Unassigned'}</td><td><time dateTime={ticket.createdAt}>{new Date(ticket.createdAt).toLocaleString()}</time></td>
      </tr>)}</tbody>
    </table></div>}
    <nav className="customer-actions" aria-label="Request pages">
      <button disabled={loading || page === 1} onClick={() => setPage(value => value - 1)}>Previous page</button>
      <span>Page {page}</span>
      <button disabled={loading || !data?.hasMore} onClick={() => setPage(value => value + 1)}>Next page</button>
    </nav>
  </section>
}
