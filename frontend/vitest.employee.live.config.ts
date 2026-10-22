import { mergeConfig } from 'vitest/config'
import config from './vite.config'

// Opt in only against disposable development accounts and a synthetic ticket.
export default mergeConfig(config, {
  test: { include: ['e2e/employee-start.live.tsx'], testTimeout: 30000 }
})
