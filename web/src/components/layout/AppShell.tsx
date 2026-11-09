import { Suspense, useState } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ShieldOff } from 'lucide-react'
import { findRoute } from '../../app/navigation'
import { useSession } from '../../app/session'
import { BrandMark } from '../BrandMark'
import { Card } from '../ui/Card'
import { EmptyState } from '../ui/EmptyState'
import { Skeleton } from '../ui/Skeleton'
import { Header } from './Header'
import { Sidebar } from './Sidebar'

const COLLAPSE_KEY = 'lims.sidebar.collapsed'

export function Splash() {
  const { t } = useTranslation()
  return (
    <div className="splash" role="status" aria-live="polite">
      <div className="splash-inner">
        <BrandMark />
        <span>{t('common.starting')}</span>
      </div>
    </div>
  )
}

function PageFallback() {
  return (
    <div className="page" aria-busy>
      <Skeleton height={28} width={240} />
      <div style={{ height: 16 }} />
      <Skeleton height={320} radius={12} />
    </div>
  )
}

/** Signed-in layout. Guards: session loaded → signed in → password changed → page permission. */
export function AppShell() {
  const { t } = useTranslation()
  const { user, loading, can } = useSession()
  const location = useLocation()
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(COLLAPSE_KEY) === '1' || window.innerWidth < 860
    } catch {
      return false
    }
  })

  if (loading) return <Splash />
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (user.mustChangePassword) return <Navigate to="/change-password" replace />

  const route = findRoute(location.pathname)
  const allowed = !route || can(route.item.permission)

  const toggle = () =>
    setCollapsed((c) => {
      try {
        localStorage.setItem(COLLAPSE_KEY, c ? '0' : '1')
      } catch {
        /* ignore */
      }
      return !c
    })

  return (
    <div className={`shell${collapsed ? ' is-collapsed' : ''}`}>
      <Sidebar collapsed={collapsed} onToggle={toggle} />
      <div className="main">
        <Header route={route} />
        <main id="content">
          {allowed ? (
            <Suspense fallback={<PageFallback />}>
              <Outlet />
            </Suspense>
          ) : (
            <div className="page">
              <Card>
                <EmptyState icon={<ShieldOff size={22} />} title={t('common.noAccess')} text={t('common.noAccessText')} />
              </Card>
            </div>
          )}
        </main>
      </div>
    </div>
  )
}
