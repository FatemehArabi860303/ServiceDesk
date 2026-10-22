import React from 'react'
import { Routes, Route, Navigate, useLocation } from 'react-router-dom'
import LoginPage from '../pages/LoginPage'
import AdminHomePage from '../pages/admin/AdminHomePage'
import UsersPage from '../pages/admin/UsersPage'
import CreateUserPage from '../pages/admin/CreateUserPage'
import { useAuth } from '../auth/AuthContext'
import ProtectedRoute from '../auth/ProtectedRoute'
import CustomerLayout from '../pages/customer/CustomerLayout'
import CustomerHomePage from '../pages/customer/CustomerHomePage'
import SubmitRequestPage from '../pages/customer/SubmitRequestPage'
import EmployeeLayout from '../pages/employee/EmployeeLayout'
import EmployeeTicketsPage from '../pages/employee/EmployeeTicketsPage'
import EmployeeTicketPage from '../pages/employee/EmployeeTicketPage'

export default function AppRoutes() {
  const { isAuthenticated, user } = useAuth()
  const location = useLocation()
  const requested = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname
  const home = user?.role === 'Customer' ? '/customer' : user?.role === 'Employee' ? '/employee' : '/admin'
  const destination = requested && (requested === home || requested.startsWith(`${home}/`)) ? requested : home

  return (
    <Routes>
      <Route
        path="/login"
        element={
          isAuthenticated ? (
            <Navigate to={destination} replace />
          ) : (
            <LoginPage />
          )
        }
      />

      <Route path="/employee" element={<ProtectedRoute requiredRole="Employee"><EmployeeLayout /></ProtectedRoute>}>
        <Route index element={<EmployeeTicketsPage />} />
        <Route path="tickets/:ticketId" element={<EmployeeTicketPage />} />
      </Route>

      <Route path="/customer" element={<ProtectedRoute requiredRole="Customer"><CustomerLayout /></ProtectedRoute>}>
        <Route index element={<CustomerHomePage />} />
        <Route path="requests/new" element={<SubmitRequestPage />} />
      </Route>

      <Route
        path="/admin"
        element={<ProtectedRoute requiredRole={"Administrator"}><AdminHomePage /></ProtectedRoute>}
      />

      <Route
        path="/admin/users"
        element={<ProtectedRoute requiredRole={"Administrator"}><UsersPage /></ProtectedRoute>}
      />

      <Route
        path="/admin/users/new"
        element={<ProtectedRoute requiredRole={"Administrator"}><CreateUserPage /></ProtectedRoute>}
      />

      <Route path="/" element={<Navigate to="/login" replace />} />
    </Routes>
  )
}
