import React from 'react'
import LoginForm from '../components/auth/LoginForm'

export default function LoginPage() {
  return (
    <main className="page-center">
      <section className="card auth-card" aria-labelledby="login-heading">
        <h1 id="login-heading" className="brand">ServiceDesk</h1>
        <h2 className="visually-hidden">Administrator Login</h2>
        <LoginForm />
      </section>
    </main>
  )
}
