import React, { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { getUsers } from '../../api/usersApi'
import type { UserResponse } from '../../types/user'
import UsersTable from '../../components/users/UsersTable'

export default function UsersPage() {
  const { user, token } = useAuth()
  const navigate = useNavigate()
  const [users, setUsers] = useState<UserResponse[] | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let mounted = true
    async function load() {
      setLoading(true)
      setError(null)
      try {
        const data = await getUsers(token ?? undefined)
        if (!mounted) return
        setUsers(data)
      } catch (e: any) {
        setError(e?.message ?? 'Failed to load users')
      } finally {
        if (mounted) setLoading(false)
      }
    }

    load()
    return () => {
      mounted = false
    }
  }, [token])

  function onAdd() {
    navigate('/admin/users/new')
  }

  return (
    <div style={{ padding: 24 }}>
      <h1>Users</h1>
      <div style={{ marginBottom: 12 }}>
        <button onClick={onAdd}>Add User</button>
      </div>

      {loading && <div>Loading users...</div>}
      {error && <div style={{ color: 'red' }}>Error: {error}</div>}
      {!loading && !error && users && users.length === 0 && <div>No users found.</div>}

      {!loading && !error && users && users.length > 0 && <UsersTable users={users} />}
    </div>
  )
}
