import React from 'react'
import { Link, Outlet } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'

export default function EmployeeLayout() {
  const { logout } = useAuth()
  return <div className="customer-app">
    <a className="skip-link" href="#employee-content">Skip to content</a>
    <header className="customer-header">
      <Link className="customer-brand" to="/employee">ServiceDesk<span>Support workspace</span></Link>
      <button className="secondary-button" onClick={logout}>Sign out</button>
    </header>
    <main className="employee-main" id="employee-content" tabIndex={-1}><Outlet /></main>
  </div>
}
