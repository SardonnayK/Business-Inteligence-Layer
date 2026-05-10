import { apiFetch } from './client'

export interface ProviderInfo {
  type: string
  name: string
  requiresApiKey: boolean
  requiresEndpoint: boolean
  defaultModel: string
  supportedModels: string[]
}

export interface EmbeddingConfig {
  id?: string
  providerType: number
  modelId: string
  endpoint: string | null
  hasApiKey: boolean
  updatedAt?: string
  isDefault?: boolean
}

export interface EmbeddingConfigUpdate {
  providerType: number
  modelId: string
  apiKey: string | null
  endpoint: string | null
}

export function getProviders(): Promise<ProviderInfo[]> {
  return apiFetch<ProviderInfo[]>('/api/embedding-config/providers')
}

export function getSystemConfig(): Promise<EmbeddingConfig> {
  return apiFetch<EmbeddingConfig>('/api/embedding-config/system')
}

export function saveSystemConfig(data: EmbeddingConfigUpdate): Promise<EmbeddingConfig> {
  return apiFetch<EmbeddingConfig>('/api/embedding-config/system', {
    method: 'PUT',
    body: JSON.stringify(data),
  })
}

export function getTenantConfig(tenantId: string): Promise<EmbeddingConfig> {
  return apiFetch<EmbeddingConfig>(`/api/embedding-config/tenant/${tenantId}`)
}

export function saveTenantConfig(tenantId: string, data: EmbeddingConfigUpdate): Promise<EmbeddingConfig> {
  return apiFetch<EmbeddingConfig>(`/api/embedding-config/tenant/${tenantId}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  })
}

export function resetTenantConfig(tenantId: string): Promise<void> {
  return apiFetch<void>(`/api/embedding-config/tenant/${tenantId}`, {
    method: 'DELETE',
  })
}
