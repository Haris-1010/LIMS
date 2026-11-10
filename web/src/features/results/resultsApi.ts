import type { Sex } from '../patients/patientsApi'

/* Contracts for /api/results, /api/worklists and /api/critical-alerts (see Lims.Application.Results). */

export type ResultStatus = 0 | 10 | 20 | 30 | 90
export type ResultFlag = 0 | 1 | 2 | 3 | 4 | 5 | 6
export type ResultType = 1 | 2 | 3 | 4
export type AlertStatus = 0 | 1
export type NotifyMethod = 1 | 2 | 3

export const resultTone = { 0: 'neutral', 10: 'info', 20: 'primary', 30: 'success', 90: 'neutral' } as const
export const flagTone = { 0: 'neutral', 1: 'success', 2: 'warning', 3: 'warning', 4: 'danger', 5: 'danger', 6: 'warning' } as const

export interface PendingItem {
  orderItemId: number
  orderId: number
  orderNo: string
  patientId: number
  patientNo: string
  patientName: string
  patientNameUr: string | null
  patientSex: Sex
  patientDateOfBirth: string | null
  testId: number
  testCode: string
  testNameEn: string
  testNameUr: string | null
  departmentId: number
  departmentNameEn: string
  departmentNameUr: string | null
  sampleId: number
  barcode: string
  receivedAtUtc: string
  priority: 0 | 1
  turnaroundHours: number
  dueAtUtc: string | null
  isOverdue: boolean
  resultId: number | null
  resultStatus: ResultStatus | null
  onWorklist: string | null
}

export interface RangeDto { id: number; low: number | null; high: number | null; criticalLow: number | null; criticalHigh: number | null; displayText: string | null }
export interface OptionDto { id: number; valueEn: string; valueUr: string | null; isAbnormal: boolean }

export interface ParameterEntry {
  parameterId: number
  code: string
  nameEn: string
  nameUr: string | null
  resultType: ResultType
  unit: string | null
  decimalPlaces: number
  groupName: string | null
  sortOrder: number
  options: OptionDto[]
  range: RangeDto | null
}

export interface ValueDto {
  parameterId: number
  valueNumeric: number | null
  optionId: number | null
  valueText: string | null
  valueTextUr: string | null
  display: string | null
  unitText: string | null
  decimalPlaces: number
  flag: ResultFlag
  range: RangeDto | null
  rangeText: string | null
}

export interface ResultActions { save: boolean; submit: boolean; discard: boolean; verify: boolean; return: boolean; approve: boolean; amend: boolean; isOwnEntry: boolean }
export interface ApprovalStep { step: string; atUtc: string; userName: string | null; comment: string | null }

export interface ResultSummary {
  id: number
  version: number
  status: ResultStatus
  rowVersion: string
  comment: string | null
  enteredByName: string | null
  submittedAtUtc: string | null
  verifiedByName: string | null
  verifiedAtUtc: string | null
  approvedByName: string | null
  approvedAtUtc: string | null
  amendmentReason: string | null
  replacesResultId: number | null
}

export interface AlertDto {
  id: number
  resultId: number
  orderItemId: number
  patientId: number
  patientNo: string
  patientName: string
  patientNameUr: string | null
  patientMobile: string | null
  testCode: string
  testNameEn: string
  testNameUr: string | null
  parameterId: number
  parameterNameEn: string
  parameterNameUr: string | null
  valueText: string
  unit: string | null
  limit: number | null
  isHigh: boolean
  status: AlertStatus
  createdAtUtc: string
  notifiedTo: string | null
  method: NotifyMethod | null
  readBackConfirmed: boolean
  notifiedByName: string | null
  notifiedAtUtc: string | null
  comment: string | null
}

export interface EntryModel {
  orderItemId: number
  orderId: number
  orderNo: string
  priority: 0 | 1
  itemStatus: number
  patientId: number
  patientNo: string
  patientName: string
  patientNameUr: string | null
  patientSex: Sex
  patientDateOfBirth: string | null
  ageDays: number | null
  testId: number
  testCode: string
  testNameEn: string
  testNameUr: string | null
  reportNotes: string | null
  sampleId: number
  barcode: string
  collectedAtUtc: string | null
  receivedAtUtc: string
  labBranch: string
  parameters: ParameterEntry[]
  current: ResultSummary | null
  values: ValueDto[]
  released: ResultSummary | null
  history: ApprovalStep[]
  alerts: AlertDto[]
  actions: ResultActions
}

