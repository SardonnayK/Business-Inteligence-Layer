import { useState, useEffect, useCallback } from 'react'
import {
  listMcpIntegrations,
  createMcpIntegration,
  updateMcpIntegration,
  deleteMcpIntegration,
  discoverMcpTools,
  type McpIntegration,
  type McpToolSummary,
} from '../api/mcpIntegrations'

function Spinner({ className = 'h-4 w-4 text-white' }: { className?: string }) {
  return (
    <svg className={`animate-spin ${className}`} fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
    </svg>
  )
}

function Toggle({ checked, onChange }: { checked: boolean; onChange: () => void }) {
  return (
    <button
      type="button"
      onClick={onChange}
      className={`relative inline-flex h-5 w-9 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${
        checked ? 'bg-blue-600' : 'bg-gray-200'
      }`}
      role="switch"
      aria-checked={checked}
    >
      <span
        className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
          checked ? 'translate-x-4' : 'translate-x-0'
        }`}
      />
    </button>
  )
}

interface AddIntegrationModalProps {
  onClose: () => void
  onCreated: () => void
}

function AddIntegrationModal({ onClose, onCreated }: AddIntegrationModalProps) {
  const [name, setName] = useState('')
  const [serverUrl, setServerUrl] = useState('')
  const [apiKey, setApiKey] = useState('')
  const [authHeaderName, setAuthHeaderName] = useState('Authorization')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError('')
    try {
      await createMcpIntegration({
        name,
        serverUrl,
        apiKey: apiKey || undefined,
        authHeaderName: authHeaderName || 'Authorization',
      })
      onCreated()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to add integration.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-lg rounded-xl bg-white p-6 shadow-xl">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-base font-semibold text-gray-900">Add MCP Integration</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">Name</label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              placeholder="Company Gmail"
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">Server URL</label>
            <input
              type="url"
              value={serverUrl}
              onChange={(e) => setServerUrl(e.target.value)}
              required
              placeholder="https://my-mcp-server.example.com/mcp"
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">
              API Key <span className="font-normal text-gray-400">(optional)</span>
            </label>
            <input
              type="password"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder="sk-…"
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">Auth Header Name</label>
            <input
              type="text"
              value={authHeaderName}
              onChange={(e) => setAuthHeaderName(e.target.value)}
              placeholder="Authorization"
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          {error && <p className="text-sm text-red-600">{error}</p>}
          <div className="flex gap-3 pt-1">
            <button
              type="submit"
              disabled={saving}
              className="flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {saving && <Spinner />}
              {saving ? 'Adding…' : 'Add Integration'}
            </button>
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

interface ToolsModalProps {
  integrationName: string
  tools: McpToolSummary[]
  onClose: () => void
}

function ToolsModal({ integrationName, tools, onClose }: ToolsModalProps) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-lg rounded-xl bg-white p-6 shadow-xl">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-base font-semibold text-gray-900">Tools — {integrationName}</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        {tools.length === 0 ? (
          <p className="text-sm text-gray-500">No tools discovered yet.</p>
        ) : (
          <ul className="divide-y divide-gray-100">
            {tools.map((t) => (
              <li key={t.name} className="py-3">
                <p className="text-sm font-medium text-gray-900">{t.name}</p>
                {t.description && <p className="mt-0.5 text-xs text-gray-500">{t.description}</p>}
              </li>
            ))}
          </ul>
        )}
        <div className="mt-4">
          <button
            onClick={onClose}
            className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  )
}

function parseCachedTools(json: string | null): McpToolSummary[] {
  if (!json) return []
  try {
    return JSON.parse(json) as McpToolSummary[]
  } catch {
    return []
  }
}

export default function IntegrationsPage() {
  const [integrations, setIntegrations] = useState<McpIntegration[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [showAddModal, setShowAddModal] = useState(false)
  const [viewToolsFor, setViewToolsFor] = useState<McpIntegration | null>(null)
  const [discoveringId, setDiscoveringId] = useState<string | null>(null)
  const [togglingId, setTogglingId] = useState<string | null>(null)
  const [deletingId, setDeletingId] = useState<string | null>(null)

  const load = useCallback(() => {
    setLoading(true)
    listMcpIntegrations()
      .then(setIntegrations)
      .catch(() => setError('Failed to load integrations.'))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => { load() }, [load])

  async function handleDiscover(integ: McpIntegration) {
    setDiscoveringId(integ.id)
    setError('')
    try {
      const result = await discoverMcpTools(integ.id)
      setIntegrations((prev) =>
        prev.map((m) =>
          m.id === integ.id
            ? { ...m, cachedToolsJson: JSON.stringify(result.tools) }
            : m
        )
      )
      setViewToolsFor({ ...integ, cachedToolsJson: JSON.stringify(result.tools) })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Discovery failed.')
    } finally {
      setDiscoveringId(null)
    }
  }

  async function handleToggle(integ: McpIntegration) {
    setTogglingId(integ.id)
    try {
      const updated = await updateMcpIntegration(integ.id, { isEnabled: !integ.isEnabled })
      setIntegrations((prev) => prev.map((m) => (m.id === integ.id ? updated : m)))
    } catch {
      setError('Failed to toggle integration.')
    } finally {
      setTogglingId(null)
    }
  }

  async function handleDelete(integ: McpIntegration) {
    if (!window.confirm(`Delete integration "${integ.name}"? This cannot be undone.`)) return
    setDeletingId(integ.id)
    try {
      await deleteMcpIntegration(integ.id)
      setIntegrations((prev) => prev.filter((m) => m.id !== integ.id))
    } catch {
      setError('Failed to delete integration.')
    } finally {
      setDeletingId(null)
    }
  }

  return (
    <div className="p-6">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">MCP Integrations</h1>
          <p className="mt-1 text-sm text-gray-500">
            Connect external MCP servers. The Supervisor will call these when it needs additional context to answer a query.
          </p>
        </div>
        <button
          onClick={() => setShowAddModal(true)}
          className="flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
          </svg>
          Add Integration
        </button>
      </div>

      {error && (
        <div className="mb-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          {error}
        </div>
      )}

      <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
        <div className="border-b border-gray-100 px-6 py-4">
          <h2 className="text-sm font-semibold text-gray-900">Registered Integrations</h2>
        </div>

        {loading ? (
          <div className="flex items-center justify-center py-12">
            <Spinner className="h-6 w-6 text-blue-500" />
          </div>
        ) : integrations.length === 0 ? (
          <div className="px-6 py-12 text-center">
            <p className="text-sm text-gray-500">No MCP integrations registered yet.</p>
            <p className="mt-1 text-xs text-gray-400">
              Add an integration to let the Supervisor pull context from external sources during analysis.
            </p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 bg-gray-50">
                  <th className="px-4 py-3 text-left font-medium text-gray-700">Name</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-700">Server URL</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-700">Tools</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-700">Status</th>
                  <th className="px-4 py-3 text-right font-medium text-gray-700">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {integrations.map((integ) => {
                  const tools = parseCachedTools(integ.cachedToolsJson)
                  return (
                    <tr key={integ.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3">
                        <p className="font-medium text-gray-900">{integ.name}</p>
                        {integ.hasApiKey && (
                          <span className="mt-0.5 inline-block text-xs text-gray-400">API key set</span>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <span className="max-w-xs truncate font-mono text-xs text-gray-600">{integ.serverUrl}</span>
                      </td>
                      <td className="px-4 py-3">
                        {tools.length > 0 ? (
                          <button
                            onClick={() => setViewToolsFor(integ)}
                            className="text-xs text-blue-600 hover:underline"
                          >
                            {tools.length} tool{tools.length !== 1 ? 's' : ''}
                          </button>
                        ) : (
                          <span className="text-xs text-gray-400">Not discovered</span>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2">
                          <Toggle
                            checked={integ.isEnabled}
                            onChange={() => togglingId !== integ.id && handleToggle(integ)}
                          />
                          <span className={`text-xs ${integ.isEnabled ? 'text-green-700' : 'text-gray-400'}`}>
                            {togglingId === integ.id ? '…' : integ.isEnabled ? 'Enabled' : 'Disabled'}
                          </span>
                        </div>
                      </td>
                      <td className="px-4 py-3 text-right">
                        <div className="flex items-center justify-end gap-2">
                          <button
                            onClick={() => discoveringId !== integ.id && handleDiscover(integ)}
                            disabled={discoveringId === integ.id}
                            className="flex items-center gap-1 rounded px-2 py-1 text-xs font-medium text-purple-600 hover:bg-purple-50 disabled:opacity-50"
                          >
                            {discoveringId === integ.id && <Spinner className="h-3 w-3 text-purple-600" />}
                            {discoveringId === integ.id ? 'Discovering…' : 'Discover Tools'}
                          </button>
                          <button
                            onClick={() => handleDelete(integ)}
                            disabled={deletingId === integ.id}
                            className="rounded px-2 py-1 text-xs font-medium text-red-600 hover:bg-red-50 disabled:opacity-50"
                          >
                            {deletingId === integ.id ? '…' : 'Delete'}
                          </button>
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {showAddModal && (
        <AddIntegrationModal
          onClose={() => setShowAddModal(false)}
          onCreated={() => {
            setShowAddModal(false)
            load()
          }}
        />
      )}

      {viewToolsFor && (
        <ToolsModal
          integrationName={viewToolsFor.name}
          tools={parseCachedTools(viewToolsFor.cachedToolsJson)}
          onClose={() => setViewToolsFor(null)}
        />
      )}
    </div>
  )
}
