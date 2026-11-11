import { useQuery } from '@tanstack/react-query'
import type { TFunction } from 'i18next'
import { api } from '../../lib/api'

/* Contracts for /api/catalog (see Lims.Application.Catalog). */

export type ResultType = 1 | 2 | 3 | 4
export type RangeSex = 0 | 1 | 2
export type RangeStatus = 0 | 1 | 2

export interface UnitDto { id: number; code: string; name: string }
export interface SampleTypeDto { id: number; code: string; nameEn: string; nameUr: string | null; container: string | null; capColour: string | null }
export interface CategoryDto { id: number; code: string; nameEn: string; nameUr: string | null; sortOrder: number; isActive: boolean; testCount: number; rowVersion: string }

export interface TestListItem {
  id: number
  code: string
  nameEn: string
  nameUr: string | null
  shortName: string | null
  categoryName: string
  categoryNameUr: string | null
  departmentName: string
  departmentNameUr: string | null
  sampleTypeName: string
  sampleTypeNameUr: string | null
  price: number
  turnaroundHours: number
  parameterCount: number
  parametersWithApprovedRanges: number
  draftRangeCount: number
  isActive: boolean
}

export interface OptionDto { valueEn: string; valueUr: string | null; isAbnormal: boolean }

export interface ParameterDto {
  id: number
  code: string
  nameEn: string
  nameUr: string | null
  resultType: ResultType
  unitId: number | null
  unitCode: string | null
  decimalPlaces: number
  groupName: string | null
  sortOrder: number
  isActive: boolean
  options: OptionDto[]
  approvedRangeCount: number
  draftRangeCount: number
  rowVersion: string
}

export interface TestDetail {
  id: number
  code: string
  nameEn: string
  nameUr: string | null
  shortName: string | null
  categoryId: number
  departmentId: number
  sampleTypeId: number
  sampleVolume: string | null
  collectionNotes: string | null
  method: string | null
  turnaroundHours: number
  price: number
  reportNotes: string | null
  sortOrder: number
  availableAllBranches: boolean
  branchIds: number[]
  isActive: boolean
  parameters: ParameterDto[]
  rowVersion: string
}

export interface RangeDto {
  id: number
  parameterId: number
  sex: RangeSex
  ageFromDays: number
  ageToDays: number | null
  low: number | null
  high: number | null
  criticalLow: number | null
  criticalHigh: number | null
  displayText: string | null
  notes: string | null
  source: string | null
  status: RangeStatus
  replacesRangeId: number | null
  createdByName: string | null
  createdAtUtc: string
  approvedByName: string | null
  approvedAtUtc: string | null
  retiredAtUtc: string | null
  rowVersion: string
}

export interface PendingRange {
  range: RangeDto
  testId: number
  testCode: string
  testName: string
  parameterName: string
  resultType: ResultType
  unitCode: string | null
  replaces: RangeDto | null
}

export interface BulkApproveFailure {
  id: number
  code: string
}

export interface BulkApproveResult {
  approved: number
  failed: BulkApproveFailure[]
}

export interface PackageDto {
  id: number
  code: string
  nameEn: string
  nameUr: string | null
  description: string | null
  price: number
  isActive: boolean
  tests: { id: number; code: string; nameEn: string; nameUr: string | null; price: number }[]
  testsTotal: number
  rowVersion: string
}

export const catalogKeys = {
  tests: (p: object) => ['catalog', 'tests', p] as const,
  test: (id: number) => ['catalog', 'test', id] as const,
  ranges: (parameterId: number) => ['catalog', 'ranges', parameterId] as const,
  pending: ['catalog', 'pending'] as const,
  categories: ['catalog', 'categories'] as const,
  packages: ['catalog', 'packages'] as const,
  units: ['catalog', 'units'] as const,
  sampleTypes: ['catalog', 'sampleTypes'] as const,
}

export const useCategories = () => useQuery({ queryKey: catalogKeys.categories, queryFn: () => api<CategoryDto[]>('/api/catalog/categories') })
export const useUnits = () => useQuery({ queryKey: catalogKeys.units, queryFn: () => api<UnitDto[]>('/api/catalog/units'), staleTime: Infinity })
export const useSampleTypes = () =>
  useQuery({ queryKey: catalogKeys.sampleTypes, queryFn: () => api<SampleTypeDto[]>('/api/catalog/sample-types'), staleTime: Infinity })

/* ---------- Age bands ----------
 * Bands are stored in days. Entry and display use years (365.25 days) or months (30.4375 days),
 * rounded to whole days, so "18 years" is within a day of the actual birthday. */
export type AgeUnit = 'd' | 'm' | 'y'
const PER: Record<AgeUnit, number> = { d: 1, m: 30.4375, y: 365.25 }

export const toDays = (value: number, unit: AgeUnit) => Math.round(value * PER[unit])

/** Best unit to show a stored day count in (exact whole years, then months, else days). */
export function fromDays(days: number): { value: number; unit: AgeUnit } {
  if (days === 0) return { value: 0, unit: 'y' }
  for (const unit of ['y', 'm'] as const) {
    const v = Math.round(days / PER[unit])
    if (v > 0 && toDays(v, unit) === days) return { value: v, unit }
  }
  return { value: days, unit: 'd' }
}

export function formatAgeBand(t: TFunction, from: number, to: number | null): string {
  const part = (d: number) => {
    const { value, unit } = fromDays(d)
    return `${value} ${t(`ageUnit.${unit}`)}`
  }
  if (from === 0 && to === null) return t('catalog.anyAge')
  if (to === null) return t('catalog.ageFromText', { from: part(from) })
  return t('catalog.ageRangeText', { from: from === 0 ? '0' : part(from), to: part(to) })
}

/** "10 – 20", "≤ 200", "≥ 5" or the configured display text. Values are shown exactly as stored. */
export function formatLimits(r: Pick<RangeDto, 'low' | 'high' | 'displayText'>): string {
  if (r.displayText) return r.displayText
  if (r.low !== null && r.high !== null) return `${r.low} – ${r.high}`
  if (r.high !== null) return `≤ ${r.high}`
  if (r.low !== null) return `≥ ${r.low}`
  return '—'
}

export function localName(x: { nameEn: string; nameUr: string | null }, lang: string) {
  return lang === 'ur' && x.nameUr ? x.nameUr : x.nameEn
}
