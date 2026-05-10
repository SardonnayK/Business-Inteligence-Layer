import { apiFetch } from './client'

export interface IngestRequest {
  text: string
  source: string
  category: string
}

export interface IngestResponse {
  id: string
  tenantId: string
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
): Promise<SearchResult[]> {
  const params = new URLSearchParams({
    query,
    topK: String(topK),
  })
  return apiFetch<SearchResult[]>(`/api/business-context/search?${params}`, {
    tenantId,
  })
}
