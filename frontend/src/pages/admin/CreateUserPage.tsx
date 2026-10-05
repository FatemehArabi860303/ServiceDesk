import React, { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import CreateUserForm from '../../components/users/CreateUserForm'
import type { ProvisionUserAccessResponse } from '../../api/usersApi'
import { provisionUserAccess } from '../../api/usersApi'
import ProvisionModal from '../../components/users/ProvisionModal'
import { useAuth } from '../../auth/AuthContext'

export default function CreateUserPage() {
  const navigate = useNavigate()
  const { token } = useAuth()
  const [createdUserId, setCreatedUserId] = useState<string | null>(null)
  const [provisionResult, setProvisionResult] = useState<ProvisionUserAccessResponse | null>(null)
  const [provisioning, setProvisioning] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function onCreated(createdId: string) {
    setCreatedUserId(createdId)
  }

  async function onProvisionNow() {
    if (!createdUserId) return
    setProvisioning(true)
    setError(null)
    try {
      const resp = await provisionUserAccess(createdUserId, token ?? undefined)
      setProvisionResult(resp)
    } catch (e: any) {
      setError(e?.message ?? 'Failed to provision access')
    } finally {
      setProvisioning(false)
    }
  }

  return (
    <div style={{ padding: 24 }}>
      <h1>Create User</h1>
      <CreateUserForm onCreated={onCreated} />

      {createdUserId && (
        <div style={{ marginTop: 16 }}>
          <div>User created successfully.</div>
          <div style={{ marginTop: 8 }}>
            <button onClick={onProvisionNow} disabled={provisioning}>Provision Access Now</button>
            <button onClick={() => navigate('/admin/users')} style={{ marginLeft: 8 }}>Back to Users</button>
          </div>
          {error && <div style={{ color: 'red', marginTop: 8 }}>Error: {error}</div>}
        </div>
      )}

      {provisionResult && (
        <div style={{ marginTop: 16 }}>
          <ProvisionModal token={provisionResult.activationToken} expiresAt={provisionResult.expiresAt} onClose={() => setProvisionResult(null)} />
        </div>
      )}
    </div>
  )
}
