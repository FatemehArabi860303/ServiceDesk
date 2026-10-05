import React from 'react'
import { Link } from 'react-router-dom'

export default function AdminHomePage() {
  return (
    <div style={{ padding: 24 }}>
      <h1>Administrator Dashboard</h1>
      <p>Welcome to the administrator area.</p>
      <div style={{ marginTop: 12 }}>
        <Link to="/admin/users">Users</Link>
      </div>
    </div>
  )
}
