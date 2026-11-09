import { createContext, useCallback, useContext, useEffect, useMemo, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import i18n from '../i18n'
import { api, ApiError, resetCsrf, UNAUTHORIZED_EVENT } from '../lib/api'

export interface Branch {
  id: number
  code: string
  nameEn: string
  nameUr: string | null
  isMain: boolean
}

/** The signed-in user as returned by GET /api/auth/me. Permissions are enforced by the server. */
export interface SessionUser {
  userId: number
  username: string
  displayNameEn: string
  displayNameUr: string | null
  designation: string | null
  preferredLanguage: 'en' | 'ur'
  mustChangePassword: boolean
  organizationId: number
  organizationNameEn: string
  organizationNameUr: string | null
  allBranches: boolean
  branches: Branch[]
  activeBranchId: number | null
  permissions: string[]
  roleNames: string[]
}

interface SessionApi {
  user: SessionUser | null
  loading: boolean
  signIn: (username: string, password: string, rememberMe: boolean) => Promise<SessionUser>
  signOut: () => Promise<void>
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>
  /** null = all branches. */
  setActiveBranch: (branchId: number | null) => Promise<void>
  setLanguage: (lang: 'en' | 'ur') => void
  can: (permission?: string) => boolean
}

const ME_KEY = ['session', 'me'] as const
const SessionContext = createContext<SessionApi | null>(null)

async function fetchMe(): Promise<SessionUser | null> {
  try {
    return await api<SessionUser>('/api/auth/me', { silent401: true })
  } catch (e) {
    if (e instanceof ApiError && e.status === 401) return null
    throw e
  }
}

export function SessionProvider({ children }: { children: ReactNode }) {
  const qc = useQueryClient()
  const { data: user, isLoading } = useQuery({ queryKey: ME_KEY, queryFn: fetchMe, staleTime: 60_000, retry: false })

  // Any 401 from the API means the session ended (expired, deactivated, password changed elsewhere).
  useEffect(() => {
    const onUnauthorized = () => {
      resetCsrf()
      qc.setQueryData(ME_KEY, null)
    }
    window.addEventListener(UNAUTHORIZED_EVENT, onUnauthorized)
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, onUnauthorized)
  }, [qc])

  /** Drops all cached business data but keeps the session query (its observers must stay attached). */
  const clearData = useCallback(() => qc.removeQueries({ predicate: (q) => q.queryKey[0] !== 'session' }), [qc])

  const applySession = useCallback(
    (me: SessionUser) => {
      resetCsrf()
      qc.setQueryData(ME_KEY, me)
      return me
    },
    [qc],
  )

  const signIn = useCallback(
    async (username: string, password: string, rememberMe: boolean) => {
      const me = await api<SessionUser>('/api/auth/login', { method: 'POST', body: { username, password, rememberMe } })
      clearData() // never show a previous user's cached data
      if (me.preferredLanguage !== i18n.language) void i18n.changeLanguage(me.preferredLanguage)
      return applySession(me)
    },
    [clearData, applySession],
  )

  const signOut = useCallback(async () => {
    try {
      await api('/api/auth/logout', { method: 'POST' })
    } finally {
      resetCsrf()
      clearData()
      qc.setQueryData(ME_KEY, null)
    }
  }, [qc, clearData])

  const changePassword = useCallback(
    async (currentPassword: string, newPassword: string) => {
      applySession(await api<SessionUser>('/api/auth/change-password', { method: 'POST', body: { currentPassword, newPassword } }))
    },
    [applySession],
  )

  const setActiveBranch = useCallback(
    async (branchId: number | null) => {
      const me = await api<SessionUser>('/api/auth/active-branch', { method: 'POST', body: { branchId } })
      applySession(me)
      // Everything branch-scoped must reload for the new branch.
      await qc.invalidateQueries({ predicate: (q) => q.queryKey[0] !== 'session' })
    },
    [qc, applySession],
  )

  const setLanguage = useCallback(
    (lang: 'en' | 'ur') => {
      void i18n.changeLanguage(lang)
      const me = qc.getQueryData<SessionUser | null>(ME_KEY)
      if (me) {
        qc.setQueryData(ME_KEY, { ...me, preferredLanguage: lang })
        api('/api/auth/preferences', { method: 'PUT', body: { language: lang } }).catch(() => {
          /* preference is a convenience; the switch already applied locally */
        })
      }
    },
    [qc],
  )

  const can = useCallback((permission?: string) => !permission || !!user?.permissions.includes(permission), [user])

  const value = useMemo<SessionApi>(
    () => ({ user: user ?? null, loading: isLoading, signIn, signOut, changePassword, setActiveBranch, setLanguage, can }),
    [user, isLoading, signIn, signOut, changePassword, setActiveBranch, setLanguage, can],
  )
  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useSession(): SessionApi {
  const ctx = useContext(SessionContext)
  if (!ctx) throw new Error('useSession must be used inside <SessionProvider>')
  return ctx
}

// eslint-disable-next-line react-refresh/only-export-components
export function branchName(b: Pick<Branch, 'nameEn' | 'nameUr'>, lang: string) {
  return lang === 'ur' && b.nameUr ? b.nameUr : b.nameEn
}

// eslint-disable-next-line react-refresh/only-export-components
export function displayName(u: Pick<SessionUser, 'displayNameEn' | 'displayNameUr'>, lang: string) {
  return lang === 'ur' && u.displayNameUr ? u.displayNameUr : u.displayNameEn
}
