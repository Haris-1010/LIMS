import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import en from './locales/en.json'
import ur from './locales/ur.json'

export type Language = 'en' | 'ur'

const STORAGE_KEY = 'lims.language'

function readStoredLanguage(): Language {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'ur' ? 'ur' : 'en'
  } catch {
    return 'en'
  }
}

/** Keeps <html lang/dir> in step with the active language so CSS logical properties flip for RTL. */
function applyDocumentLanguage(lng: string) {
  const isUrdu = lng === 'ur'
  document.documentElement.lang = isUrdu ? 'ur' : 'en'
  document.documentElement.dir = isUrdu ? 'rtl' : 'ltr'
}

i18n.use(initReactI18next).init({
  resources: { en: { translation: en }, ur: { translation: ur } },
  lng: readStoredLanguage(),
  fallbackLng: 'en',
  interpolation: { escapeValue: false }, // React already escapes output
  returnNull: false,
})

applyDocumentLanguage(i18n.language)

i18n.on('languageChanged', (lng) => {
  applyDocumentLanguage(lng)
  try {
    localStorage.setItem(STORAGE_KEY, lng)
  } catch {
    /* storage unavailable — preference simply won't persist */
  }
})

export default i18n
