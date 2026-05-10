import { useState, useEffect } from 'react'
import type { Tenant } from '../api/tenants'
import { ingestBusinessContext, searchBusinessContext } from '../api/businessContext'
import type { SearchResult } from '../api/businessContext'

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

  // Search state
  const [searchTenantId, setSearchTenantId] = useState(selectedTenantId)
  const [searchQuery, setSearchQuery] = useState('')
  const [topK, setTopK] = useState(5)
  const [searching, setSearching] = useState(false)
  const [searchResults, setSearchResults] = useState<SearchResult[]>([])
  const [searchError, setSearchError] = useState<string | null>(null)
  const [hasSearched, setHasSearched] = useState(false)

  useEffect(() => {
    if (selectedTenantId) {
      setIngestTenantId(selectedTenantId)
      setSearchTenantId(selectedTenantId)
    }
  }, [selectedTenantId])

  async function handleIngest(e: React.FormEvent) {
    e.preventDefault()
    if (!ingestTenantId) return
    setIngesting(true)
    setIngestResult(null)
    try {
      const result = await ingestBusinessContext(ingestTenantId, {
        text: ingestText,
        source: ingestSource,
        category: ingestCategory,
      })
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

  async function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    if (!searchTenantId || !searchQuery.trim()) return
    setSearching(true)
    setSearchError(null)
    setHasSearched(true)
    try {
      const results = await searchBusinessContext(searchTenantId, searchQuery, topK)
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
                Text
              </label>
              <textarea
                value={ingestText}
                onChange={(e) => setIngestText(e.target.value)}
                required
                rows={5}
                placeholder="Enter the business context text to ingest..."
                className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>

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

          {ingestResult && (
            <div
              className={`mt-4 rounded-lg px-4 py-3 text-sm ${
                ingestResult.ok ? 'bg-emerald-50 text-emerald-800' : 'bg-red-50 text-red-800'
              }`}
            >
              {ingestResult.message}
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
