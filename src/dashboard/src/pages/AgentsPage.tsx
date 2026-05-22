import { useState, useEffect, useCallback } from 'react'
import {
  getAgents,
  getAgentConfig,
  updateAgentConfig,
  createAgent,
  updateAgent,
  deleteAgent,
  type AgentRegistration,
  type AgentConfig,
  type AgentCapability,
} from '../api/agents'

function Spinner({ className = 'h-4 w-4 text-white' }: { className?: string }) {
  return (
    <svg className={`animate-spin ${className}`} fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
    </svg>
  )
}

function CapabilityBadge({ capability }: { capability: AgentCapability }) {
  const map: Record<AgentCapability, string> = {
    Ingest: 'bg-emerald-100 text-emerald-700',
    Query: 'bg-blue-100 text-blue-700',
    Discover: 'bg-purple-100 text-purple-700',
    General: 'bg-gray-100 text-gray-700',
  }
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${map[capability]}`}>
      {capability}
    </span>
  )
}

function TypeBadge({ agentType }: { agentType: 'BuiltIn' | 'HttpPlugin' }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
        agentType === 'BuiltIn' ? 'bg-gray-100 text-gray-600' : 'bg-blue-100 text-blue-700'
      }`}
    >
      {agentType === 'BuiltIn' ? 'Built-in' : 'HTTP Plugin'}
    </span>
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

const CAPABILITY_OPTIONS: { label: string; value: number; key: AgentCapability }[] = [
  { label: 'Ingest', value: 0, key: 'Ingest' },
  { label: 'Query', value: 1, key: 'Query' },
  { label: 'General', value: 3, key: 'General' },
]

interface RegisterModalProps {
  onClose: () => void
  onCreated: () => void
}

