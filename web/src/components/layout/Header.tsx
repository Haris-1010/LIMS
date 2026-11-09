import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Bell, ChevronDown, ChevronRight, KeyRound, LogOut, MapPin } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { branchName, displayName, useSession } from '../../app/session'
import type { RouteMeta } from '../../app/navigation'
import { initials } from '../../lib/format'
import { errorMessage } from '../../lib/errors'
import { ConfirmDialog } from '../ui/ConfirmDialog'
import { Menu } from '../ui/Menu'
import { useToast } from '../ui/Toast'
import { LanguageSwitch } from './LanguageSwitch'

export function Header({ route }: { route?: RouteMeta }) {
  const { t, i18n } = useTranslation()
  const { user, setActiveBranch, signOut } = useSession()
  const toast = useToast()
  const navigate = useNavigate()
  const [confirmOut, setConfirmOut] = useState(false)
  const [switching, setSwitching] = useState(false)

  const title = route ? t(`nav.${route.item.key}`) : t('common.notFound')
  const crumbs = route
    ? [t(`nav.groups.${route.group.key}`), route.parent && t(`nav.${route.parent.key}`)].filter(Boolean)
    : []

  const onBranch = async (value: string) => {
    setSwitching(true)
    try {
      await setActiveBranch(value === 'all' ? null : Number(value))
    } catch (e) {
      toast.show({ tone: 'danger', title: t('errors.title'), text: errorMessage(t, e) })
    } finally {
      setSwitching(false)
    }
  }

  const name = user ? displayName(user, i18n.language) : ''
  const showBranchSelect = !!user && (user.branches.length > 1 || user.allBranches)

  return (
    <header className="header">
      <div className="header-title">
        <nav className="breadcrumbs" aria-label="Breadcrumb">
          <span>{t('header.home')}</span>
          {crumbs.map((c) => (
            <span key={c as string} style={{ display: 'contents' }}>
              <ChevronRight size={12} aria-hidden />
              <span>{c}</span>
            </span>
          ))}
        </nav>
        <h1>{title}</h1>
      </div>

      <div className="header-actions">
        {user && user.branches.length > 0 && (
          <label className="branch-select" aria-busy={switching || undefined}>
            <MapPin size={15} aria-hidden />
            <span className="sr-only">{t('header.branch')}</span>
            {showBranchSelect ? (
              <select value={user.activeBranchId ?? 'all'} disabled={switching} onChange={(e) => void onBranch(e.target.value)}>
                {user.allBranches && <option value="all">{t('header.allBranchesView')}</option>}
                {user.branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {branchName(b, i18n.language)}
                  </option>
                ))}
              </select>
            ) : (
              <span className="branch-single">{branchName(user.branches[0]!, i18n.language)}</span>
            )}
          </label>
        )}

        <LanguageSwitch />

        <button type="button" className="icon-btn" aria-label={t('header.notifications')}>
          <Bell size={18} aria-hidden />
        </button>

        <span className="header-divider" aria-hidden />

        <Menu
          label={t('header.profile')}
          trigger={
            <span className="profile">
              <span className="avatar">{initials(user?.displayNameEn ?? '')}</span>
              <span className="profile-meta">
                <span className="profile-name">{name}</span>
                <span className="profile-role">{user?.roleNames[0]}</span>
              </span>
              <ChevronDown size={15} color="var(--muted)" aria-hidden />
            </span>
          }
          items={[
            { key: 'pw', label: t('header.changePassword'), icon: <KeyRound size={16} />, onSelect: () => navigate('/change-password') },
            { key: 'out', label: t('header.signOut'), icon: <LogOut size={16} className="flip-rtl" />, danger: true, onSelect: () => setConfirmOut(true) },
          ]}
        />
      </div>

      <ConfirmDialog
        open={confirmOut}
        tone="danger"
        icon={<LogOut size={20} className="flip-rtl" />}
        title={t('signOutDialog.title')}
        text={t('signOutDialog.text')}
        confirmLabel={t('signOutDialog.confirm')}
        onCancel={() => setConfirmOut(false)}
        onConfirm={() => {
          setConfirmOut(false)
          void signOut().then(() => navigate('/login', { replace: true }))
        }}
      />
    </header>
  )
}
