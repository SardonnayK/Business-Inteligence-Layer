import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import Badge from '../components/Badge'
import type { Tenant } from '../api/tenants'

interface Props {
  tenants: Tenant[]
  tenantsLoading: boolean
  selectedTenantId: string
  setSelectedTenantId: (id: string) => void
  refreshTenants: () => void
}

function formatDate(iso: string) {
  try {
    return new Date(iso).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    })
  } catch {
    return iso
  }
}

export default function TenantsPage({ tenants, tenantsLoading, setSelectedTenantId }: Props) {
  const navigate = useNavigate()
  const [search, setSearch] = useState('')

  const filtered = tenants.filter((t) =>
    t.name.toLowerCase().includes(search.toLowerCase()),
  )

  function handleRowClick(tenant: Tenant) {
    setSelectedTenantId(tenant.id)
    navigate('/projects')
  }

  return (
    <div className="p-8">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Tenants</h1>
          <p className="mt-1 text-sm text-gray-500">All registered tenants in the system</p>
        </div>
        <span className="rounded-full bg-slate-100 px-3 py-1 text-sm font-medium text-slate-700">
          {tenants.length} total
        </span>
      </div>

      {/* Search */}
      <div className="mb-4">
        <input
          type="text"
          placeholder="Search tenants..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="w-full max-w-sm rounded-lg border border-gray-200 px-4 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
      </div>

      <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
        {tenantsLoading ? (
          <div className="flex items-center justify-center py-16">
            <LoadingSpinner />
          </div>
        ) : filtered.length === 0 ? (
          <div className="py-16 text-center">
            <p className="text-sm text-gray-500">
              {search ? 'No tenants match your search.' : 'No tenants found. Try seeding demo data.'}
            </p>
          </div>
        ) : (
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">
                  Name
                </th>
                <th className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">
                  ID
                </th>
                <th className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">
                  Status
                </th>
                <th className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">
                  Created
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 bg-white">
              {filtered.map((tenant) => (
                <tr
                  key={tenant.id}
                  onClick={() => handleRowClick(tenant)}
                  className="cursor-pointer transition-colors hover:bg-blue-50"
                >
                  <td className="whitespace-nowrap px-6 py-4 text-sm font-medium text-gray-900">
                    {tenant.name}
                  </td>
                  <td className="whitespace-nowrap px-6 py-4 font-mono text-xs text-gray-400">
                    {tenant.id}
                  </td>
                  <td className="whitespace-nowrap px-6 py-4">
                    <Badge variant={tenant.isActive ? 'success' : 'neutral'}>
                      {tenant.isActive ? 'Active' : 'Inactive'}
                    </Badge>
                  </td>
                  <td className="whitespace-nowrap px-6 py-4 text-sm text-gray-500">
                    {formatDate(tenant.createdAt)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}

function LoadingSpinner() {
  return (
    <svg className="h-6 w-6 animate-spin text-blue-500" fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
    </svg>
  )
}
