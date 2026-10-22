import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'

export default function useEmployeeError() {
  const { logout } = useAuth()
  const navigate = useNavigate()
  return (error: unknown): string => {
    const status = (error as { status?: number })?.status
    if (status === 401) {
      logout()
      navigate('/login', { replace: true, state: { sessionExpired: true } })
      return 'Your session has expired. Sign in again.'
    }
    if (status === 403) return 'You do not have permission to access or change this request.'
    if (status === 404) return 'This request could not be found.'
    if (status === 409) return 'This request changed or is no longer available to start. Review its refreshed state.'
    if (error instanceof TypeError) return 'Unable to connect to ServiceDesk. Check your connection. If you tried to start work, refresh before retrying.'
    return 'ServiceDesk could not complete this operation. Please try again later.'
  }
}
