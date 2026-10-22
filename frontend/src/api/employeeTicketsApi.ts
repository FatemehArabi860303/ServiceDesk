import { get, post } from './apiClient'

export type EmployeeTicket = {
  id: string; title: string; customerEmail: string
  assignedEmployeeUserId: string | null; assignedEmployeeEmail: string | null
  priority: number; status: number; createdAt: string; updatedAt: string
}
export type TicketPage = { items: EmployeeTicket[]; page: number; pageSize: number; hasMore: boolean }
export type TicketDetails = {
  ticket: EmployeeTicket; description: string; canStartWork: boolean
  history: { id: string; action: number; actorUserId: string; occurredAt: string; assignedEmployeeUserId: string | null }[]
}
export const getEmployeeTickets = (page: number, token: string) =>
  get<TicketPage>(`/api/employee/tickets?page=${page}&pageSize=20`, token)
export const getEmployeeTicket = (id: string, token: string) =>
  get<TicketDetails>(`/api/employee/tickets/${encodeURIComponent(id)}`, token)
export const startEmployeeWork = (id: string, token: string) =>
  post<void>(`/api/employee/tickets/${encodeURIComponent(id)}/start-work`, {}, token)

export const statusLabel = (status: number) => ['Open', 'In progress', 'Resolved', 'Closed'][status] ?? 'Unknown'
export const priorityLabel = (priority: number) => ['Low', 'Medium', 'High', 'Critical'][priority] ?? 'Unknown'
