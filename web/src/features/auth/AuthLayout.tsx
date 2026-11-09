import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { BrandMark } from '../../components/BrandMark'
import { LanguageSwitch } from '../../components/layout/LanguageSwitch'
import '../../styles/login.css'

/** Split layout shared by sign-in and password screens: brand panel + form panel. */
export function AuthLayout({ hero, children }: { hero?: ReactNode; children: ReactNode }) {
  const { t } = useTranslation()
  return (
    <div className="login">
      <aside className="login-hero">
        <div className="hero-brand">
          <BrandMark />
          <div className="brand-text">
            <span className="brand-name">{t('app.name')}</span>
            <span className="brand-tag">{t('app.tagline')}</span>
          </div>
        </div>
        <div className="hero-copy">{hero}</div>
        <p className="hero-foot">{t('login.hero.foot', { year: new Date().getFullYear() })}</p>
      </aside>

      <main className="login-panel">
        <div className="login-panel-top">
          <LanguageSwitch />
        </div>
        <div className="login-card">{children}</div>
      </main>
    </div>
  )
}
