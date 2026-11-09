import { useTranslation } from 'react-i18next'
import { Link, useLocation } from 'react-router-dom'
import { ArrowLeft, Compass, Hammer } from 'lucide-react'
import { findRoute } from '../../app/navigation'
import { Card } from '../../components/ui/Card'
import { EmptyState } from '../../components/ui/EmptyState'

/** Shown for modules that are in the navigation but not built yet. */
export function PlaceholderPage() {
  const { t } = useTranslation()
  const route = findRoute(useLocation().pathname)

  return (
    <div className="page">
      <Card>
        {route ? (
          <EmptyState
            icon={<Hammer size={22} />}
            title={t('common.comingSoon')}
            text={t('common.comingSoonText', { module: t(`nav.${route.item.key}`), phase: route.item.phase })}
            action={<BackLink />}
          />
        ) : (
          <EmptyState icon={<Compass size={22} />} title={t('common.notFound')} text={t('common.notFoundText')} action={<BackLink />} />
        )}
      </Card>
    </div>
  )
}

function BackLink() {
  const { t } = useTranslation()
  return (
    <Link to="/" className="btn btn-secondary">
      <ArrowLeft size={16} className="flip-rtl" aria-hidden />
      {t('common.backToDashboard')}
    </Link>
  )
}
