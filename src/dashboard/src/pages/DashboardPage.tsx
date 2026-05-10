import { useState } from 'react'
import StatCard from '../components/StatCard'
import Badge from '../components/Badge'
import type { Tenant } from '../api/tenants'
import { apiFetch } from '../api/client'
import { getHealth } from '../api/health'

interface Props {
  tenants: Tenant[]
  tenantsLoading: boolean
  refreshTenants: () => void
  selectedTenantId: string
  setSelectedTenantId: (id: string) => void
}

export default function DashboardPage({ tenants, tenantsLoading, refreshTenants }: Props) {
  const [seeding, setSeeding] = useState(false)
  const [seedResult, setSeedResult] = useState<{ ok: boolean; message: string } | null>(null)
  const [healthStatus, setHealthStatus] = useState<string | null>(null)
  const [healthChecking, setHealthChecking] = useState(false)

  const activeTenants = tenants.filter((t) => t.isActive).length

  async function handleSeed() {
    setSeeding(true)
    setSeedResult(null)
    try {
      await apiFetch('/api/dev/seed', { method: 'POST' })
      setSeedResult({ ok: true, message: 'Demo data seeded successfully.' })
      refreshTenants()
    } catch (err) {
      setSeedResult({
        ok: false,
        message: err instanceof Error ? err.message : 'Seed failed.',
      })
    } finally {
      setSeeding(false)
    }
  }

  async function handleHealthCheck() {
    setHealthChecking(true)
    setHealthStatus(null)
    try {
      const data = await getHealth()
      const status =
        typeof data === 'object' && data !== null && 'status' in data
          ? String((data as Record<string, unknown>).status)
          : 'OK'
      setHealthStatus(status)
    } catch {
      setHealthStatus('Unreachable')
    } finally {
      setHealthChecking(false)
    }
  }

  return (
    <div className="p-8">
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-gray-900">Dashboard</h1>
        <p className="mt-1 text-sm text-gray-500">Overview of your Business Intelligence Layer</p>
      </div>

      {/* Stat cards */}
      <div className="mb-8 grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
        <StatCard
          label="Total Tenants"
          value={tenantsLoading ? '...' : tenants.length}
          subtext="registered tenants"
          icon={
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          }
        />
        <StatCard
          label="Active Tenants"
          value={tenantsLoading ? '...' : activeTenants}
          subtext="currently active"
          icon={
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          }
        />
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <div className="flex items-start justify-between">
            <div>
              <p className="text-sm font-medium text-gray-500">API Health</p>
              <div className="mt-2">
                {healthChecking ? (
                  <span className="text-sm text-gray-400">Checking...</span>
                ) : healthStatus ? (
                  <Badge
                    variant={
                      healthStatus.toLowerCase() === 'healthy' || healthStatus === 'OK'
                        ? 'success'
                        : 'danger'
                    }
                  >
                    {healthStatus}
                  </Badge>
                ) : (
                  <button
                    onClick={handleHealthCheck}
                    className="text-sm text-blue-600 underline hover:text-blue-700"
                  >
                    Check now
                  </button>
                )}
              </div>
            </div>
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-slate-100 text-slate-600">
              <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z" />
              </svg>
            </div>
          </div>
        </div>
      </div>

      {/* Quick Actions */}
      <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
        <h2 className="mb-4 text-base font-semibold text-gray-900">Quick Actions</h2>
        <div className="flex flex-wrap gap-3">
          <button
            onClick={handleSeed}
            disabled={seeding}
            className="flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {seeding ? (
              <>
                <Spinner />
                Seeding…
              </>
            ) : (
              <>
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
                </svg>
                Seed Demo Data
              </>
            )}
          </button>
          <button
            onClick={handleHealthCheck}
            disabled={healthChecking}
            className="flex items-center gap-2 rounded-lg border border-gray-200 bg-white px-4 py-2.5 text-sm font-medium text-gray-700 transition hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {healthChecking ? (
              <>
                <Spinner className="text-gray-500" />
                Checking…
              </>
            ) : (
              <>
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                Check Health
              </>
            )}
          </button>
        </div>

        {seedResult && (
          <div
            className={`mt-4 rounded-lg px-4 py-3 text-sm ${
              seedResult.ok
                ? 'bg-emerald-50 text-emerald-800'
                : 'bg-red-50 text-red-800'
            }`}
          >
            {seedResult.message}
          </div>
        )}
      </div>

      {/* Recent tenants */}
      {tenants.length > 0 && (
        <div className="mt-6 rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="mb-4 text-base font-semibold text-gray-900">Recent Tenants</h2>
          <ul className="divide-y divide-gray-100">
            {tenants.slice(0, 5).map((t) => (
              <li key={t.id} className="flex items-center justify-between py-3">
                <div>
                  <p className="text-sm font-medium text-gray-900">{t.name}</p>
                  <p className="text-xs text-gray-400">{t.id}</p>
                </div>
                <Badge variant={t.isActive ? 'success' : 'neutral'}>
                  {t.isActive ? 'Active' : 'Inactive'}
                </Badge>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}

function Spinner({ className = 'text-white' }: { className?: string }) {
  return (
    <svg
      className={`h-4 w-4 animate-spin ${className}`}
      fill="none"
      viewBox="0 0 24 24"
    >
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path
        className="opacity-75"
        fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
      />
    </svg>
  )
}
