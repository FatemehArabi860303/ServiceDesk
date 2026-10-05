import React, { useState } from 'react'
import { createUser } from '../../api/usersApi'
import type { UserRole } from '../../auth/authTypes'
import { useAuth } from '../../auth/AuthContext'

type Props = {
  onCreated: () => void
}

export default function CreateUserForm({ onCreated }: Props) {
  const { token } = useAuth()
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<UserRole>('Employee')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    if (!email || !role) {
      setError('Email and Role are required')
      return
    }

    setLoading(true)
    try {
      await createUser(
        {
          FirstName: firstName || undefined,
          LastName: lastName || undefined,
          Email: email,
          Role: role
        },
        token ?? undefined
      )
      onCreated()
    } catch (e: any) {
      setError(e?.message ?? 'Failed to create user')
    } finally {
      setLoading(false)
    }
  }

  return (
    <form onSubmit={onSubmit} style={{ maxWidth: 600 }}>
      {error && <div style={{ color: 'red', marginBottom: 8 }}>{error}</div>}
      <div style={{ marginBottom: 8 }}>
        <label>
          First Name
          <input value={firstName} onChange={e => setFirstName(e.target.value)} />
        </label>
      </div>
      <div style={{ marginBottom: 8 }}>
        <label>
          Last Name
          <input value={lastName} onChange={e => setLastName(e.target.value)} />
        </label>
      </div>
      <div style={{ marginBottom: 8 }}>
        <label>
          Email
          <input value={email} onChange={e => setEmail(e.target.value)} />
        </label>
      </div>
      <div style={{ marginBottom: 8 }}>
        <label>
          Role
          <select value={role} onChange={e => setRole(e.target.value as UserRole)}>
            <option value="Administrator">Administrator</option>
            <option value="Employee">Employee</option>
            <option value="Customer">Customer</option>
          </select>
        </label>
      </div>
      <div>
        <button type="submit" disabled={loading}>{loading ? 'Creating...' : 'Create'}</button>
      </div>
    </form>
  )
}
