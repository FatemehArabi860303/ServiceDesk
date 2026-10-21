import React from 'react'
import { Link } from 'react-router-dom'

export default function CustomerHomePage() {
  return (
    <section className="customer-card customer-welcome" aria-labelledby="customer-heading">
      <p className="eyebrow">Your support workspace</p>
      <h1 id="customer-heading">How can we help?</h1>
      <p>Tell us what you need help with. Submit a request and our support team will take it from there.</p>
      <Link className="button-link" to="/customer/requests/new">Submit a Request <span aria-hidden="true">→</span></Link>
    </section>
  )
}
