import { useTranslation } from 'react-i18next'
import { useDepartments } from '../admin/adminApi'

/** Active departments with their name in the current language, for filters and pickers. */
export function useDepartmentOptions() {
  const { i18n } = useTranslation()
  const { data } = useDepartments()
  return (data ?? []).filter((d) => d.isActive).map((d) => ({ id: d.id, name: i18n.language === 'ur' && d.nameUr ? d.nameUr : d.nameEn }))
}
