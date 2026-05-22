import { apiFetch } from './client'

export interface McpIntegration {
  id: string
  tenantId: string
  name: string
  serverUrl: string
  hasApiKey: boolean
  authHeaderName: string
  toolNamesJson: string
  cachedToolsJson: string | null
  isEnabled: boolean
  createdAt: string
  updatedAt: string
}

export interface McpToolSummary {
  name: string
  description: string
}

export interface DiscoverResult {
  toolCount: number
  tools: McpToolSummary[]
}

export async function listMcpIntegrations(): Promise<McpIntegration[]> {
  return apiFetch<McpIntegration[]>('/api/mcp-integrations')
}

export async function createMcpIntegration(data: {
  name: string
  serverUrl: string
  apiKey?: string
  authHeaderName?: string
  toolNames?: string[]
}): Promise<McpIntegration> {
  return apiFetch<McpIntegration>('/api/mcp-integrations', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  })
}

export async function updateMcpIntegration(
  id: string,
  data: {
    name?: string
    serverUrl?: string
    apiKey?: string
    authHeaderName?: string
    isEnabled?: boolean
    toolNames?: string[]
  }
): Promise<McpIntegration> {
  return apiFetch<McpIntegration>(`/api/mcp-integrations/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  })
}

export async function deleteMcpIntegration(id: string): Promise<void> {
  await apiFetch<void>(`/api/mcp-integrations/${id}`, { method: 'DELETE' })
}

export async function discoverMcpTools(id: string): Promise<DiscoverResult> {
  return apiFetch<DiscoverResult>(`/api/mcp-integrations/${id}/discover`, { method: 'POST' })
}
