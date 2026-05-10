import { useState, useEffect } from 'react'
import { BrowserRouter, Routes, Route, Navigate, useLocation } from 'react-router-dom'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import Layout from './components/Layout'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import TenantsPage from './pages/TenantsPage'
import BusinessContextPage from './pages/BusinessContextPage'
import KnowledgePage from './pages/KnowledgePage'
import ProjectsPage from './pages/ProjectsPage'
import HealthPage from './pages/HealthPage'
import SettingsPage from './pages/SettingsPage'
import UsersPage from './pages/UsersPage'
import { getTenants } from './api/tenants'
import type { Tenant } from './api/tenants'

function ProtectedRoutes() {
  const { user, isLoading } = useAuth()
  const location = useLocation()
  const [tenants, setTenants] = useState<Tenant[]>([])
  const [selectedTenantId, setSelectedTenantId] = useState<string>('')
  const [tenantsLoading, setTenantsLoading] = useState(true)

  useEffect(() => {
    if (!user) return
    // Tenant is determined by JWT — set it immediately
    setSelectedTenantId(user.tenantId)
    // Still load tenants list for display purposes
    getTenants()
      .then((data) => setTenants(data))
      .catch(() => {})
      .finally(() => setTenantsLoading(false))
  }, [user])

  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center bg-gray-50">
        <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
      </div>
    )
  }

  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  const sharedProps = {
    tenants,
    selectedTenantId,
    setSelectedTenantId: (_id: string) => { /* tenant locked to JWT */ },
    tenantsLoading,
    refreshTenants: () => {
      getTenants().then(setTenants).catch(() => {})
    },
  }

  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<DashboardPage {...sharedProps} />} />
        <Route path="/tenants" element={<TenantsPage {...sharedProps} />} />
        <Route path="/context" element={<BusinessContextPage {...sharedProps} />} />
        <Route path="/knowledge" element={<KnowledgePage {...sharedProps} />} />
        <Route path="/projects" element={<ProjectsPage {...sharedProps} />} />
        <Route path="/health" element={<HealthPage />} />
        <Route path="/settings" element={<SettingsPage {...sharedProps} />} />
        <Route path="/users" element={<UsersPage />} />
      </Route>
    </Routes>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginRouteGuard />} />
          <Route path="/*" element={<ProtectedRoutes />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}

function LoginRouteGuard() {
  const { user, isLoading } = useAuth()
  if (isLoading) return null
  if (user) return <Navigate to="/" replace />
  return <LoginPage />
}
