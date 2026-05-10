import { useState, useEffect } from 'react'
import { getHealth } from '../api/health'
import Badge from '../components/Badge'

type HealthData = Record<string, unknown>

function detectOverallStatus(data: HealthData): 'success' | 'warning' | 'danger' {
  const status =
    typeof data.status === 'string' ? data.status.toLowerCase() : ''
  if (status === 'healthy') return 'success'
  if (status === 'degraded') return 'warning'
  if (status === 'unhealthy') return 'danger'
  return 'success'
}

export default function HealthPage() {
  const [data, setData] = useState<HealthData | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastChecked, setLastChecked] = useState<Date | null>(null)

  async function fetchHealth() {
    setLoading(true)
    setError(null)
    try {
      const result = await getHealth()
      setData(result)
      setLastChecked(new Date())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Health check failed.')
      setData(null)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchHealth()
  }, [])

  return (
    <div className="p-8">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Health</h1>
          <p className="mt-1 text-sm text-gray-500">
            Live health status from <code className="rounded bg-gray-100 px-1 py-0.5 text-xs">/health</code>
          </p>
        </div>
        <div className="flex items-center gap-3">
          {lastChecked && (
            <span className="text-xs text-gray-400">
              Last checked: {lastChecked.toLocaleTimeString()}
            </span>
          )}
          <button
            onClick={fetchHealth}
            disabled={loading}
            className="flex items-center gap-2 rounded-lg border border-gray-200 bg-white px-4 py-2 text-sm font-medium text-gray-700 transition hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {loading ? (
              <>
                <Spinner />
                Refreshing…
              </>
            ) : (
              <>
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                Refresh
              </>
            )}
          </button>
        </div>
      </div>

      {loading && !data && (
        <div className="flex items-center justify-center py-16">
          <svg className="h-8 w-8 animate-spin text-blue-500" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
        </div>
      )}

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-6">
          <div className="flex items-center gap-2">
            <svg className="h-5 w-5 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <p className="text-sm font-medium text-red-800">API Unreachable</p>
          </div>
          <p className="mt-2 text-sm text-red-700">{error}</p>
          <p className="mt-1 text-xs text-red-500">
            Make sure the API is running at{' '}
            <code className="rounded bg-red-100 px-1">http://localhost:5000</code>
          </p>
        </div>
      )}

      {data && (
        <>
          {/* Summary card */}
          <div className="mb-6 flex items-center gap-4 rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
            <div
              className={`flex h-12 w-12 items-center justify-center rounded-full ${
                detectOverallStatus(data) === 'success'
                  ? 'bg-emerald-100'
                  : detectOverallStatus(data) === 'warning'
                  ? 'bg-amber-100'
                  : 'bg-red-100'
              }`}
            >
              {detectOverallStatus(data) === 'success' ? (
                <svg className="h-6 w-6 text-emerald-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                </svg>
              ) : (
                <svg className="h-6 w-6 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
                </svg>
              )}
            </div>
            <div>
              <p className="text-sm text-gray-500">Overall Status</p>
              <div className="mt-1">
                <Badge variant={detectOverallStatus(data)}>
                  {typeof data.status === 'string' ? data.status : 'OK'}
                </Badge>
              </div>
            </div>
          </div>

          {/* Raw JSON */}
          <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
            <div className="border-b border-gray-100 px-6 py-3">
              <h2 className="text-sm font-semibold text-gray-700">Raw Response</h2>
            </div>
            <div className="overflow-x-auto p-6">
              <pre className="text-xs leading-relaxed text-gray-700">
                {JSON.stringify(data, null, 2)}
              </pre>
            </div>
          </div>

          {/* Entries breakdown */}
          {typeof data.entries === 'object' && data.entries !== null && (
            <div className="mt-6 rounded-xl border border-gray-200 bg-white shadow-sm">
              <div className="border-b border-gray-100 px-6 py-3">
                <h2 className="text-sm font-semibold text-gray-700">Health Checks</h2>
              </div>
              <ul className="divide-y divide-gray-100">
                {Object.entries(data.entries as Record<string, unknown>).map(([key, val]) => {
                  const entry = val as Record<string, unknown>
                  const entryStatus =
                    typeof entry?.status === 'string' ? entry.status.toLowerCase() : ''
                  const variant: 'success' | 'warning' | 'danger' =
                    entryStatus === 'healthy'
                      ? 'success'
                      : entryStatus === 'degraded'
                      ? 'warning'
                      : 'danger'
                  return (
                    <li key={key} className="flex items-center justify-between px-6 py-4">
                      <div>
                        <p className="text-sm font-medium text-gray-900">{key}</p>
                        {typeof entry?.description === 'string' && (
                          <p className="text-xs text-gray-400">{entry.description}</p>
                        )}
                      </div>
                      <Badge variant={variant}>
                        {typeof entry?.status === 'string' ? entry.status : 'Unknown'}
                      </Badge>
                    </li>
                  )
                })}
              </ul>
            </div>
          )}
        </>
      )}
    </div>
  )
}

function Spinner() {
  return (
    <svg className="h-4 w-4 animate-spin text-gray-500" fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
    </svg>
  )
}
