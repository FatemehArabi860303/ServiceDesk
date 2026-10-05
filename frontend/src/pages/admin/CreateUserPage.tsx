import React from 'react'
import { useNavigate } from 'react-router-dom'
import CreateUserForm from '../../components/users/CreateUserForm'

export default function CreateUserPage() {
  const navigate = useNavigate()

  async function onCreated() {
    navigate('/admin/users')
  }

  return (
    <div style={{ padding: 24 }}>
      <h1>Create User</h1>
      <CreateUserForm onCreated={onCreated} />
    </div>
  )
}
