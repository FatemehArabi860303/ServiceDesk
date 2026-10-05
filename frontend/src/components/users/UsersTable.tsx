import React from 'react'
import type { UserResponse } from '../../types/user'
import { roleNumberToUserRole } from '../../types/user'

export default function UsersTable({ users, onProvision }: { users: UserResponse[]; onProvision?: (id: string) => void }) {
  return (
    <table style={{ borderCollapse: 'collapse', width: '100%' }}>
      <thead>
        <tr>
          <th style={{ textAlign: 'left', padding: 8 }}>First Name</th>
          <th style={{ textAlign: 'left', padding: 8 }}>Last Name</th>
          <th style={{ textAlign: 'left', padding: 8 }}>Email</th>
          <th style={{ textAlign: 'left', padding: 8 }}>Role</th>
          <th style={{ textAlign: 'left', padding: 8 }}>Active</th>
          <th style={{ textAlign: 'left', padding: 8 }}>Actions</th>
        </tr>
      </thead>
      <tbody>
        {users.map(u => (
          <tr key={u.id}>
            <td style={{ padding: 8 }}>{u.firstName ?? ''}</td>
            <td style={{ padding: 8 }}>{u.lastName ?? ''}</td>
            <td style={{ padding: 8 }}>{u.email}</td>
            <td style={{ padding: 8 }}>{roleNumberToUserRole(u.role)}</td>
            <td style={{ padding: 8 }}>{u.isActive ? 'Yes' : 'No'}</td>
            <td style={{ padding: 8 }}>
              {onProvision && (
                <button onClick={() => onProvision(u.id)}>Provision Access</button>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
