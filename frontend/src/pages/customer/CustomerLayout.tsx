import React from 'react'
import { Link, Outlet } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'

export default function CustomerLayout() {
  const { logout } = useAuth()
  return (
    <div className="customer-app">
      <a className="skip-link" href="#customer-content">Skip to content</a>
      <header className="customer-header">
        <Link className="customer-brand" to="/customer">ServiceDesk<span>Customer support</span></Link>
        <button className="secondary-button" onClick={logout}>Sign out</button>
      </header>
      <main id="customer-content" className="customer-main" tabIndex={-1}><Outlet /></main>
      <footer className="customer-footer">ServiceDesk · Here to help</footer>
    </div>
  )
}
