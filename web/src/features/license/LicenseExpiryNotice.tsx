import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertTriangle, Clock3 } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { useSession } from '../../app/session'
import { Button } from '../../components/ui/Button'
import { api } from '../../lib/api'
import type { LicenseStatus } from './licenseApi'
import { licenseKey } from './licenseApi'

function daysUntilExpiry(expiresOnUtc: string, now: Date): number | null {
  const expiry = new Date(expiresOnUtc)
  if (!Number.isFinite(expiry.getTime())) return null

  const expiryDay = Date.UTC(expiry.getUTCFullYear(), expiry.getUTCMonth(), expiry.getUTCDate())
  const today = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate())
  return Math.floor((expiryDay - today) / 86_400_000)
}

export function LicenseExpiryNotice() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { can } = useSession()
  const [now, setNow] = useState(() => new Date())
  const { data } = useQuery({
    queryKey: licenseKey,
    queryFn: () => api<LicenseStatus>('/api/license'),
    staleTime: 5 * 60_000,
  })

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 60_000)
    return () => window.clearInterval(timer)
  }, [])

  if (!data?.isValid || data.reason !== 'ok' || !data.expiresOnUtc) return null

  const days = daysUntilExpiry(data.expiresOnUtc, now)
  if (days === null || days > 7) return null

  const urgent = days <= 2
  const date = new Intl.DateTimeFormat(i18n.language === 'ur' ? 'ur-PK' : 'en-GB', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(data.expiresOnUtc))
  const key = days === 0
    ? urgent ? 'license.expiry.todayUrgent' : 'license.expiry.today'
    : days === 1 ? 'license.expiry.urgentOneDay'
    : urgent ? 'license.expiry.urgent' : 'license.expiry.warning'

  return (
    <div className="page license-expiry-page">
      <div className={`banner tone-${urgent ? 'danger' : 'warning'}`} role={urgent ? 'alert' : 'status'}>
        {urgent ? <AlertTriangle size={18} aria-hidden /> : <Clock3 size={18} aria-hidden />}
        <span>{t(key, { days: Math.max(days, 0), date })}</span>
        {can('settings.manage') && (
          <Button size="sm" variant={urgent ? 'danger' : 'secondary'} className="license-expiry-action" onClick={() => navigate('/admin/settings')}>
            {t('license.manageTitle')}
          </Button>
        )}
      </div>
    </div>
  )
}
