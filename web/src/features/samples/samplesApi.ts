import type { Sex } from '../patients/patientsApi'

/* Contracts for /api/samples and /api/sample-transfers (see Lims.Application.Samples). */

export type SampleStatus = 0 | 10 | 15 | 20 | 80 | 90
export const SAMPLE_STATUSES: SampleStatus[] = [0, 10, 15, 20, 80, 90]
export type TransferStatus = 0 | 1 | 2

export const sampleTone = { 0: 'neutral', 10: 'info', 15: 'warning', 20: 'success', 80: 'danger', 90: 'neutral' } as const
export const transferTone = { 0: 'warning', 1: 'success', 2: 'neutral' } as const

export interface SampleListItem {
  id: number
  barcode: string
  barcodeModules: string
  status: SampleStatus
  priority: 0 | 1
  orderId: number
  orderNo: string
  patientId: number
  patientNo: string
  patientName: string
  patientNameUr: string | null
  patientSex: Sex
  patientDateOfBirth: string | null
  sampleTypeId: number
  sampleTypeNameEn: string
  sampleTypeNameUr: string | null
  capColour: string | null
  container: string | null
  testCodes: string[]
  originBranch: string
  currentBranch: string
  destinationBranch: string | null
  createdAtUtc: string
  collectedAtUtc: string | null
  isRecollection: boolean
  labelPrintCount: number
}

export interface SampleTestDto { orderItemId: number; code: string; nameEn: string; nameUr: string | null; status: number }
export interface SampleEventDto { type: string; atUtc: string; userName: string | null; branchName: string | null; notes: string | null }
export interface SampleActions { collect: boolean; receive: boolean; reject: boolean; dispatch: boolean; recollect: boolean; printLabel: boolean }

export interface SampleDetail {
  sample: SampleListItem
  patientMobile: string | null
  doctorName: string | null
  orderBranch: string | null
  tests: SampleTestDto[]
  identityConfirmed: boolean
  collectionNotes: string | null
  collectedByName: string | null
  receivedAtUtc: string | null
  receivedByName: string | null
  rejectedAtUtc: string | null
  rejectedByName: string | null
  rejectionReason: string | null
  rejectionReasonUr: string | null
  rejectionNote: string | null
  replacesSampleId: number | null
  replacesBarcode: string | null
  replacedBySampleId: number | null
  replacedByBarcode: string | null
  events: SampleEventDto[]
  actions: SampleActions
  rowVersion: string
}

export interface OrderSampleDto {
  id: number
  barcode: string
  barcodeModules: string
  status: SampleStatus
  sampleTypeNameEn: string
  sampleTypeNameUr: string | null
  capColour: string | null
  testCodes: string[]
  currentBranch: string
  collectedAtUtc: string | null
  isRecollection: boolean
}

export interface RejectionReason { id: number; code: string; nameEn: string; nameUr: string | null; requiresNote: boolean; isActive: boolean; sortOrder: number; rowVersion: string }

export interface TransferListItem {
  id: number
  transferNo: string
  status: TransferStatus
  fromBranch: string
  toBranch: string
  fromBranchId: number
  toBranchId: number
  dispatchedAtUtc: string
  dispatchedByName: string | null
  sampleCount: number
  receivedCount: number
  missingCount: number
}
export interface TransferItem { sampleId: number; barcode: string; patientName: string; sampleTypeNameEn: string; sampleTypeNameUr: string | null; receivedAtUtc: string | null; markedMissing: boolean; sampleStatus: SampleStatus }
export interface TransferDetail {
  transfer: TransferListItem
  notes: string | null
  completedAtUtc: string | null
  completedByName: string | null
  items: TransferItem[]
  canReceive: boolean
  canCancel: boolean
  canComplete: boolean
}

export const sampleKeys = {
  all: ['samples'] as const,
  list: (p: object) => ['samples', 'list', p] as const,
  queue: ['samples', 'queue'] as const,
  detail: (id: number) => ['samples', 'detail', id] as const,
  ofOrder: (orderId: number) => ['samples', 'order', orderId] as const,
  reasons: (all: boolean) => ['samples', 'reasons', all] as const,
  transfers: (p: object) => ['samples', 'transfers', p] as const,
  transfer: (id: number) => ['samples', 'transfer', id] as const,
}

/** Names the lab staff read: the Urdu name when the interface is Urdu and one exists. */
export const pick = (en: string, ur: string | null, lang: string) => (lang === 'ur' && ur ? ur : en)
