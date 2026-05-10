import { useState, useEffect, useCallback } from 'react'
import {
  getUsers,
  createUser,
  updateUser,
  getUserPermissions,
  updateUserPermissions,
  type TenantUserItem,
  type ArtifactPermission,
} from '../api/users'

function StatusBadge({ isActive }: { isActive: boolean }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
        isActive ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'
      }`}
    >
      {isActive ? 'Active' : 'Inactive'}
    </span>
  )
}

interface PermissionsPanelProps {
  userId: string
  username: string
  onClose: () => void
}

function PermissionsPanel({ userId, username, onClose }: PermissionsPanelProps) {
  const [artifacts, setArtifacts] = useState<ArtifactPermission[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    getUserPermissions(userId)
      .then(setArtifacts)
      .catch(() => setError('Failed to load permissions.'))
      .finally(() => setLoading(false))
  }, [userId])

  function toggle(id: string, field: 'canRead' | 'canWrite') {
    setArtifacts((prev) =>
      prev.map((a) => (a.id === id ? { ...a, [field]: !a[field] } : a)),
    )
    setSaved(false)
  }

  async function save() {
    setSaving(true)
    setError('')
    try {
      await updateUserPermissions(
        userId,
        artifacts.map((a) => ({ artifactId: a.id, canRead: a.canRead, canWrite: a.canWrite })),
      )
      setSaved(true)
      setTimeout(() => setSaved(false), 3000)
    } catch {
      setError('Failed to save permissions.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="rounded-lg border border-blue-200 bg-blue-50 p-4">
      <div className="mb-3 flex items-center justify-between">
        <h3 className="text-sm font-semibold text-gray-900">
          Permissions for <span className="text-blue-700">{username}</span>
        </h3>
        <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      {loading ? (
        <p className="text-sm text-gray-500">Loading…</p>
      ) : (
        <>
          <div className="overflow-hidden rounded border border-gray-200 bg-white">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-200 bg-gray-50">
                  <th className="px-3 py-2 text-left font-medium text-gray-700">Artifact</th>
                  <th className="px-3 py-2 text-left font-medium text-gray-700">Department</th>
                  <th className="px-3 py-2 text-center font-medium text-gray-700">Read</th>
                  <th className="px-3 py-2 text-center font-medium text-gray-700">Write</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {artifacts.map((a) => (
                  <tr key={a.id}>
                    <td className="px-3 py-2 text-gray-900">
                      {a.name}
                      {a.isShared && (
                        <span className="ml-1.5 rounded bg-purple-100 px-1 py-0.5 text-xs text-purple-700">
                          shared
                        </span>
                      )}
                    </td>
                    <td className="px-3 py-2 text-gray-500">{a.departmentName ?? '—'}</td>
                    <td className="px-3 py-2 text-center">
                      <input
                        type="checkbox"
                        checked={a.canRead}
                        onChange={() => toggle(a.id, 'canRead')}
                        className="h-4 w-4 rounded border-gray-300 text-blue-600"
                      />
                    </td>
                    <td className="px-3 py-2 text-center">
                      <input
                        type="checkbox"
                        checked={a.canWrite}
                        onChange={() => toggle(a.id, 'canWrite')}
                        className="h-4 w-4 rounded border-gray-300 text-blue-600"
                      />
                    </td>
                  </tr>
                ))}
                {artifacts.length === 0 && (
                  <tr>
                    <td colSpan={4} className="px-3 py-4 text-center text-gray-400">
                      No artifacts found for this tenant.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          {error && <p className="mt-2 text-sm text-red-600">{error}</p>}
          {saved && (
            <p className="mt-2 text-sm text-green-700">Permissions saved successfully.</p>
          )}

          <div className="mt-3 flex justify-end">
            <button
              onClick={save}
              disabled={saving}
              className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {saving ? 'Saving…' : 'Save Permissions'}
            </button>
          </div>
        </>
      )}
    </div>
  )
}

export default function UsersPage() {
  const [users, setUsers] = useState<TenantUserItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [showAddForm, setShowAddForm] = useState(false)
  const [newUsername, setNewUsername] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [newRole, setNewRole] = useState<'Admin' | 'Member'>('Member')
  const [addError, setAddError] = useState('')
  const [addLoading, setAddLoading] = useState(false)
  const [expandedUserId, setExpandedUserId] = useState<string | null>(null)

  const load = useCallback(() => {
    setLoading(true)
    getUsers()
      .then(setUsers)
      .catch(() => setError('Failed to load users.'))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => { load() }, [load])

  async function handleAddUser(e: React.FormEvent) {
    e.preventDefault()
    setAddError('')
    setAddLoading(true)
    try {
      await createUser(newUsername, newPassword, newRole)
      setNewUsername('')
      setNewPassword('')
      setNewRole('Member')
      setShowAddForm(false)
      load()
    } catch (err: unknown) {
      setAddError(err instanceof Error ? err.message : 'Failed to create user.')
    } finally {
      setAddLoading(false)
    }
  }

  async function handleToggleActive(user: TenantUserItem) {
    try {
      await updateUser(user.id, { isActive: !user.isActive })
      load()
    } catch {
      setError('Failed to update user.')
    }
  }

  async function handleRoleChange(user: TenantUserItem, newRoleValue: string) {
    try {
      await updateUser(user.id, { role: newRoleValue })
      load()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to update role.')
    }
  }

  function togglePermissions(userId: string) {
    setExpandedUserId((prev) => (prev === userId ? null : userId))
  }

  return (
    <div className="p-6">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">User Management</h1>
          <p className="mt-1 text-sm text-gray-500">
            Manage users and control their artifact-level permissions.
          </p>
        </div>
        <button
          onClick={() => { setShowAddForm(!showAddForm); setAddError('') }}
          className="flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
          </svg>
          Add User
        </button>
      </div>

      {/* Add user form */}
      {showAddForm && (
        <form onSubmit={handleAddUser} className="mb-6 rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
          <h2 className="mb-4 text-sm font-semibold text-gray-900">New User</h2>
          <div className="grid grid-cols-3 gap-4">
            <div>
              <label className="mb-1 block text-xs font-medium text-gray-700">Username</label>
              <input
                type="text"
                value={newUsername}
                onChange={(e) => setNewUsername(e.target.value)}
                required
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-gray-700">Password</label>
              <input
                type="password"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                required
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-gray-700">Role</label>
              <select
                value={newRole}
                onChange={(e) => setNewRole(e.target.value as 'Admin' | 'Member')}
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="Member">Member</option>
                <option value="Admin">Admin</option>
              </select>
            </div>
          </div>
          {addError && <p className="mt-2 text-sm text-red-600">{addError}</p>}
          <div className="mt-4 flex gap-2">
            <button
              type="submit"
              disabled={addLoading}
              className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {addLoading ? 'Creating…' : 'Create User'}
            </button>
            <button
              type="button"
              onClick={() => setShowAddForm(false)}
              className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
            >
              Cancel
            </button>
          </div>
        </form>
      )}

      {error && (
        <div className="mb-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>
      )}

      {loading ? (
        <div className="flex items-center justify-center py-12">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
        </div>
      ) : (
        <div className="space-y-2">
          {users.length === 0 ? (
            <div className="rounded-lg border border-dashed border-gray-300 p-8 text-center">
              <p className="text-sm text-gray-500">No users found. Seed the database first.</p>
            </div>
          ) : (
            <div className="overflow-hidden rounded-lg border border-gray-200 bg-white shadow-sm">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-200 bg-gray-50">
                    <th className="px-4 py-3 text-left font-medium text-gray-700">Username</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-700">Role</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-700">Status</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-700">Permissions</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-700">Created</th>
                    <th className="px-4 py-3 text-right font-medium text-gray-700">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {users.map((user) => (
                    <>
                      <tr key={user.id} className={expandedUserId === user.id ? 'bg-blue-50' : 'hover:bg-gray-50'}>
                        <td className="px-4 py-3 font-medium text-gray-900">{user.username}</td>
                        <td className="px-4 py-3">
                          <select
                            value={user.role}
                            onChange={(e) => handleRoleChange(user, e.target.value)}
                            className="rounded border border-gray-200 px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-blue-500"
                          >
                            <option value="Admin">Admin</option>
                            <option value="Member">Member</option>
                          </select>
                        </td>
                        <td className="px-4 py-3">
                          <StatusBadge isActive={user.isActive} />
                        </td>
                        <td className="px-4 py-3 text-gray-500">
                          {user.role === 'Admin' ? (
                            <span className="text-xs text-purple-600 font-medium">Full access</span>
                          ) : (
                            <button
                              onClick={() => togglePermissions(user.id)}
                              className="text-xs text-blue-600 hover:underline"
                            >
                              {user.permissionCount} artifact{user.permissionCount !== 1 ? 's' : ''}
                              {expandedUserId === user.id ? ' ▲' : ' ▼'}
                            </button>
                          )}
                        </td>
                        <td className="px-4 py-3 text-gray-500">
                          {new Date(user.createdAt).toLocaleDateString()}
                        </td>
                        <td className="px-4 py-3 text-right">
                          <div className="flex items-center justify-end gap-2">
                            {user.role !== 'Admin' && (
                              <button
                                onClick={() => togglePermissions(user.id)}
                                className="rounded px-2 py-1 text-xs font-medium text-blue-600 hover:bg-blue-50"
                              >
                                Permissions
                              </button>
                            )}
                            <button
                              onClick={() => handleToggleActive(user)}
                              className={`rounded px-2 py-1 text-xs font-medium ${
                                user.isActive
                                  ? 'text-red-600 hover:bg-red-50'
                                  : 'text-green-600 hover:bg-green-50'
                              }`}
                            >
                              {user.isActive ? 'Deactivate' : 'Activate'}
                            </button>
                          </div>
                        </td>
                      </tr>
                      {expandedUserId === user.id && (
                        <tr key={`${user.id}-permissions`}>
                          <td colSpan={6} className="px-4 py-3">
                            <PermissionsPanel
                              userId={user.id}
                              username={user.username}
                              onClose={() => setExpandedUserId(null)}
                            />
                          </td>
                        </tr>
                      )}
                    </>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
