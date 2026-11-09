import { useTranslation } from 'react-i18next'
import { useSession } from '../../app/session'

/** EN / اردو toggle. When signed in, the choice is also saved to the user's profile. */
export function LanguageSwitch() {
  const { t, i18n } = useTranslation()
  const { setLanguage } = useSession()
  const lang = i18n.language
  return (
    <div className="lang-switch" role="group" aria-label={t('header.language')}>
      <button type="button" aria-pressed={lang === 'en'} onClick={() => setLanguage('en')} lang="en">
        EN
      </button>
      <button type="button" aria-pressed={lang === 'ur'} onClick={() => setLanguage('ur')} lang="ur" aria-label="اردو">
        <span className="ur-label">اردو</span>
      </button>
    </div>
  )
}
