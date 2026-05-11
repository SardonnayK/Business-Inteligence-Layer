import { apiFetch } from './client'

export interface Department {
  id: string
  name: string
  description: string
  estimatedSize: 'small' | 'medium' | 'large' | 'unknown'
  createdAt: string
  artifactCount: number
}

export interface ArtifactDepartment {
  id: string
  name: string
}

export interface Artifact {
  id: string
  tenantId: string
  departments: ArtifactDepartment[]
  name: string
  description: string
  isShared: boolean
  chunkCount: number
  createdAt: string
  updatedAt: string
}

export interface ContextChunk {
  id: string
  text: string
  source: string | null
  category: string | null
  createdAt: string
  artifactId: string | null
  artifactName: string | null
}

export interface DiscoveryResult {
  wasAiAssisted: boolean
  departments: Department[]
  artifacts: Artifact[]
}

export function getDepartments(tenantId: string): Promise<Department[]> {
  return apiFetch<Department[]>('/api/departments', { tenantId })
}

export function discoverDepartments(tenantId: string): Promise<DiscoveryResult> {
  return apiFetch<DiscoveryResult>(`/api/tenants/${tenantId}/discover-departments`, {
    method: 'POST',
  })
}

export function getArtifacts(tenantId: string): Promise<Artifact[]> {
  return apiFetch<Artifact[]>('/api/artifacts', { tenantId })
}

export function getArtifactContexts(artifactId: string): Promise<ContextChunk[]> {
  return apiFetch<ContextChunk[]>(`/api/artifacts/${artifactId}/contexts`)
}

export function clearArtifactContexts(artifactId: string): Promise<{ deletedCount: number }> {
  return apiFetch<{ deletedCount: number }>(`/api/artifacts/${artifactId}/contexts`, {
    method: 'DELETE',
  })
}
