import React, { useState } from 'react'

export default function ProvisionModal({ token, expiresAt, onClose }: { token: string; expiresAt: string; onClose(): void }) {
  const [copied, setCopied] = useState(false)

  async function copyToken() {
    try {
      await navigator.clipboard.writeText(token)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      setCopied(false)
    }
  }

  return (
    <div style={{ border: '1px solid #ccc', padding: 12, background: '#fff' }} role="dialog" aria-modal="true">
      <h2>User Access Provisioned</h2>
      <div>
        <div><strong>Activation Token</strong></div>
        <div style={{ fontFamily: 'monospace', marginTop: 6 }}>{token}</div>
        <div style={{ marginTop: 6 }}><strong>Expires At:</strong> {new Date(expiresAt).toString()}</div>
        <div style={{ marginTop: 8, color: '#555' }}>Share this activation token securely with the user. The token is single-use and expires in 24 hours.</div>
        <div style={{ marginTop: 8 }}>
          <button onClick={copyToken}>{copied ? 'Copied' : 'Copy Token'}</button>
          <button onClick={onClose} style={{ marginLeft: 8 }}>Close</button>
        </div>
      </div>
    </div>
  )
}
