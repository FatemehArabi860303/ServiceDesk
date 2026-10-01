import React, { useState } from 'react'
import { useAuth } from '../../auth/AuthContext'
import { useNavigate, useLocation } from 'react-router-dom'

type FormState = {
  email: string
  password: string
}

export default function LoginForm() {
  const [form, setForm] = useState<FormState>({ email: '', password: '' })
  const [errors, setErrors] = useState<string | null>(null)
  const { login } = useAuth()
  const [loading, setLoading] = useState(false)
  const navigate = useNavigate()
  const location = useLocation()

  const validate = () => {
    if (!form.email.trim()) return 'Email is required.'
    if (!form.password) return 'Password is required.'
    return null
  }

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setErrors(null)
    const v = validate()
    if (v) {
      setErrors(v)
      return
    }
    setLoading(true)
    try {
      await login(form.email.trim(), form.password)
      const from = (location.state as any)?.from?.pathname
      navigate(from ?? '/admin', { replace: true })
    } catch (err: any) {
      const message = err?.message ?? 'Invalid email or password.'
      setErrors(message)
    } finally {
      setLoading(false)
      setForm(f => ({ ...f, password: '' }))
    }
  }

  return (
    <form onSubmit={onSubmit} aria-describedby="auth-error">
      <div className="field">
        <label htmlFor="email">Email</label>
        <input
          id="email"
          name="email"
          type="email"
          autoComplete="username"
          value={form.email}
          onChange={e => setForm(f => ({ ...f, email: e.target.value }))}
          disabled={loading}
        />
      </div>

      <div className="field">
        <label htmlFor="password">Password</label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          value={form.password}
          onChange={e => setForm(f => ({ ...f, password: e.target.value }))}
          disabled={loading}
        />
      </div>

      {errors && (
        <div id="auth-error" role="alert" className="error">
          {errors}
        </div>
      )}

      <div className="actions">
        <button type="submit" disabled={loading} aria-busy={loading}>
          {loading ? 'Signing in...' : 'Sign in'}
        </button>
      </div>
    </form>
  )
}
