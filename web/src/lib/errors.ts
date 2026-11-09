import type { TFunction } from 'i18next'
import { ApiError } from './api'

/** Translates an API error by its stable code; falls back to the server's English title. */
export function errorMessage(t: TFunction, error: unknown): string {
  if (error instanceof ApiError) return t(`errors.${error.code}`, { defaultValue: error.message || t('errors.server_error') })
  return t('errors.server_error')
}

/** camelCase field names from ASP.NET validation errors ("DisplayNameEn" → "displayNameEn"). */
export function fieldErrors(error: unknown): Record<string, string> {
  if (!(error instanceof ApiError)) return {}
  return Object.fromEntries(
    Object.entries(error.fieldErrors).map(([k, v]) => [k.charAt(0).toLowerCase() + k.slice(1), v[0] ?? '']),
  )
}
