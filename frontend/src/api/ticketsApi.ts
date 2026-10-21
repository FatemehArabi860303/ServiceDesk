import { post } from './apiClient'

export type TicketPriority = 0 | 1 | 2 | 3

export type CreateTicketRequest = {
  title: string
  description: string
  priority: TicketPriority
}

// Matches the WebApi TicketResponse. Customer identity is never sent in the request.
export type TicketResponse = CreateTicketRequest & {
  id: string
  customerUserId: string
  status: number
  createdAt: string
  updatedAt: string
}

export async function createTicket(request: CreateTicketRequest, token: string): Promise<TicketResponse> {
  const ticket = await post<TicketResponse>('/api/tickets', request, token)
  if (!ticket?.id) throw new Error('Missing ticket confirmation.')
  return ticket
}
