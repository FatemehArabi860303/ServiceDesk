import React, { useState } from 'react'
import { createUser } from '../../api/usersApi'
import type { UserRole } from '../../auth/authTypes'
enum NumericUserRole {
  Customer = 0,
  Employee = 1,
  Administrator = 2
}
import { useAuth } from '../../auth/AuthContext'

type Props = {
  onCreated: (createdId: string) => void
}

export default function CreateUserForm({ onCreated }: Props) {
  const { token } = useAuth()
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  // keep role selection as numeric string in the select; convert to number on submit
  const [role, setRole] = useState<string>(String(NumericUserRole.Employee))
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    if (!email || role === '') {
      setError('Email and Role are required')
      return
    }

    setLoading(true)
    try {
      const numericRole = Number(role)
      if (![0, 1, 2].includes(numericRole)) throw new Error('Invalid role')

      const created = await createUser(
        {
          FirstName: firstName || undefined,
          LastName: lastName || undefined,
          Email: email,
          Role: numericRole
        },
        token ?? undefined
      )
      onCreated(created.id)
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
          <select value={role} onChange={e => setRole(e.target.value)}>
            <option value={String(NumericUserRole.Administrator)}>Administrator</option>
            <option value={String(NumericUserRole.Employee)}>Employee</option>
            <option value={String(NumericUserRole.Customer)}>Customer</option>
          </select>
        </label>
      </div>
      <div>
        <button type="submit" disabled={loading}>{loading ? 'Creating...' : 'Create'}</button>
      </div>
    </form>
  )
}
