import { useQueryClient } from '@tanstack/react-query'
import { corporateKeys } from './corporateApi'

export function useRefreshCorporate() {
  const qc = useQueryClient()
  return async () => {
    await qc.invalidateQueries({ queryKey: corporateKeys.all })
    await qc.invalidateQueries({ queryKey: ['orders'] })
    await qc.invalidateQueries({ queryKey: ['billing'] })
  }
}

export const clientName = (c: { nameEn: string; nameUr: string | null }, lang: string) => (lang === 'ur' && c.nameUr ? c.nameUr : c.nameEn)
