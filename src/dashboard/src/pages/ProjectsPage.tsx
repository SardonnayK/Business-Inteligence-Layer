import { useState, useEffect } from 'react'
import type { Tenant } from '../api/tenants'
import { getProjects, getRequirements } from '../api/projects'
import type { Project, Requirement } from '../api/projects'
import Badge from '../components/Badge'

interface Props {
  tenants: Tenant[]
  selectedTenantId: string
  setSelectedTenantId: (id: string) => void
  tenantsLoading: boolean
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

function statusVariant(status: string): 'success' | 'warning' | 'info' | 'neutral' | 'danger' {
  const s = status.toLowerCase()
  if (s === 'done' || s === 'completed') return 'success'
  if (s === 'in progress' || s === 'active') return 'info'
  if (s === 'blocked') return 'danger'
  if (s === 'pending' || s === 'todo') return 'warning'
  return 'neutral'
}

export default function ProjectsPage({
  tenants,
  selectedTenantId,
  setSelectedTenantId,
}: Props) {
  const [projects, setProjects] = useState<Project[]>([])
  const [projectsLoading, setProjectsLoading] = useState(false)
  const [projectsError, setProjectsError] = useState<string | null>(null)

  const [selectedProject, setSelectedProject] = useState<Project | null>(null)
  const [requirements, setRequirements] = useState<Requirement[]>([])
  const [reqLoading, setReqLoading] = useState(false)
  const [reqError, setReqError] = useState<string | null>(null)

  useEffect(() => {
    if (!selectedTenantId) {
      setProjects([])
      setSelectedProject(null)
      return
    }
    setProjectsLoading(true)
    setProjectsError(null)
    setSelectedProject(null)
    setRequirements([])
    getProjects(selectedTenantId)
      .then(setProjects)
      .catch((err) => setProjectsError(err instanceof Error ? err.message : 'Failed to load projects.'))
      .finally(() => setProjectsLoading(false))
  }, [selectedTenantId])

  function handleProjectClick(project: Project) {
    if (selectedProject?.id === project.id) {
      setSelectedProject(null)
      setRequirements([])
      return
    }
    setSelectedProject(project)
    setReqLoading(true)
    setReqError(null)
    setRequirements([])
    getRequirements(project.id)
      .then(setRequirements)
      .catch((err) =>
        setReqError(err instanceof Error ? err.message : 'Failed to load requirements.'),
      )
      .finally(() => setReqLoading(false))
  }

  return (
    <div className="p-8">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Projects</h1>
        <p className="mt-1 text-sm text-gray-500">Browse projects and their requirements by tenant</p>
      </div>

      {/* Tenant selector */}
      <div className="mb-6">
        <label className="mb-1.5 block text-sm font-medium text-gray-700">Select Tenant</label>
        {tenants.length === 0 ? (
          <p className="text-sm text-gray-400">No tenants available. Seed demo data first.</p>
        ) : (
          <select
            value={selectedTenantId}
            onChange={(e) => setSelectedTenantId(e.target.value)}
            className="rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          >
            <option value="">Select a tenant</option>
            {tenants.map((t) => (
              <option key={t.id} value={t.id}>
                {t.name}
              </option>
            ))}
          </select>
        )}
      </div>

      {/* Projects list */}
      {!selectedTenantId ? (
        <div className="rounded-xl border border-dashed border-gray-200 py-16 text-center">
          <p className="text-sm text-gray-400">Select a tenant to view its projects.</p>
        </div>
      ) : projectsLoading ? (
        <div className="flex items-center justify-center py-16">
          <LoadingSpinner />
        </div>
      ) : projectsError ? (
        <div className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800">{projectsError}</div>
      ) : projects.length === 0 ? (
        <div className="rounded-xl border border-dashed border-gray-200 py-16 text-center">
          <p className="text-sm text-gray-400">No projects found for this tenant.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {projects.map((project) => (
            <div key={project.id}>
              <button
                onClick={() => handleProjectClick(project)}
                className={`w-full rounded-xl border px-6 py-4 text-left transition-all ${
                  selectedProject?.id === project.id
                    ? 'border-blue-300 bg-blue-50 shadow-sm'
                    : 'border-gray-200 bg-white hover:border-blue-200 hover:bg-blue-50/40'
                }`}
              >
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    <h3 className="text-sm font-semibold text-gray-900">{project.name}</h3>
                    {project.description && (
                      <p className="mt-1 text-sm text-gray-500">{project.description}</p>
                    )}
                  </div>
                  <div className="ml-4 flex items-center gap-2">
                    <span className="text-xs text-gray-400">{formatDate(project.createdAt)}</span>
                    <svg
                      className={`h-4 w-4 text-gray-400 transition-transform ${
                        selectedProject?.id === project.id ? 'rotate-90' : ''
                      }`}
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                      strokeWidth={2}
                    >
                      <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
                    </svg>
                  </div>
                </div>
              </button>

              {/* Requirements panel */}
              {selectedProject?.id === project.id && (
                <div className="ml-4 mt-2 rounded-xl border border-gray-100 bg-white p-5 shadow-sm">
                  <h4 className="mb-3 text-sm font-semibold text-gray-700">Requirements</h4>

                  {reqLoading ? (
                    <div className="flex items-center justify-center py-6">
                      <LoadingSpinner />
                    </div>
                  ) : reqError ? (
                    <div className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800">
                      {reqError}
                    </div>
                  ) : requirements.length === 0 ? (
                    <p className="text-sm text-gray-400">No requirements found for this project.</p>
                  ) : (
                    <ul className="divide-y divide-gray-100">
                      {requirements.map((req) => (
                        <li key={req.id} className="flex items-start justify-between py-3">
                          <p className="flex-1 pr-4 text-sm text-gray-900">{req.content}</p>
                          <div className="flex flex-shrink-0 flex-col items-end gap-1">
                            <Badge variant={statusVariant(req.status)}>{req.status}</Badge>
                            <span className="text-xs text-gray-400">{formatDate(req.createdAt)}</span>
                          </div>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
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
