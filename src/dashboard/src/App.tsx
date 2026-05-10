import { useState, useEffect } from 'react'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import Layout from './components/Layout'
import DashboardPage from './pages/DashboardPage'
import TenantsPage from './pages/TenantsPage'
import BusinessContextPage from './pages/BusinessContextPage'
import ProjectsPage from './pages/ProjectsPage'
import HealthPage from './pages/HealthPage'
import { getTenants } from './api/tenants'
import type { Tenant } from './api/tenants'

export default function App() {
  const [tenants, setTenants] = useState<Tenant[]>([])
  const [selectedTenantId, setSelectedTenantId] = useState<string>('')
  const [tenantsLoading, setTenantsLoading] = useState(true)

  useEffect(() => {
    getTenants()
      .then((data) => {
        setTenants(data)
        if (data.length > 0 && !selectedTenantId) {
          setSelectedTenantId(data[0].id)
        }
      })
      .catch(() => {
        // tenants endpoint may not exist yet; silently continue
      })
      .finally(() => setTenantsLoading(false))
  }, [])

  const sharedProps = {
    tenants,
    selectedTenantId,
    setSelectedTenantId,
    tenantsLoading,
    refreshTenants: () => {
      setTenantsLoading(true)
      getTenants()
        .then((data) => {
          setTenants(data)
          if (data.length > 0 && !selectedTenantId) {
            setSelectedTenantId(data[0].id)
          }
        })
        .catch(() => {})
        .finally(() => setTenantsLoading(false))
    },
  }

  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/" element={<DashboardPage {...sharedProps} />} />
          <Route path="/tenants" element={<TenantsPage {...sharedProps} />} />
          <Route path="/context" element={<BusinessContextPage {...sharedProps} />} />
          <Route path="/projects" element={<ProjectsPage {...sharedProps} />} />
          <Route path="/health" element={<HealthPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
