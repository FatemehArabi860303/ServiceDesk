import React from 'react'
import type { UserResponse } from '../../types/user'
import { roleNumberToUserRole } from '../../types/user'

export default function UsersTable({ users }: { users: UserResponse[] }) {
  return (
    <table style={{ borderCollapse: 'collapse', width: '100%' }}>
      <thead>
        <tr>
          <th style={{ textAlign: 'left', padding: 8 }}>First Name</th>
          <th style={{ textAlign: 'left', padding: 8 }}>Last Name</th>
          <th style={{ textAlign: 'left', padding: 8 }}>Email</th>
          <th style={{ textAlign: 'left', padding: 8 }}>Role</th>
          <th style={{ textAlign: 'left', padding: 8 }}>Active</th>
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
          </tr>
        ))}
      </tbody>
    </table>
  )
}
