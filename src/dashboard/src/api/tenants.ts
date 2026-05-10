import { apiFetch } from './client'

export interface Tenant {
  id: string
  name: string
  isActive: boolean
  createdAt: string
}

export function getTenants(): Promise<Tenant[]> {
  return apiFetch<Tenant[]>('/api/tenants')
}
