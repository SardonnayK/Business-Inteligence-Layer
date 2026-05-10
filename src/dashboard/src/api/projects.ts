import { apiFetch } from './client'

export interface Project {
  id: string
  tenantId: string
  name: string
  description: string
  createdAt: string
}

export interface Requirement {
  id: string
  projectId: string
  content: string
  status: string
  createdAt: string
}

export function getProjects(tenantId: string): Promise<Project[]> {
  return apiFetch<Project[]>(`/api/tenants/${tenantId}/projects`)
}

export function getRequirements(projectId: string): Promise<Requirement[]> {
  return apiFetch<Requirement[]>(`/api/projects/${projectId}/requirements`)
}
