import React from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext'
import type { ReactNode } from 'react'

export default function ProtectedRoute({
  children,
  requiredRole
}: {
  children: ReactNode
  requiredRole?: string
}) {
  const auth = useAuth()
  const location = useLocation()

  if (!auth.isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  if (requiredRole && auth.user?.role !== requiredRole) {
    // Not authorized for this area
    return <Navigate to="/login" replace />
  }

  return <>{children}</>
}
