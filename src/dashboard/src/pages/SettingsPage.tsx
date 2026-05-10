import { useState, useEffect, useCallback } from 'react'
import type { Tenant } from '../api/tenants'
import {
  getProviders,
  getSystemConfig,
  saveSystemConfig,
  getTenantConfig,
  saveTenantConfig,
  resetTenantConfig,
} from '../api/embeddingConfig'
import type { ProviderInfo, EmbeddingConfig, EmbeddingConfigUpdate } from '../api/embeddingConfig'

const PROVIDER_TYPE_MAP: Record<number, string> = {
  0: 'none',
  1: 'openai',
  2: 'ollama',
  3: 'azure-openai',
}

const PROVIDER_TYPE_REVERSE: Record<string, number> = {
  none: 0,
  openai: 1,
  ollama: 2,
  'azure-openai': 3,
}

interface SettingsPageProps {
  tenants: Tenant[]
  selectedTenantId: string
  setSelectedTenantId: (id: string) => void
  tenantsLoading: boolean
}

function Spinner({ className = 'text-white' }: { className?: string }) {
  return (
    <svg className={`h-4 w-4 animate-spin ${className}`} fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path
        className="opacity-75"
        fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
      />
    </svg>
  )
}

interface EmbeddingFormState {
  providerType: string  // string type key, e.g. "openai"
  modelId: string
  endpoint: string
  apiKey: string
}

function buildFormState(config: EmbeddingConfig): EmbeddingFormState {
  const typeKey = PROVIDER_TYPE_MAP[config.providerType] ?? 'none'
  return {
    providerType: typeKey,
    modelId: config.modelId ?? '',
    endpoint: config.endpoint ?? '',
    apiKey: '',
  }
}

function buildUpdatePayload(form: EmbeddingFormState): EmbeddingConfigUpdate {
  return {
    providerType: PROVIDER_TYPE_REVERSE[form.providerType] ?? 0,
    modelId: form.modelId,
    apiKey: form.apiKey.trim() !== '' ? form.apiKey : null,
    endpoint: form.endpoint.trim() !== '' ? form.endpoint : null,
  }
}

interface EmbeddingFormProps {
  providers: ProviderInfo[]
  form: EmbeddingFormState
  onChange: (next: EmbeddingFormState) => void
  onSave: () => void
  onReset?: () => void
  saving: boolean
  canReset?: boolean
  successMessage: string | null
  errorMessage: string | null
}

function EmbeddingForm({
  providers,
  form,
  onChange,
  onSave,
  onReset,
  saving,
  canReset = false,
  successMessage,
  errorMessage,
}: EmbeddingFormProps) {
  const currentProvider = providers.find((p) => p.type === form.providerType)

  function handleProviderChange(type: string) {
    const provider = providers.find((p) => p.type === type)
    onChange({
      ...form,
      providerType: type,
      modelId: provider?.defaultModel ?? '',
      endpoint: '',
      apiKey: '',
    })
  }

  return (
    <div className="space-y-4">
      {/* Provider selector */}
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">Provider</label>
        <select
          value={form.providerType}
          onChange={(e) => handleProviderChange(e.target.value)}
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        >
          {providers.map((p) => (
            <option key={p.type} value={p.type}>
              {p.name}
            </option>
          ))}
        </select>
      </div>

      {/* Model ID */}
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">Model ID</label>
        <input
          type="text"
          value={form.modelId}
          onChange={(e) => onChange({ ...form, modelId: e.target.value })}
          list={`model-list-${form.providerType}`}
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
          placeholder={currentProvider?.defaultModel ?? 'e.g. text-embedding-ada-002'}
        />
        {currentProvider && currentProvider.supportedModels.length > 0 && (
          <datalist id={`model-list-${form.providerType}`}>
            {currentProvider.supportedModels.map((m) => (
              <option key={m} value={m} />
            ))}
          </datalist>
        )}
      </div>

      {/* Endpoint URL — only when provider requires it */}
      {currentProvider?.requiresEndpoint && (
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Endpoint URL</label>
          <input
            type="url"
            value={form.endpoint}
            onChange={(e) => onChange({ ...form, endpoint: e.target.value })}
            className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            placeholder="https://your-resource.openai.azure.com/"
          />
        </div>
      )}

      {/* API Key — only when provider requires it */}
      {currentProvider?.requiresApiKey && (
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">API Key</label>
          <input
            type="password"
            value={form.apiKey}
            onChange={(e) => onChange({ ...form, apiKey: e.target.value })}
            className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            placeholder="Leave blank to keep existing key"
            autoComplete="new-password"
          />
        </div>
      )}

      {/* Success banner */}
      {successMessage && (
        <div className="bg-green-50 border border-green-200 text-green-800 text-sm rounded-md p-3">
          {successMessage}
        </div>
      )}

      {/* Error banner */}
      {errorMessage && (
        <div className="bg-red-50 border border-red-200 text-red-800 text-sm rounded-md p-3">
          {errorMessage}
        </div>
      )}

      {/* Actions */}
      <div className="flex items-center gap-3 pt-1">
        <button
          onClick={onSave}
          disabled={saving}
          className="flex items-center gap-2 bg-indigo-600 hover:bg-indigo-700 text-white px-4 py-2 rounded-md text-sm font-medium disabled:cursor-not-allowed disabled:opacity-60 transition-colors"
        >
          {saving && <Spinner />}
          {saving ? 'Saving…' : 'Save'}
        </button>

        {canReset && onReset && (
          <button
            onClick={onReset}
            disabled={saving}
            className="bg-red-50 hover:bg-red-100 text-red-700 border border-red-200 px-4 py-2 rounded-md text-sm font-medium disabled:cursor-not-allowed disabled:opacity-60 transition-colors"
          >
            Reset to system default
          </button>
        )}
      </div>
    </div>
  )
}

