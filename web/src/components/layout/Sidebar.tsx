import { useState } from 'react'
import { NavLink, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ChevronRight, PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import { navigation, type NavItem } from '../../app/navigation'
import { useSession } from '../../app/session'
import { BrandMark } from '../BrandMark'

interface SidebarProps {
  collapsed: boolean
  onToggle: () => void
}

export function Sidebar({ collapsed, onToggle }: SidebarProps) {
  const { t } = useTranslation()
  const { can } = useSession()
  const { pathname } = useLocation()

  // Parents start expanded when one of their children is the current page.
  const [open, setOpen] = useState<Record<string, boolean>>(() =>
    Object.fromEntries(
      navigation.flatMap((g) => g.items.filter((i) => i.children?.some((c) => c.path === pathname)).map((i) => [i.key, true])),
    ),
  )

  const visible = (item: NavItem) =>
    can(item.permission) && (!item.children || item.children.some((c) => can(c.permission)))

  return (
    <aside className="sidebar" aria-label={t('app.name')}>
      <div className="sidebar-brand">
        <BrandMark />
        <div className="brand-text">
          <span className="brand-name">{t('app.name')}</span>
          <span className="brand-tag">{t('app.tagline')}</span>
        </div>
      </div>

      <nav className="sidebar-nav">
        {navigation.map((group) => {
          const items = group.items.filter(visible)
          if (items.length === 0) return null
          return (
            <div className="nav-group" key={group.key}>
              <div className="nav-group-label">{t(`nav.groups.${group.key}`)}</div>
              {items.map((item) => {
                const Icon = item.icon
                const label = t(`nav.${item.key}`)

                if (item.children) {
                  const childActive = item.children.some((c) => c.path === pathname)
                  const isOpen = open[item.key] ?? false
                  return (
                    <div key={item.key}>
                      <button
                        type="button"
                        className={`nav-item${childActive && (collapsed || !isOpen) ? ' is-active' : ''}`}
                        aria-expanded={isOpen}
                        title={collapsed ? label : undefined}
                        onClick={() => {
                          if (collapsed) onToggle()
                          setOpen((s) => ({ ...s, [item.key]: !isOpen || collapsed }))
                        }}
                      >
                        {Icon && <Icon size={18} aria-hidden />}
                        <span className="nav-item-label">{label}</span>
                        <ChevronRight size={15} className="nav-chevron" aria-hidden />
                      </button>
                      {isOpen && (
                        <div className="nav-children">
                          {item.children
                            .filter((c) => can(c.permission))
                            .map((child) => (
                              <NavLink
                                key={child.key}
                                to={child.path!}
                                end
                                className={({ isActive }) => `nav-item${isActive ? ' is-active' : ''}`}
                              >
                                <span className="nav-item-label">{t(`nav.${child.key}`)}</span>
                              </NavLink>
                            ))}
                        </div>
                      )}
                    </div>
                  )
                }

                return (
                  <NavLink
                    key={item.key}
                    to={item.path!}
                    end
                    title={collapsed ? label : undefined}
                    className={({ isActive }) => `nav-item${isActive ? ' is-active' : ''}`}
                  >
                    {Icon && <Icon size={18} aria-hidden />}
                    <span className="nav-item-label">{label}</span>
                  </NavLink>
                )
              })}
            </div>
          )
        })}
      </nav>

      <div className="sidebar-foot">
        <button type="button" className="nav-item" onClick={onToggle} aria-label={collapsed ? t('nav.expand') : t('nav.collapse')}>
          {collapsed ? <PanelLeftOpen size={18} className="flip-rtl" aria-hidden /> : <PanelLeftClose size={18} className="flip-rtl" aria-hidden />}
          <span className="nav-item-label">{t('nav.collapse')}</span>
        </button>
      </div>
    </aside>
  )
}
