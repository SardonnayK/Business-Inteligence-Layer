import { useState, useEffect } from 'react'
import type { Tenant } from '../api/tenants'
import {
  getDepartments,
  getArtifacts,
  discoverDepartments,
  getArtifactContexts,
  clearArtifactContexts,
} from '../api/knowledge'
import type { Department, Artifact, ContextChunk } from '../api/knowledge'
import Badge from '../components/Badge'

interface Props {
  tenants: Tenant[]
  selectedTenantId: string
  setSelectedTenantId: (id: string) => void
  tenantsLoading: boolean
}

function formatDate(iso: string) {
  try {
    return new Date(iso).toLocaleString()
  } catch {
    return iso
  }
}

function Spinner({ className = 'h-4 w-4 text-white' }: { className?: string }) {
  return (
    <svg className={`animate-spin ${className}`} fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
    </svg>
  )
}

function sizeBadgeVariant(size: Department['estimatedSize']): 'info' | 'warning' | 'success' | 'neutral' {
  switch (size) {
    case 'small': return 'info'
    case 'medium': return 'warning'
    case 'large': return 'success'
    default: return 'neutral'
  }
}

interface ArtifactRowProps {
  artifact: Artifact
  isShared?: boolean
  onClearSuccess: (artifactId: string) => void
}

function ArtifactRow({ artifact, isShared, onClearSuccess }: ArtifactRowProps) {
  const [expanded, setExpanded] = useState(false)
  const [chunks, setChunks] = useState<ContextChunk[]>([])
  const [loadingChunks, setLoadingChunks] = useState(false)
  const [chunksFetched, setChunksFetched] = useState(false)
  const [chunkCount, setChunkCount] = useState(artifact.chunkCount)
  const [clearing, setClearing] = useState(false)

  async function handleToggle() {
    if (!expanded && !chunksFetched) {
      setLoadingChunks(true)
      try {
        const data = await getArtifactContexts(artifact.id)
        setChunks(data)
        setChunksFetched(true)
      } catch {
        // silently fail — expanded state will show empty
      } finally {
        setLoadingChunks(false)
      }
    }
    setExpanded((prev) => !prev)
  }

  async function handleClear() {
    const confirmed = window.confirm(
      `Delete all ${chunkCount} chunks from '${artifact.name}'? They will need to be re-ingested.`,
    )
    if (!confirmed) return
    setClearing(true)
    try {
      await clearArtifactContexts(artifact.id)
      setChunkCount(0)
      setChunks([])
      setChunksFetched(false)
      setExpanded(false)
      onClearSuccess(artifact.id)
    } catch {
      // leave state unchanged on error
    } finally {
      setClearing(false)
    }
  }

  const rowBase =
    'flex items-center justify-between px-4 py-3 border rounded-lg mb-1 hover:bg-gray-50'
  const rowClass = isShared
    ? `${rowBase} border-indigo-200 bg-indigo-50`
    : `${rowBase} bg-white border-gray-200`

  return (
    <div>
      <div className={rowClass}>
        <div className="flex min-w-0 flex-1 items-center gap-3">
          {isShared && (
            <span className="shrink-0 rounded-full bg-indigo-100 px-2 py-0.5 text-xs font-medium text-indigo-700">
              Shared
            </span>
          )}
          <div className="min-w-0">
            <p className="truncate text-sm font-medium text-gray-900">{artifact.name}</p>
            {artifact.description && (
              <p className="truncate text-xs text-gray-500">{artifact.description}</p>
            )}
          </div>
        </div>
        <div className="ml-4 flex shrink-0 items-center gap-3">
          <span className="text-xs text-gray-500">{chunkCount} chunks</span>
          <button
            type="button"
            onClick={handleToggle}
            title={expanded ? 'Collapse' : 'Expand'}
            className="rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600"
          >
            {loadingChunks ? (
              <Spinner className="h-4 w-4 text-gray-500" />
            ) : (
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M7 16V4m0 0L3 8m4-4l4 4M17 8v12m0 0l4-4m-4 4l-4-4" />
              </svg>
            )}
          </button>
          {!isShared && (
            <button
              type="button"
              onClick={handleClear}
              disabled={clearing || chunkCount === 0}
              title="Clear all chunks"
              className="rounded p-1 text-gray-400 hover:bg-red-50 hover:text-red-600 disabled:cursor-not-allowed disabled:opacity-40"
            >
              {clearing ? (
                <Spinner className="h-4 w-4 text-red-400" />
              ) : (
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                </svg>
              )}
            </button>
          )}
        </div>
      </div>

      {expanded && (
        <div className="bg-gray-50 border border-gray-100 rounded-b-lg px-4 py-3 -mt-1 mb-1 space-y-2">
          {chunks.length === 0 ? (
            <p className="text-xs text-gray-400">No chunks available.</p>
          ) : (
            chunks.map((chunk) => (
              <div key={chunk.id} className="rounded border border-gray-200 bg-white px-3 py-2">
                <p className="text-sm text-gray-700">
                  {chunk.text.length > 120 ? `${chunk.text.slice(0, 120)}…` : chunk.text}
                </p>
                <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                  {chunk.source && <Badge variant="info">{chunk.source}</Badge>}
                  {chunk.category && <Badge variant="neutral">{chunk.category}</Badge>}
                  <span className="ml-auto text-xs text-gray-400">{formatDate(chunk.createdAt)}</span>
                </div>
              </div>
            ))
          )}
        </div>
      )}
    </div>
  )
}

