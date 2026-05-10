import { apiFetch } from './client'

export type HealthStatus = Record<string, unknown>

export function getHealth(): Promise<HealthStatus> {
  return apiFetch<HealthStatus>('/health')
}
