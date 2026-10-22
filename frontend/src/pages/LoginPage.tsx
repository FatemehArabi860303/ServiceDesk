import React from 'react'
import LoginForm from '../components/auth/LoginForm'
import { useLocation } from 'react-router-dom'

export default function LoginPage() {
  const location = useLocation()
  return (
    <main className="page-center">
      <section className="card auth-card" aria-labelledby="login-heading">
        <h1 id="login-heading" className="brand">ServiceDesk</h1>
        <h2>Sign in to your account</h2>
        {(location.state as { sessionExpired?: boolean } | null)?.sessionExpired &&
          <p role="status">Your session has expired. Sign in again to continue.</p>}
        <LoginForm />
      </section>
    </main>
  )
}