export interface QueueItem {
  resultId: number
  version: number
  status: ResultStatus
  orderItemId: number
  orderNo: string
  patientId: number
  patientNo: string
  patientName: string
  patientNameUr: string | null
  testCode: string
  testNameEn: string
  testNameUr: string | null
  barcode: string
  priority: 0 | 1
  enteredByName: string | null
  submittedAtUtc: string | null
  verifiedByName: string | null
  criticalCount: number
  abnormalCount: number
  hasOpenAlert: boolean
  isAmendment: boolean
  isOwnEntry: boolean
}

export interface OrderResult { orderItemId: number; testCode: string; testNameEn: string; testNameUr: string | null; itemStatus: number; resultId: number | null; resultStatus: ResultStatus | null; version: number | null; hasReleased: boolean }

export interface WorklistListItem { id: number; worklistNo: string; status: 0 | 1; departmentId: number; departmentNameEn: string; departmentNameUr: string | null; itemCount: number; doneCount: number; createdAtUtc: string; createdByName: string | null; notes: string | null }
export interface WorklistItem {
  orderItemId: number
  barcode: string
  orderNo: string
  patientNo: string
  patientName: string
  patientNameUr: string | null
  patientSex: Sex
  patientDateOfBirth: string | null
  testCode: string
  testNameEn: string
  testNameUr: string | null
  sampleTypeNameEn: string
  sampleTypeNameUr: string | null
  priority: 0 | 1
  resultStatus: ResultStatus | null
  parameterCodes: string[]
}
export interface WorklistDetail { worklist: WorklistListItem; branchName: string; items: WorklistItem[]; rowVersion: string }

export const resultKeys = {
  all: ['results'] as const,
  pending: (p: object) => ['results', 'pending', p] as const,
  queue: (p: object) => ['results', 'queue', p] as const,
  entry: (itemId: number) => ['results', 'entry', itemId] as const,
  ofOrder: (orderId: number) => ['results', 'order', orderId] as const,
  worklists: (p: object) => ['results', 'worklists', p] as const,
  worklist: (id: number) => ['results', 'worklist', id] as const,
  alerts: (p: object) => ['results', 'alerts', p] as const,
  alertCount: ['results', 'alert-count'] as const,
}

/** Arabic-Indic and Urdu digits, and the Arabic decimal separator, become plain digits (as the server does). */
export function normaliseDigits(s: string): string {
  return s
    .replace(/[٠-٩]/g, (c) => String(c.charCodeAt(0) - 0x0660))
    .replace(/[۰-۹]/g, (c) => String(c.charCodeAt(0) - 0x06f0))
    .replace(/٫/g, '.')
    .replace(/−/g, '-')
}

/** Same grammar the server accepts: optional sign, digits, one point, at most 6 decimals. */
export function parseNumber(input: string): number | null {
  const s = normaliseDigits(input.trim())
  if (!/^[+-]?(\d+\.?\d*|\.\d+)$/.test(s)) return null
  const decimals = s.includes('.') ? s.length - s.indexOf('.') - 1 : 0
  if (decimals > 6) return null
  const n = Number(s)
  return Number.isFinite(n) && Math.abs(n) < 1e12 ? n : null
}

/**
 * Preview of the flag while typing, using the approved range sent by the server. It compares numbers with the limits
 * only; the saved flag (from the server) is the one that counts.
 */
export function previewFlag(value: number, range: RangeDto | null): ResultFlag {
  if (!range) return 0
  if (range.criticalLow !== null && value < range.criticalLow) return 4
  if (range.criticalHigh !== null && value > range.criticalHigh) return 5
  if (range.low === null && range.high === null) return 0
  if (range.low !== null && value < range.low) return 2
  if (range.high !== null && value > range.high) return 3
  return 1
}

export function rangeText(r: RangeDto | null, places: number): string | null {
  if (!r) return null
  if (r.displayText) return r.displayText
  const f = (n: number) => n.toFixed(Math.min(Math.max(places, 0), 6))
  if (r.low !== null && r.high !== null) return `${f(r.low)} – ${f(r.high)}`
  if (r.low !== null) return `≥ ${f(r.low)}`
  if (r.high !== null) return `≤ ${f(r.high)}`
  return null
}

/** Opens print for the dialog that is currently open (worklist sheet); the print stylesheet hides everything else. */
export function printOpenDialog() {
  const style = document.createElement('style')
  style.textContent = '@page { size: A4; margin: 12mm; }'
  document.head.appendChild(style)
  document.body.classList.add('printing-dialog')
  const done = () => { style.remove(); document.body.classList.remove('printing-dialog') }
  window.addEventListener('afterprint', done, { once: true })
  window.print()
}
