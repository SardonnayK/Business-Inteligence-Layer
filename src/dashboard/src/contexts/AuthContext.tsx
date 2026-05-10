import { createContext, useContext, useState, useEffect, type ReactNode } from 'react'

export interface AuthUser {
  userId: string
  username: string
  tenantId: string
  role: 'Admin' | 'Member'
}

interface AuthContextValue {
  user: AuthUser | null
  isLoading: boolean
  login: (token: string) => void
  logout: () => void
}

function parseToken(token: string): AuthUser | null {
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')))
    const exp = payload.exp as number
    if (exp && Date.now() / 1000 > exp) return null
    return {
      userId: payload.sub as string,
      username: payload.name as string,
      tenantId: payload.tenant_id as string,
      role: (payload.role as string) === 'Admin' ? 'Admin' : 'Member',
    }
  } catch {
    return null
  }
}

const AuthContext = createContext<AuthContextValue>({
  user: null,
  isLoading: true,
  login: () => {},
  logout: () => {},
})

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    try {
      const raw = localStorage.getItem('bi_auth')
      if (raw) {
        const { token } = JSON.parse(raw)
        const parsed = parseToken(token)
        setUser(parsed)
        if (!parsed) localStorage.removeItem('bi_auth')
      }
    } catch {
      localStorage.removeItem('bi_auth')
    }
    setIsLoading(false)
  }, [])

  function login(token: string) {
    const parsed = parseToken(token)
    if (!parsed) throw new Error('Invalid token')
    localStorage.setItem('bi_auth', JSON.stringify({ token }))
    setUser(parsed)
  }

  function logout() {
    localStorage.removeItem('bi_auth')
    setUser(null)
  }

  return (
    <AuthContext.Provider value={{ user, isLoading, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  return useContext(AuthContext)
}
