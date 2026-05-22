import { apiFetch, getStoredToken } from './client'

export type AgentType = 'BuiltIn' | 'HttpPlugin'
export type AgentCapability = 'Ingest' | 'Query' | 'Discover' | 'General'

export interface AgentRegistration {
  id: string
  tenantId: string | null
  agentType: AgentType
  capability: AgentCapability
  name: string
  description: string
  httpEndpoint: string | null
  inputSchemaJson: string | null
  isEnabled: boolean
  priority: number
  createdAt: string
  updatedAt: string
}

export interface AgentConfig {
  requireConfirmationForRerouting: boolean
  autoIngestAgentOutput: boolean
  allowAutoCreateArtifacts: boolean
}

export interface ProcessRequest {
  capability: number
  textInput?: string
  hintArtifactId?: string
  forceRegistrationId?: string
}

export interface ProcessResponse {
  executionId: string
  agentName: string
  output: string | null
  routedArtifactId: string | null
  requiresUserConfirmation: boolean
  confirmationMessage: string | null
  suggestedArtifactId: string | null
  ingestedCount: number
}

export interface CreateAgentRequest {
  capability: number
  name: string
  description: string
  httpEndpoint: string
  inputSchemaJson?: string
  priority?: number
}

export function getAgents(): Promise<AgentRegistration[]> {
  return apiFetch<AgentRegistration[]>('/api/agents')
}

export function getAgentConfig(): Promise<AgentConfig> {
  return apiFetch<AgentConfig>('/api/agents/config')
}

export function updateAgentConfig(config: AgentConfig): Promise<AgentConfig> {
  return apiFetch<AgentConfig>('/api/agents/config', {
    method: 'PUT',
    body: JSON.stringify(config),
  })
}

export function createAgent(req: CreateAgentRequest): Promise<AgentRegistration> {
  return apiFetch<AgentRegistration>('/api/agents', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

export function updateAgent(
  id: string,
  updates: Partial<Pick<AgentRegistration, 'isEnabled' | 'priority' | 'httpEndpoint' | 'inputSchemaJson'>>,
): Promise<AgentRegistration> {
  return apiFetch<AgentRegistration>(`/api/agents/${id}`, {
    method: 'PUT',
    body: JSON.stringify(updates),
  })
}

export function deleteAgent(id: string): Promise<void> {
  return apiFetch<void>(`/api/agents/${id}`, { method: 'DELETE' })
}

export function processAgentRequest(tenantId: string, req: ProcessRequest): Promise<ProcessResponse> {
  return apiFetch<ProcessResponse>('/api/agent/process', {
    method: 'POST',
    tenantId,
    body: JSON.stringify(req),
  })
}

export function confirmAgentAction(
  tenantId: string,
  executionId: string,
  accept: boolean,
): Promise<ProcessResponse> {
  return apiFetch<ProcessResponse>(`/api/agent/process/${executionId}/confirm`, {
    method: 'POST',
    tenantId,
    body: JSON.stringify({ accept }),
  })
}

export async function processAgentFile(
  tenantId: string,
  capability: number,
  file: File,
  hintArtifactId?: string,
): Promise<ProcessResponse> {
  const form = new FormData()
  form.append('capability', String(capability))
  form.append('file', file)
  if (hintArtifactId) form.append('hintArtifactId', hintArtifactId)

  const token = getStoredToken()
  const headers: Record<string, string> = {}
  if (token) headers['Authorization'] = `Bearer ${token}`
  if (tenantId) headers['X-Tenant-Id'] = tenantId

  const BASE_URL = import.meta.env.VITE_API_URL ?? ''
  const res = await fetch(`${BASE_URL}/api/agent/process`, { method: 'POST', headers, body: form })
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new Error(text || `HTTP ${res.status}`)
  }
  return res.json()
}
