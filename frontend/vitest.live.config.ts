import { mergeConfig } from 'vitest/config'
import config from './vite.config'

// Explicit opt-in: credentials and an isolated running API must be supplied securely.
export default mergeConfig(config, {
  test: { include: ['e2e/customer-submit.live.tsx'], testTimeout: 30000 }
})