export default function SettingsPage({
  tenants,
  selectedTenantId,
  setSelectedTenantId,
  tenantsLoading,
}: SettingsPageProps) {
  // ── Shared provider list ──────────────────────────────────────────────────
  const [providers, setProviders] = useState<ProviderInfo[]>([])
  const [providersLoading, setProvidersLoading] = useState(true)
  const [providersError, setProvidersError] = useState<string | null>(null)

  useEffect(() => {
    getProviders()
      .then(setProviders)
      .catch((err) =>
        setProvidersError(err instanceof Error ? err.message : 'Failed to load providers'),
      )
      .finally(() => setProvidersLoading(false))
  }, [])

  // ── Section 1: System Default ─────────────────────────────────────────────
  const [systemConfig, setSystemConfig] = useState<EmbeddingConfig | null>(null)
  const [systemLoading, setSystemLoading] = useState(true)
  const [systemError, setSystemError] = useState<string | null>(null)
  const [systemForm, setSystemForm] = useState<EmbeddingFormState>({
    providerType: 'none',
    modelId: '',
    endpoint: '',
    apiKey: '',
  })
  const [systemSaving, setSystemSaving] = useState(false)
  const [systemSuccess, setSystemSuccess] = useState<string | null>(null)
  const [systemSaveError, setSystemSaveError] = useState<string | null>(null)

  useEffect(() => {
    setSystemLoading(true)
    setSystemError(null)
    getSystemConfig()
      .then((cfg) => {
        setSystemConfig(cfg)
        setSystemForm(buildFormState(cfg))
      })
      .catch((err) =>
        setSystemError(err instanceof Error ? err.message : 'Failed to load system config'),
      )
      .finally(() => setSystemLoading(false))
  }, [providers])

  async function handleSystemSave() {
    setSystemSaving(true)
    setSystemSuccess(null)
    setSystemSaveError(null)
    try {
      const updated = await saveSystemConfig(buildUpdatePayload(systemForm))
      setSystemConfig(updated)
      setSystemForm(buildFormState(updated))
      setSystemSuccess('System configuration saved successfully.')
      setTimeout(() => setSystemSuccess(null), 3000)
    } catch (err) {
      setSystemSaveError(err instanceof Error ? err.message : 'Save failed.')
    } finally {
      setSystemSaving(false)
    }
  }

  // ── Section 2: Per-Tenant Override ───────────────────────────────────────
  const [tenantConfig, setTenantConfig] = useState<EmbeddingConfig | null>(null)
  const [tenantConfigLoading, setTenantConfigLoading] = useState(false)
  const [tenantConfigError, setTenantConfigError] = useState<string | null>(null)
  const [tenantForm, setTenantForm] = useState<EmbeddingFormState>({
    providerType: 'none',
    modelId: '',
    endpoint: '',
    apiKey: '',
  })
  const [tenantSaving, setTenantSaving] = useState(false)
  const [tenantSuccess, setTenantSuccess] = useState<string | null>(null)
  const [tenantSaveError, setTenantSaveError] = useState<string | null>(null)

  const loadTenantConfig = useCallback(
    (tenantId: string) => {
      if (!tenantId) return
      setTenantConfigLoading(true)
      setTenantConfigError(null)
      setTenantSuccess(null)
      setTenantSaveError(null)
      getTenantConfig(tenantId)
        .then((cfg) => {
          setTenantConfig(cfg)
          setTenantForm(buildFormState(cfg))
        })
        .catch((err) =>
          setTenantConfigError(err instanceof Error ? err.message : 'Failed to load tenant config'),
        )
        .finally(() => setTenantConfigLoading(false))
    },
    [providers],
  )

  useEffect(() => {
    if (selectedTenantId && providers.length > 0) {
      loadTenantConfig(selectedTenantId)
    }
  }, [selectedTenantId, providers, loadTenantConfig])

  async function handleTenantSave() {
    if (!selectedTenantId) return
    setTenantSaving(true)
    setTenantSuccess(null)
    setTenantSaveError(null)
    try {
      const updated = await saveTenantConfig(selectedTenantId, buildUpdatePayload(tenantForm))
      setTenantConfig(updated)
      setTenantForm(buildFormState(updated))
      setTenantSuccess('Tenant configuration saved successfully.')
      setTimeout(() => setTenantSuccess(null), 3000)
    } catch (err) {
      setTenantSaveError(err instanceof Error ? err.message : 'Save failed.')
    } finally {
      setTenantSaving(false)
    }
  }

  async function handleTenantReset() {
    if (!selectedTenantId) return
    setTenantSaving(true)
    setTenantSuccess(null)
    setTenantSaveError(null)
    try {
      await resetTenantConfig(selectedTenantId)
      // Reload to reflect the now-defaulted state
      loadTenantConfig(selectedTenantId)
      setTenantSuccess('Tenant override removed. Now using system default.')
      setTimeout(() => setTenantSuccess(null), 3000)
    } catch (err) {
      setTenantSaveError(err instanceof Error ? err.message : 'Reset failed.')
    } finally {
      setTenantSaving(false)
    }
  }

  // ─────────────────────────────────────────────────────────────────────────

  return (
    <div className="p-8">
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-gray-900">Settings</h1>
        <p className="mt-1 text-sm text-gray-500">
          Configure embedding providers for the system and individual tenants.
        </p>
      </div>

      {/* Provider loading/error state */}
      {providersLoading && (
        <div className="flex items-center gap-2 text-sm text-gray-500 mb-6">
          <Spinner className="text-gray-400" />
          Loading providers…
        </div>
      )}
      {providersError && (
        <div className="bg-red-50 border border-red-200 text-red-800 text-sm rounded-md p-3 mb-6">
          Failed to load provider list: {providersError}
        </div>
      )}

      {/* ── Section 1: System Default Provider ─────────────────────────────── */}
      {!providersLoading && !providersError && (
        <div className="bg-white rounded-lg shadow p-6 mb-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-4">System Default Provider</h2>

          {systemLoading ? (
            <div className="flex items-center gap-2 text-sm text-gray-500">
              <Spinner className="text-gray-400" />
              Loading system configuration…
            </div>
          ) : systemError ? (
            <div className="bg-red-50 border border-red-200 text-red-800 text-sm rounded-md p-3">
              {systemError}
            </div>
          ) : (
            <>
              {systemConfig?.updatedAt && (
                <p className="text-xs text-gray-400 mb-4">
                  Last updated:{' '}
                  {new Date(systemConfig.updatedAt).toLocaleString()}
                </p>
              )}
              <EmbeddingForm
                providers={providers}
                form={systemForm}
                onChange={setSystemForm}
                onSave={handleSystemSave}
                saving={systemSaving}
                successMessage={systemSuccess}
                errorMessage={systemSaveError}
              />
            </>
          )}
        </div>
      )}

      {/* ── Section 2: Per-Tenant Provider Override ─────────────────────────── */}
      {!providersLoading && !providersError && (
        <div className="bg-white rounded-lg shadow p-6 mb-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-4">Per-Tenant Provider Override</h2>

          {/* Tenant selector */}
          <div className="mb-5">
            <label className="block text-sm font-medium text-gray-700 mb-1">Tenant</label>
            {tenantsLoading ? (
              <div className="flex items-center gap-2 text-sm text-gray-500">
                <Spinner className="text-gray-400" />
                Loading tenants…
              </div>
            ) : tenants.length === 0 ? (
              <p className="text-sm text-gray-400">No tenants available.</p>
            ) : (
              <select
                value={selectedTenantId}
                onChange={(e) => setSelectedTenantId(e.target.value)}
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              >
                <option value="" disabled>
                  Select a tenant…
                </option>
                {tenants.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name}
                  </option>
                ))}
              </select>
            )}
          </div>

          {/* Tenant config */}
          {selectedTenantId && (
            <>
              {tenantConfigLoading ? (
                <div className="flex items-center gap-2 text-sm text-gray-500">
                  <Spinner className="text-gray-400" />
                  Loading tenant configuration…
                </div>
              ) : tenantConfigError ? (
                <div className="bg-red-50 border border-red-200 text-red-800 text-sm rounded-md p-3">
                  {tenantConfigError}
                </div>
              ) : tenantConfig ? (
                <>
                  {tenantConfig.isDefault && (
                    <div className="bg-blue-50 border border-blue-200 text-blue-800 text-sm rounded-md p-3 mb-4">
                      Using system default — save below to create a tenant override.
                    </div>
                  )}
                  {tenantConfig.updatedAt && !tenantConfig.isDefault && (
                    <p className="text-xs text-gray-400 mb-4">
                      Last updated:{' '}
                      {new Date(tenantConfig.updatedAt).toLocaleString()}
                    </p>
                  )}
                  <EmbeddingForm
                    providers={providers}
                    form={tenantForm}
                    onChange={setTenantForm}
                    onSave={handleTenantSave}
                    onReset={handleTenantReset}
                    saving={tenantSaving}
                    canReset={!tenantConfig.isDefault}
                    successMessage={tenantSuccess}
                    errorMessage={tenantSaveError}
                  />
                </>
              ) : null}
            </>
          )}
        </div>
      )}
    </div>
  )
}