function RegisterModal({ onClose, onCreated }: RegisterModalProps) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [capability, setCapability] = useState(0)
  const [httpEndpoint, setHttpEndpoint] = useState('')
  const [inputSchemaJson, setInputSchemaJson] = useState('')
  const [priority, setPriority] = useState(10)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError('')
    try {
      await createAgent({
        name,
        description,
        capability,
        httpEndpoint,
        inputSchemaJson: inputSchemaJson.trim() || undefined,
        priority,
      })
      onCreated()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to register agent.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-lg rounded-xl bg-white p-6 shadow-xl">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-base font-semibold text-gray-900">Register HTTP Plugin Agent</h2>
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
              placeholder="My Custom Agent"
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">Description</label>
            <input
              type="text"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              required
              placeholder="What does this agent do?"
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">Capability</label>
            <select
              value={capability}
              onChange={(e) => setCapability(Number(e.target.value))}
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            >
              {CAPABILITY_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">HTTP Endpoint URL</label>
            <input
              type="url"
              value={httpEndpoint}
              onChange={(e) => setHttpEndpoint(e.target.value)}
              required
              placeholder="https://my-agent.example.com/process"
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">
              Input Schema JSON <span className="font-normal text-gray-400">(optional)</span>
            </label>
            <textarea
              value={inputSchemaJson}
              onChange={(e) => setInputSchemaJson(e.target.value)}
              rows={3}
              placeholder='{"type":"object","properties":{"text":{"type":"string"}}}'
              className="w-full rounded-lg border border-gray-200 px-3 py-2 font-mono text-xs focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">Priority</label>
            <input
              type="number"
              value={priority}
              onChange={(e) => setPriority(Number(e.target.value))}
              min={1}
              max={100}
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
              {saving ? 'Registering…' : 'Register'}
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

interface EditAgentModalProps {
  agent: AgentRegistration
  onClose: () => void
  onSaved: () => void
}

function EditAgentModal({ agent, onClose, onSaved }: EditAgentModalProps) {
  const [httpEndpoint, setHttpEndpoint] = useState(agent.httpEndpoint ?? '')
  const [inputSchemaJson, setInputSchemaJson] = useState(agent.inputSchemaJson ?? '')
  const [priority, setPriority] = useState(agent.priority)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError('')
    try {
      await updateAgent(agent.id, {
        httpEndpoint: httpEndpoint || undefined,
        inputSchemaJson: inputSchemaJson.trim() || undefined,
        priority,
      })
      onSaved()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update agent.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-lg rounded-xl bg-white p-6 shadow-xl">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-base font-semibold text-gray-900">Edit Agent: {agent.name}</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">HTTP Endpoint URL</label>
            <input
              type="url"
              value={httpEndpoint}
              onChange={(e) => setHttpEndpoint(e.target.value)}
              placeholder="https://my-agent.example.com/process"
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">
              Input Schema JSON <span className="font-normal text-gray-400">(optional)</span>
            </label>
            <textarea
              value={inputSchemaJson}
              onChange={(e) => setInputSchemaJson(e.target.value)}
              rows={3}
              placeholder='{"type":"object","properties":{"text":{"type":"string"}}}'
              className="w-full rounded-lg border border-gray-200 px-3 py-2 font-mono text-xs focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">Priority</label>
            <input
              type="number"
              value={priority}
              onChange={(e) => setPriority(Number(e.target.value))}
              min={1}
              max={100}
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
              {saving ? 'Saving…' : 'Save Changes'}
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

export default function AgentsPage() {
  const [agents, setAgents] = useState<AgentRegistration[]>([])
  const [loadingAgents, setLoadingAgents] = useState(true)
  const [agentsError, setAgentsError] = useState('')

  const [config, setConfig] = useState<AgentConfig | null>(null)
  const [configSaving, setConfigSaving] = useState(false)

  const [showRegisterModal, setShowRegisterModal] = useState(false)
  const [editingAgent, setEditingAgent] = useState<AgentRegistration | null>(null)
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const [togglingId, setTogglingId] = useState<string | null>(null)

  const loadAgents = useCallback(() => {
    setLoadingAgents(true)
    getAgents()
      .then(setAgents)
      .catch(() => setAgentsError('Failed to load agents.'))
      .finally(() => setLoadingAgents(false))
  }, [])

  useEffect(() => {
    loadAgents()
    getAgentConfig()
      .then(setConfig)
      .catch(() => {})
  }, [loadAgents])

  async function handleToggleEnabled(agent: AgentRegistration) {
    setTogglingId(agent.id)
    try {
      const updated = await updateAgent(agent.id, { isEnabled: !agent.isEnabled })
      setAgents((prev) => prev.map((a) => (a.id === agent.id ? updated : a)))
    } catch {
      setAgentsError('Failed to toggle agent.')
    } finally {
      setTogglingId(null)
    }
  }

  async function handleDelete(agent: AgentRegistration) {
    if (!window.confirm(`Delete agent "${agent.name}"? This cannot be undone.`)) return
    setDeletingId(agent.id)
    try {
      await deleteAgent(agent.id)
      setAgents((prev) => prev.filter((a) => a.id !== agent.id))
    } catch {
      setAgentsError('Failed to delete agent.')
    } finally {
      setDeletingId(null)
    }
  }

  async function handleConfigToggle(field: keyof AgentConfig) {
    if (!config) return
    const next = { ...config, [field]: !config[field] }
    setConfig(next)
    setConfigSaving(true)
    try {
      const saved = await updateAgentConfig(next)
      setConfig(saved)
    } catch {
      setConfig(config)
    } finally {
      setConfigSaving(false)
    }
  }

  return (
    <div className="p-6">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Agents</h1>
          <p className="mt-1 text-sm text-gray-500">Manage registered agents and supervisor configuration.</p>
        </div>
        <button
          onClick={() => setShowRegisterModal(true)}
          className="flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
          </svg>
          Register HTTP Plugin Agent
        </button>
      </div>

      {agentsError && (
        <div className="mb-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          {agentsError}
        </div>
      )}

      <div className="mb-6 rounded-xl border border-gray-200 bg-white shadow-sm">
        <div className="border-b border-gray-100 px-6 py-4">
          <h2 className="text-sm font-semibold text-gray-900">Agent Registrations</h2>
        </div>
        {loadingAgents ? (
          <div className="flex items-center justify-center py-12">
            <Spinner className="h-6 w-6 text-blue-500" />
          </div>
        ) : agents.length === 0 ? (
          <div className="px-6 py-10 text-center">
            <p className="text-sm text-gray-500">No agents registered.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 bg-gray-50">
                  <th className="px-4 py-3 text-left font-medium text-gray-700">Name</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-700">Capability</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-700">Type</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-700">Status</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-700">Priority</th>
                  <th className="px-4 py-3 text-right font-medium text-gray-700">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {agents.map((agent) => (
                  <tr key={agent.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3">
                      <p className="font-medium text-gray-900">{agent.name}</p>
                      {agent.description && (
                        <p className="mt-0.5 text-xs text-gray-400 line-clamp-1">{agent.description}</p>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <CapabilityBadge capability={agent.capability} />
                    </td>
                    <td className="px-4 py-3">
                      <TypeBadge agentType={agent.agentType} />
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <Toggle
                          checked={agent.isEnabled}
                          onChange={() => togglingId !== agent.id && handleToggleEnabled(agent)}
                        />
                        <span className={`text-xs ${agent.isEnabled ? 'text-green-700' : 'text-gray-400'}`}>
                          {togglingId === agent.id ? '…' : agent.isEnabled ? 'Enabled' : 'Disabled'}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-gray-600">{agent.priority}</td>
                    <td className="px-4 py-3 text-right">
                      {agent.agentType === 'HttpPlugin' && (
                        <div className="flex items-center justify-end gap-2">
                          <button
                            onClick={() => setEditingAgent(agent)}
                            className="rounded px-2 py-1 text-xs font-medium text-blue-600 hover:bg-blue-50"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => handleDelete(agent)}
                            disabled={deletingId === agent.id}
                            className="rounded px-2 py-1 text-xs font-medium text-red-600 hover:bg-red-50 disabled:opacity-50"
                          >
                            {deletingId === agent.id ? '…' : 'Delete'}
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
        <div className="mb-4 flex items-center gap-2">
          <h2 className="text-sm font-semibold text-gray-900">Supervisor Configuration</h2>
          {configSaving && <Spinner className="h-3.5 w-3.5 text-blue-500" />}
        </div>
        {config === null ? (
          <p className="text-sm text-gray-400">Loading configuration…</p>
        ) : (
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium text-gray-900">Require confirmation before rerouting</p>
                <p className="text-xs text-gray-500">
                  Prompt users when the supervisor wants to route to a different artifact than selected.
                </p>
              </div>
              <Toggle
                checked={config.requireConfirmationForRerouting}
                onChange={() => handleConfigToggle('requireConfirmationForRerouting')}
              />
            </div>
            <div className="border-t border-gray-100" />
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium text-gray-900">Auto-ingest agent output</p>
                <p className="text-xs text-gray-500">
                  Automatically store agent output into the routed artifact's context store.
                </p>
              </div>
              <Toggle
                checked={config.autoIngestAgentOutput}
                onChange={() => handleConfigToggle('autoIngestAgentOutput')}
              />
            </div>
            <div className="border-t border-gray-100" />
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium text-gray-900">Allow auto-create artifacts</p>
                <p className="text-xs text-gray-500">
                  Let the supervisor create new departments and artifacts when no existing match is found.
                </p>
              </div>
              <Toggle
                checked={config.allowAutoCreateArtifacts}
                onChange={() => handleConfigToggle('allowAutoCreateArtifacts')}
              />
            </div>
          </div>
        )}
      </div>

      {showRegisterModal && (
        <RegisterModal
          onClose={() => setShowRegisterModal(false)}
          onCreated={() => {
            setShowRegisterModal(false)
            loadAgents()
          }}
        />
      )}

      {editingAgent && (
        <EditAgentModal
          agent={editingAgent}
          onClose={() => setEditingAgent(null)}
          onSaved={() => {
            setEditingAgent(null)
            loadAgents()
          }}
        />
      )}
    </div>
  )
}
