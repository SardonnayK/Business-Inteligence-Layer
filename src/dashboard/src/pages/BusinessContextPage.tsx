import { useState, useEffect, useRef } from 'react'
import type { Tenant } from '../api/tenants'
import { ingestBusinessContext, searchBusinessContext } from '../api/businessContext'
import type { SearchResult, IngestResponse } from '../api/businessContext'
import { getArtifacts } from '../api/knowledge'
import type { Artifact } from '../api/knowledge'
import Badge from '../components/Badge'
import { processAgentFile, confirmAgentAction } from '../api/agents'
import type { ProcessResponse } from '../api/agents'

interface Props {
  tenants: Tenant[]
  selectedTenantId: string
  setSelectedTenantId: (id: string) => void
  tenantsLoading: boolean
  refreshTenants: () => void
}

function formatDate(iso: string) {
  try {
    return new Date(iso).toLocaleString()
  } catch {
    return iso
  }
}

export default function BusinessContextPage({
  tenants,
  selectedTenantId,
  setSelectedTenantId,
}: Props) {
  // Ingest form state
  const [ingestTenantId, setIngestTenantId] = useState(selectedTenantId)
  const [ingestText, setIngestText] = useState('')
  const [ingestSource, setIngestSource] = useState('')
  const [ingestCategory, setIngestCategory] = useState('')
  const [ingesting, setIngesting] = useState(false)
  const [ingestResult, setIngestResult] = useState<{ ok: boolean; message: string } | null>(null)
  const [lastIngest, setLastIngest] = useState<IngestResponse | null>(null)

  // File drop zone state
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [isDragging, setIsDragging] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)

  // Agent confirmation state
  const [pendingResponse, setPendingResponse] = useState<ProcessResponse | null>(null)
  const [confirming, setConfirming] = useState(false)
  const [agentName, setAgentName] = useState<string | null>(null)

  // Search state
  const [searchTenantId, setSearchTenantId] = useState(selectedTenantId)
  const [searchQuery, setSearchQuery] = useState('')
  const [topK, setTopK] = useState(5)
  const [searching, setSearching] = useState(false)
  const [searchResults, setSearchResults] = useState<SearchResult[]>([])
  const [searchError, setSearchError] = useState<string | null>(null)
  const [hasSearched, setHasSearched] = useState(false)

  // Filter dropdowns for search
  const [artifacts, setArtifacts] = useState<Artifact[]>([])
  const [filterArtifactId, setFilterArtifactId] = useState('')
  const [filterDepartmentId, setFilterDepartmentId] = useState('')

  useEffect(() => {
    if (selectedTenantId) {
      setIngestTenantId(selectedTenantId)
      setSearchTenantId(selectedTenantId)
      setFilterArtifactId('')
      setFilterDepartmentId('')
      // Fetch artifacts for filter dropdowns
      getArtifacts(selectedTenantId)
        .then(setArtifacts)
        .catch(() => setArtifacts([]))
    }
  }, [selectedTenantId])

  // Derived: unique departments from artifact list
  const departmentsFromArtifacts = (() => {
    const seen = new Map<string, string>()
    for (const a of artifacts) {
      for (const d of a.departments) {
        if (!seen.has(d.id)) seen.set(d.id, d.name)
      }
    }
    return Array.from(seen.entries()).map(([id, name]) => ({ id, name }))
  })()

  function handleFileDrop(e: React.DragEvent) {
    e.preventDefault()
    setIsDragging(false)
    const file = e.dataTransfer.files[0]
    if (file) setSelectedFile(file)
  }

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (file) setSelectedFile(file)
  }

  async function handleIngest(e: React.FormEvent) {
    e.preventDefault()
    if (!ingestTenantId) return
    setIngesting(true)
    setIngestResult(null)
    setLastIngest(null)
    setAgentName(null)
    setPendingResponse(null)

    if (selectedFile) {
      try {
        const res = await processAgentFile(ingestTenantId, 0, selectedFile)
        if (res.requiresUserConfirmation) {
          setPendingResponse(res)
          setIngesting(false)
          return
        }
        setAgentName(res.agentName || null)
        setIngestResult({
          ok: true,
          message: `Ingested successfully via agent. Artifact ID: ${res.routedArtifactId ?? 'N/A'}`,
        })
        setSelectedFile(null)
        if (fileInputRef.current) fileInputRef.current.value = ''
      } catch (err) {
        setIngestResult({
          ok: false,
          message: err instanceof Error ? err.message : 'Ingest failed.',
        })
      } finally {
        setIngesting(false)
      }
      return
    }

    try {
      const result = await ingestBusinessContext(ingestTenantId, {
        text: ingestText,
        source: ingestSource,
        category: ingestCategory,
      })
      setLastIngest(result)
      setIngestResult({
        ok: true,
        message: `Ingested successfully. Record ID: ${result.id}`,
      })
      setIngestText('')
      setIngestSource('')
      setIngestCategory('')
    } catch (err) {
      setIngestResult({
        ok: false,
        message: err instanceof Error ? err.message : 'Ingest failed.',
      })
    } finally {
      setIngesting(false)
    }
  }

  async function handleConfirm(accept: boolean) {
    if (!pendingResponse || !ingestTenantId) return
    setConfirming(true)
    try {
      const res = await confirmAgentAction(ingestTenantId, pendingResponse.executionId, accept)
      setAgentName(res.agentName || null)
      setIngestResult({
        ok: true,
        message: `Ingest ${accept ? 'accepted' : 'kept original'}. Artifact ID: ${res.routedArtifactId ?? 'N/A'}`,
      })
      setSelectedFile(null)
      if (fileInputRef.current) fileInputRef.current.value = ''
    } catch (err) {
      setIngestResult({
        ok: false,
        message: err instanceof Error ? err.message : 'Confirmation failed.',
      })
    } finally {
      setPendingResponse(null)
      setConfirming(false)
    }
  }

  async function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    if (!searchTenantId || !searchQuery.trim()) return
    setSearching(true)
    setSearchError(null)
    setHasSearched(true)
    try {
      const results = await searchBusinessContext(
        searchTenantId,
        searchQuery,
        topK,
        filterArtifactId || undefined,
        filterDepartmentId || undefined,
      )
      setSearchResults(results)
    } catch (err) {
      setSearchError(err instanceof Error ? err.message : 'Search failed.')
      setSearchResults([])
    } finally {
      setSearching(false)
    }
  }

  const tenantOptions = tenants.map((t) => (
    <option key={t.id} value={t.id}>
      {t.name}
    </option>
  ))

  return (
    <div className="p-8">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Business Context</h1>
        <p className="mt-1 text-sm text-gray-500">Ingest and search your business context data</p>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Left: Ingest */}
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="mb-4 text-base font-semibold text-gray-900">Ingest Context</h2>
          <form onSubmit={handleIngest} className="space-y-4">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700">
                Tenant
              </label>
              {tenants.length === 0 ? (
                <p className="text-sm text-gray-400">No tenants available. Seed demo data first.</p>
              ) : (
                <select
                  value={ingestTenantId}
                  onChange={(e) => {
                    setIngestTenantId(e.target.value)
                    setSelectedTenantId(e.target.value)
                  }}
                  required
                  className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                >
                  <option value="">Select a tenant</option>
                  {tenantOptions}
                </select>
              )}
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700">
                File <span className="font-normal text-gray-400">(optional — replaces text input)</span>
              </label>
              <div
                onDragOver={(e) => { e.preventDefault(); setIsDragging(true) }}
                onDragLeave={() => setIsDragging(false)}
                onDrop={handleFileDrop}
                onClick={() => fileInputRef.current?.click()}
                className={`flex cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed px-4 py-5 text-center transition ${
                  isDragging
                    ? 'border-blue-400 bg-blue-50'
                    : 'border-gray-200 hover:border-blue-300 hover:bg-gray-50'
                }`}
              >
                <svg className="mb-1.5 h-6 w-6 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5" />
                </svg>
                <p className="text-xs text-gray-500">Drop a file here, or click to browse</p>
                <input
                  ref={fileInputRef}
                  type="file"
                  className="hidden"
                  onChange={handleFileChange}
                  onClick={(e) => e.stopPropagation()}
                />
              </div>
              {selectedFile && (
                <div className="mt-2 flex items-center gap-2">
                  <span className="flex items-center gap-1.5 rounded-full bg-blue-100 px-2.5 py-1 text-xs font-medium text-blue-700">
                    <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M15.172 7l-6.586 6.586a2 2 0 102.828 2.828l6.414-6.586a4 4 0 00-5.656-5.656l-6.415 6.585a6 6 0 108.486 8.486L20.5 13" />
                    </svg>
                    {selectedFile.name}
                  </span>
                  <button
                    type="button"
                    onClick={() => {
                      setSelectedFile(null)
                      if (fileInputRef.current) fileInputRef.current.value = ''
                    }}
                    className="text-gray-400 hover:text-gray-600"
                  >
                    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                    </svg>
                  </button>
                </div>
              )}
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700">
                Text {selectedFile && <span className="font-normal text-gray-400">(optional when file selected)</span>}
              </label>
              <textarea
                value={ingestText}
                onChange={(e) => setIngestText(e.target.value)}
                required={!selectedFile}
                rows={5}
                placeholder="Enter the business context text to ingest..."
                className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>

            {!selectedFile && (
              <>
                <div>
                  <label className="mb-1.5 block text-sm font-medium text-gray-700">
                    Source
                  </label>
                  <input
                    type="text"
                    value={ingestSource}
                    onChange={(e) => setIngestSource(e.target.value)}
                    required
                    placeholder="e.g. confluence, slack, manual"
                    className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                  />
                </div>

                <div>
                  <label className="mb-1.5 block text-sm font-medium text-gray-700">
                    Category
                  </label>
                  <input
                    type="text"
                    value={ingestCategory}
                    onChange={(e) => setIngestCategory(e.target.value)}
                    required
                    placeholder="e.g. policy, product, technical"
                    className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                  />
                </div>
              </>
            )}

            <button
              type="submit"
              disabled={ingesting || !ingestTenantId}
              className="flex w-full items-center justify-center gap-2 rounded-lg bg-blue-600 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {ingesting ? (
                <>
                  <Spinner />
                  Ingesting…
                </>
              ) : (
                'Submit'
              )}
            </button>
          </form>

          {pendingResponse && (
            <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 p-4">
              <p className="mb-3 text-sm font-medium text-amber-900">
                {pendingResponse.confirmationMessage ?? `The agent suggests routing to artifact ${pendingResponse.suggestedArtifactId ?? 'unknown'} instead of your selection. Accept?`}
              </p>
              <div className="flex gap-2">
                <button
                  onClick={() => handleConfirm(true)}
                  disabled={confirming}
                  className="flex items-center gap-1.5 rounded-lg bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
                >
                  {confirming && <Spinner />}
                  Accept
                </button>
                <button
                  onClick={() => handleConfirm(false)}
                  disabled={confirming}
                  className="rounded-lg border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
                >
                  Keep Original
                </button>
              </div>
            </div>
          )}

          {ingestResult && (
            <div
              className={`mt-4 rounded-lg px-4 py-3 text-sm ${
                ingestResult.ok ? 'bg-emerald-50 text-emerald-800' : 'bg-red-50 text-red-800'
              }`}
            >
              {ingestResult.message}
            </div>
          )}

          {ingestResult?.ok && agentName && (
            <div className="mt-2 flex items-center gap-1.5">
              <span className="inline-flex items-center gap-1 rounded-full bg-violet-100 px-2.5 py-0.5 text-xs font-medium text-violet-700">
                <svg className="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9 3H5a2 2 0 00-2 2v4m6-6h10a2 2 0 012 2v4M9 3v18" />
                </svg>
                Handled by: {agentName}
              </span>
            </div>
          )}

          {ingestResult?.ok && lastIngest && (
            <div className="mt-2 flex flex-wrap items-center gap-1.5 rounded-lg border border-indigo-100 bg-indigo-50 px-4 py-3 text-sm text-indigo-800">
              <svg className="h-4 w-4 shrink-0 text-indigo-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
              </svg>
              <span>Routed to:</span>
              <span className="font-medium">{lastIngest.artifactName ?? 'Unknown artifact'}</span>
              <span className="text-indigo-400">·</span>
              <span>Department:</span>
              <span className="font-medium">{lastIngest.departments[0]?.name ?? 'Shared'}</span>
              {lastIngest.isShared && (
                <Badge variant="info">Shared</Badge>
              )}
            </div>
          )}
        </div>

        {/* Right: Search */}
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="mb-4 text-base font-semibold text-gray-900">Search Context</h2>
          <form onSubmit={handleSearch} className="space-y-4">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700">
                Tenant
              </label>
              {tenants.length === 0 ? (
                <p className="text-sm text-gray-400">No tenants available.</p>
              ) : (
                <select
                  value={searchTenantId}
                  onChange={(e) => {
                    setSearchTenantId(e.target.value)
                    setSelectedTenantId(e.target.value)
                  }}
                  required
                  className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                >
                  <option value="">Select a tenant</option>
                  {tenantOptions}
                </select>
              )}
            </div>

            {/* Filter by artifact */}
            {artifacts.length > 0 && (
              <div>
                <label className="mb-1.5 block text-sm font-medium text-gray-700">
                  Filter by artifact
                </label>
                <select
                  value={filterArtifactId}
                  onChange={(e) => setFilterArtifactId(e.target.value)}
                  className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                >
                  <option value="">All</option>
                  {artifacts.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name}
                    </option>
                  ))}
                </select>
              </div>
            )}

            {/* Filter by department */}
            {departmentsFromArtifacts.length > 0 && (
              <div>
                <label className="mb-1.5 block text-sm font-medium text-gray-700">
                  Filter by department
                </label>
                <select
                  value={filterDepartmentId}
                  onChange={(e) => setFilterDepartmentId(e.target.value)}
                  className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                >
                  <option value="">All</option>
                  {departmentsFromArtifacts.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </select>
              </div>
            )}

            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700">
                Query
              </label>
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                required
                placeholder="What are you looking for?"
                className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700">
                Top K results: <span className="font-semibold text-blue-600">{topK}</span>
              </label>
              <input
                type="range"
                min={1}
                max={20}
                value={topK}
                onChange={(e) => setTopK(Number(e.target.value))}
                className="w-full accent-blue-600"
              />
              <div className="mt-1 flex justify-between text-xs text-gray-400">
                <span>1</span>
                <span>20</span>
              </div>
            </div>

            <button
              type="submit"
              disabled={searching || !searchTenantId || !searchQuery.trim()}
              className="flex w-full items-center justify-center gap-2 rounded-lg bg-slate-800 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-slate-900 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {searching ? (
                <>
                  <Spinner />
                  Searching…
                </>
              ) : (
                'Search'
              )}
            </button>
          </form>

          {/* Results */}
          <div className="mt-5">
            {searching && (
              <div className="flex items-center justify-center py-8">
                <Spinner className="h-6 w-6 text-blue-500" />
              </div>
            )}

            {searchError && (
              <div className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800">
                {searchError}
              </div>
            )}

            {!searching && hasSearched && !searchError && (
              <>
                <p className="mb-3 text-xs text-gray-500">
                  {searchResults.length === 0
                    ? 'No results found.'
                    : `${searchResults.length} result${searchResults.length === 1 ? '' : 's'} found`}
                </p>
                <ul className="space-y-3">
                  {searchResults.map((r) => (
                    <li
                      key={r.id}
                      className="rounded-lg border border-gray-100 bg-gray-50 px-4 py-3"
                    >
                      <p className="text-sm text-gray-900">{r.text}</p>
                      <div className="mt-2 flex flex-wrap gap-2">
                        <span className="rounded-full bg-blue-100 px-2 py-0.5 text-xs text-blue-700">
                          {r.category}
                        </span>
                        <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600">
                          {r.source}
                        </span>
                        <span className="ml-auto text-xs text-gray-400">
                          {formatDate(r.createdAt)}
                        </span>
                      </div>
                    </li>
                  ))}
                </ul>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

function Spinner({ className = 'h-4 w-4 text-white' }: { className?: string }) {
  return (
    <svg className={`animate-spin ${className}`} fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
    </svg>
  )
}
