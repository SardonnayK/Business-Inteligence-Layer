import { apiFetch } from './client'

export interface TenantUserItem {
  id: string
  username: string
  role: string
  isActive: boolean
  createdAt: string
  permissionCount: number
}

export interface ArtifactPermission {
  id: string
  name: string
  departmentName: string | null
  isShared: boolean
  canRead: boolean
  canWrite: boolean
}

export function getUsers(): Promise<TenantUserItem[]> {
  return apiFetch<TenantUserItem[]>('/api/users')
}

export function createUser(username: string, password: string, role: string): Promise<TenantUserItem> {
  return apiFetch<TenantUserItem>('/api/users', {
    method: 'POST',
    body: JSON.stringify({ username, password, role }),
  })
}

export function updateUser(id: string, patch: { role?: string; isActive?: boolean }): Promise<TenantUserItem> {
  return apiFetch<TenantUserItem>(`/api/users/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(patch),
  })
}

export function getUserPermissions(id: string): Promise<ArtifactPermission[]> {
  return apiFetch<ArtifactPermission[]>(`/api/users/${id}/permissions`)
}

export function updateUserPermissions(
  id: string,
  permissions: Array<{ artifactId: string; canRead: boolean; canWrite: boolean }>,
): Promise<void> {
  return apiFetch<void>(`/api/users/${id}/permissions`, {
    method: 'PUT',
    body: JSON.stringify(permissions),
  })
}
