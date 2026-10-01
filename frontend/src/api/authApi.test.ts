import { login } from './authApi'
import * as apiClient from './apiClient'

vi.mock('./apiClient')

test('maps 401 to friendly message', async () => {
  vi.spyOn(apiClient, 'post').mockRejectedValue({ status: 401, message: 'Authentication failed.' })

  await expect(login({ Email: 'a', Password: 'b' })).rejects.toThrow('Invalid email or password.')
})

test('returns response on success', async () => {
  const resp = { accessToken: 't', expiresAt: 'x' }
  vi.spyOn(apiClient, 'post').mockResolvedValue(resp)
  await expect(login({ Email: 'a', Password: 'b' })).resolves.toEqual(resp)
})
