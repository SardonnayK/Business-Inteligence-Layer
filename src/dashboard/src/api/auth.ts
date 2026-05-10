import { apiFetch } from './client'

export interface LoginResponse {
  token: string
  expiresAt: string
  userId: string
  username: string
  tenantId: string
  role: string
}

export function loginApi(username: string, password: string, tenantId: string): Promise<LoginResponse> {
  return apiFetch<LoginResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ username, password, tenantId }),
  })
}
