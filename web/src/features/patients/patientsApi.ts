import type { TFunction } from 'i18next'

/* Contracts for /api/patients and /api/doctors (see Lims.Application.Patients). */

export type Sex = 0 | 1 | 2 | 3

export interface PatientListItem {
  id: number
  patientNo: string
  title: string | null
  fullName: string
  fullNameUr: string | null
  guardianName: string | null
  sex: Sex
  dateOfBirth: string | null
  isDateOfBirthEstimated: boolean
  mobile: string | null
  city: string | null
  registeredAtUtc: string
}

export interface PatientDetail extends PatientListItem {
  cnic: string | null
  altPhone: string | null
  email: string | null
  address: string | null
  notes: string | null
  sensitiveVisible: boolean
  registeredBranchId: number | null
  registeredBranchName: string | null
  registeredByName: string | null
  rowVersion: string
}

export interface DuplicateCandidate {
  id: number
  patientNo: string
  fullName: string
  guardianName: string | null
  sex: Sex
  dateOfBirth: string | null
  mobile: string | null
  reasons: string[]
}

export interface DoctorDto {
  id: number
  code: string
  fullName: string
  fullNameUr: string | null
  qualification: string | null
  specialty: string | null
  pmdcNo: string | null
  phone: string | null
  email: string | null
  hospital: string | null
  city: string | null
  isInHouse: boolean
  isActive: boolean
  rowVersion: string
}

export const patientKeys = {
  list: (params: object) => ['patients', 'list', params] as const,
  detail: (id: number) => ['patients', 'detail', id] as const,
  doctors: (params: object) => ['doctors', 'list', params] as const,
}

/** Years/months/days between a date of birth and today (same rule as the server's PatientAge). */
export function ageParts(dob: string, today = new Date()): { y: number; m: number; d: number } {
  const [by, bm, bd] = dob.split('-').map(Number) as [number, number, number]
  const t = { y: today.getFullYear(), m: today.getMonth() + 1, d: today.getDate() }
  const addMonths = (months: number) => {
    const target = new Date(Date.UTC(by, bm - 1 + months, 1))
    const days = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0)).getUTCDate()
    target.setUTCDate(Math.min(bd, days))
    return target
  }
  const todayUtc = Date.UTC(t.y, t.m - 1, t.d)
  let total = (t.y - by) * 12 + (t.m - bm)
  let anchor = addMonths(total)
  if (anchor.getTime() > todayUtc) anchor = addMonths(--total)
  return { y: Math.floor(total / 12), m: total % 12, d: Math.round((todayUtc - anchor.getTime()) / 86_400_000) }
}

/** Compact age: "34 y", "1 y 3 m", "5 m 12 d", "9 d" — finer units only for young children. */
export function formatAge(t: TFunction, dob: string | null): string {
  if (!dob) return '—'
  const { y, m, d } = ageParts(dob)
  if (y >= 5) return t('age.y', { count: y })
  if (y >= 1) return [t('age.y', { count: y }), m ? t('age.m', { count: m }) : ''].filter(Boolean).join(' ')
  if (m >= 1) return [t('age.m', { count: m }), d ? t('age.d', { count: d }) : ''].filter(Boolean).join(' ')
  return t('age.d', { count: d })
}

export function patientName(p: { fullName: string; fullNameUr: string | null; title?: string | null }, lang: string) {
  const name = lang === 'ur' && p.fullNameUr ? p.fullNameUr : p.fullName
  return p.title && !(lang === 'ur' && p.fullNameUr) ? `${p.title} ${name}` : name
}

/** Formats a stored mobile number for display: 03001234567 → 0300-1234567. */
export function formatPhone(p: string | null) {
  if (!p) return '—'
  return /^0\d{10}$/.test(p) ? `${p.slice(0, 4)}-${p.slice(4)}` : p
}