export default function KnowledgePage({
  tenants,
  selectedTenantId,
  setSelectedTenantId,
  tenantsLoading,
}: Props) {
  const [departments, setDepartments] = useState<Department[]>([])
  const [artifacts, setArtifacts] = useState<Artifact[]>([])
  const [loadingData, setLoadingData] = useState(false)
  const [dataError, setDataError] = useState<string | null>(null)

  const [discovering, setDiscovering] = useState(false)
  const [discoveryBanner, setDiscoveryBanner] = useState<{
    departments: number
    artifacts: number
    wasAiAssisted: boolean
  } | null>(null)
  const [discoveryError, setDiscoveryError] = useState<string | null>(null)

  // Tracks cleared artifacts so we can update chunkCount in artifact list
  const [, setClearedArtifactIds] = useState<Set<string>>(new Set())

  useEffect(() => {
    if (!selectedTenantId) return
    setLoadingData(true)
    setDataError(null)
    setDiscoveryBanner(null)
    Promise.all([getDepartments(selectedTenantId), getArtifacts(selectedTenantId)])
      .then(([deps, arts]) => {
        setDepartments(deps)
        setArtifacts(arts)
      })
      .catch((err) => {
        setDataError(err instanceof Error ? err.message : 'Failed to load knowledge data.')
      })
      .finally(() => setLoadingData(false))
  }, [selectedTenantId])

  async function handleDiscover() {
    if (!selectedTenantId) return
    setDiscovering(true)
    setDiscoveryError(null)
    setDiscoveryBanner(null)
    try {
      const result = await discoverDepartments(selectedTenantId)
      setDiscoveryBanner({
        departments: result.departments.length,
        artifacts: result.artifacts.length,
        wasAiAssisted: result.wasAiAssisted,
      })
      // Refresh lists after discovery
      const [deps, arts] = await Promise.all([
        getDepartments(selectedTenantId),
        getArtifacts(selectedTenantId),
      ])
      setDepartments(deps)
      setArtifacts(arts)
    } catch (err) {
      setDiscoveryError(err instanceof Error ? err.message : 'Discovery failed.')
    } finally {
      setDiscovering(false)
    }
  }

  function handleClearSuccess(artifactId: string) {
    setClearedArtifactIds((prev) => new Set(prev).add(artifactId))
    setArtifacts((prev) =>
      prev.map((a) => (a.id === artifactId ? { ...a, chunkCount: 0 } : a)),
    )
  }

  const sharedArtifacts = artifacts.filter((a) => a.isShared)
  const unsharedArtifacts = artifacts.filter((a) => !a.isShared)

  // Group non-shared artifacts by departmentId
  const artifactsByDept = new Map<string | null, Artifact[]>()
  for (const art of unsharedArtifacts) {
    const key = art.departmentId ?? null
    if (!artifactsByDept.has(key)) artifactsByDept.set(key, [])
    artifactsByDept.get(key)!.push(art)
  }

  const hasContent = sharedArtifacts.length > 0 || unsharedArtifacts.length > 0 || departments.length > 0

  return (
    <div className="p-8">
      {/* Page header */}
      <div className="mb-6 flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Knowledge</h1>
          <p className="mt-1 text-sm text-gray-500">Browse and manage your organisation's knowledge artifacts</p>
        </div>
        <div className="flex items-center gap-3">
          {tenantsLoading ? null : (
            <select
              value={selectedTenantId}
              onChange={(e) => setSelectedTenantId(e.target.value)}
              className="rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            >
              <option value="">Select tenant</option>
              {tenants.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
            </select>
          )}
          <button
            type="button"
            onClick={handleDiscover}
            disabled={discovering || !selectedTenantId}
            className="flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {discovering ? (
              <>
                <Spinner />
                Discovering…
              </>
            ) : (
              <>
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-4.35-4.35M17 11A6 6 0 1 1 5 11a6 6 0 0 1 12 0z" />
                </svg>
                Discover Departments
              </>
            )}
          </button>
        </div>
      </div>

      {/* Discovery result banner */}
      {discoveryBanner && (
        <div className="mb-4 rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
          Discovery complete — {discoveryBanner.departments} department{discoveryBanner.departments !== 1 ? 's' : ''},{' '}
          {discoveryBanner.artifacts} artifact{discoveryBanner.artifacts !== 1 ? 's' : ''} created.{' '}
          AI-assisted: {discoveryBanner.wasAiAssisted ? 'yes' : 'no'}
        </div>
      )}

      {/* Discovery error */}
      {discoveryError && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800">
          {discoveryError}
        </div>
      )}

      {/* Data error */}
      {dataError && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800">
          {dataError}
        </div>
      )}

      {/* Loading */}
      {loadingData && (
        <div className="flex items-center justify-center py-16">
          <Spinner className="h-6 w-6 text-blue-500" />
        </div>
      )}

      {/* Empty state */}
      {!loadingData && !dataError && !hasContent && (
        <div className="rounded-xl border border-dashed border-gray-300 bg-white py-16 text-center">
          <svg
            className="mx-auto mb-3 h-10 w-10 text-gray-300"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={1.5}
          >
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 6.042A8.967 8.967 0 006 3.75c-1.052 0-2.062.18-3 .512v14.25A8.987 8.987 0 016 18c2.305 0 4.408.867 6 2.292m0-14.25a8.966 8.966 0 016-2.292c1.052 0 2.062.18 3 .512v14.25A8.987 8.987 0 0018 18a8.967 8.967 0 00-6 2.292m0-14.25v14.25" />
          </svg>
          <p className="text-sm font-medium text-gray-600">No departments discovered yet.</p>
          <p className="mt-1 text-sm text-gray-400">
            Click "Discover Departments" to analyse existing knowledge,
            <br />
            or start ingesting text from the Business Context page.
          </p>
        </div>
      )}

      {/* Tree */}
      {!loadingData && !dataError && hasContent && (
        <div>
          {/* Shared artifacts at the top */}
          {sharedArtifacts.map((artifact) => (
            <ArtifactRow
              key={artifact.id}
              artifact={artifact}
              isShared
              onClearSuccess={handleClearSuccess}
            />
          ))}

          {/* Departments with their artifacts */}
          {departments.map((dept) => {
            const deptArtifacts = artifactsByDept.get(dept.id) ?? []
            return (
              <div key={dept.id}>
                <div className="text-sm font-semibold text-gray-700 uppercase tracking-wide mt-4 mb-1 flex items-center gap-2">
                  <span>{dept.name}</span>
                  <Badge variant={sizeBadgeVariant(dept.estimatedSize)}>{dept.estimatedSize}</Badge>
                  <span className="text-xs font-normal normal-case text-gray-400">
                    [{deptArtifacts.length} artifact{deptArtifacts.length !== 1 ? 's' : ''}]
                  </span>
                </div>
                {deptArtifacts.length === 0 ? (
                  <p className="mb-2 text-xs text-gray-400 pl-2">No artifacts in this department.</p>
                ) : (
                  deptArtifacts.map((artifact) => (
                    <ArtifactRow
                      key={artifact.id}
                      artifact={artifact}
                      onClearSuccess={handleClearSuccess}
                    />
                  ))
                )}
              </div>
            )
          })}

          {/* Artifacts not belonging to any department (non-shared, null departmentId, not in departments list) */}
          {(() => {
            const departmentIds = new Set(departments.map((d) => d.id))
            const orphaned = unsharedArtifacts.filter(
              (a) => a.departmentId === null || !departmentIds.has(a.departmentId),
            )
            // Remove ones already rendered under their dept
            const renderedIds = new Set(
              departments.flatMap((d) => (artifactsByDept.get(d.id) ?? []).map((a) => a.id)),
            )
            const truly_orphaned = orphaned.filter((a) => !renderedIds.has(a.id))
            if (truly_orphaned.length === 0) return null
            return (
              <div>
                <div className="text-sm font-semibold text-gray-700 uppercase tracking-wide mt-4 mb-1 flex items-center gap-2">
                  <span>Uncategorised</span>
                </div>
                {truly_orphaned.map((artifact) => (
                  <ArtifactRow
                    key={artifact.id}
                    artifact={artifact}
                    onClearSuccess={handleClearSuccess}
                  />
                ))}
              </div>
            )
          })()}
        </div>
      )}
    </div>
  )
}
