import { apiFetch } from './client'

export interface IngestRequest {
  text: string
  source: string
  category: string
}

export interface IngestResponse {
  id: string
  tenantId: string
  artifactId: string | null
  artifactName: string | null
  departmentId: string | null
  departmentName: string | null
  isShared: boolean
  createdAt: string
}

export interface SearchResult {
  id: string
  text: string
  source: string
  category: string
  createdAt: string
}

export function ingestBusinessContext(
  tenantId: string,
  body: IngestRequest,
): Promise<IngestResponse> {
  return apiFetch<IngestResponse>('/api/business-context', {
    method: 'POST',
    tenantId,
    body: JSON.stringify(body),
  })
}

export function searchBusinessContext(
  tenantId: string,
  query: string,
  topK: number,
  artifactId?: string,
  departmentId?: string,
): Promise<SearchResult[]> {
  const params = new URLSearchParams({
    query,
    topK: String(topK),
  })
  if (artifactId) params.set('artifactId', artifactId)
  if (departmentId) params.set('departmentId', departmentId)
  return apiFetch<SearchResult[]>(`/api/business-context/search?${params}`, {
    tenantId,
  })
}
