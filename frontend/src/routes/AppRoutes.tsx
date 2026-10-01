import React from 'react'
import { Routes, Route, Navigate } from 'react-router-dom'
import LoginPage from '../pages/LoginPage'
import AdminHomePage from '../pages/admin/AdminHomePage'
import { useAuth } from '../auth/AuthContext'
import ProtectedRoute from '../auth/ProtectedRoute'

export default function AppRoutes() {
  const { isAuthenticated, user } = useAuth()

  return (
    <Routes>
      <Route
        path="/login"
        element={
          isAuthenticated && user?.role === 'Administrator' ? (
            <Navigate to="/admin" replace />
          ) : (
            <LoginPage />
          )
        }
      />

      <Route
        path="/admin"
        element={<ProtectedRoute requiredRole={"Administrator"}><AdminHomePage /></ProtectedRoute>}
      />

      <Route path="/" element={<Navigate to="/login" replace />} />
    </Routes>
  )
}
