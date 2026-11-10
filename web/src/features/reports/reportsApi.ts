import type { Sex } from '../patients/patientsApi'

/* Contracts for /api/reports, /api/orders/{id}/reports and /api/report-template (see Lims.Application.Reports). */

export type ReportStatus = 0 | 1 | 90
export type ReportLanguage = 0 | 1 | 2
export type DocLang = 'en' | 'ur' | 'both'
export const docLang = (l: ReportLanguage): DocLang => (l === 0 ? 'en' : l === 1 ? 'ur' : 'both')

export const reportTone = { 0: 'warning', 1: 'success', 90: 'neutral' } as const

export interface SnapLetterhead {
  organizationNameEn: string
  organizationNameUr: string | null
  labNameEn: string
  labNameUr: string | null
  addressEn: string | null
  addressUr: string | null
  phone: string | null
  email: string | null
  headerNoteEn: string | null
  headerNoteUr: string | null
  footerNoteEn: string | null
  footerNoteUr: string | null
  disclaimerEn: string | null
  disclaimerUr: string | null
}
export interface SnapPatient {
  id: number
  patientNo: string
  nameEn: string
  nameUr: string | null
  sex: Sex
  dateOfBirth: string | null
  isDateOfBirthEstimated: boolean
  ageYears: number | null
  ageMonths: number | null
  ageDays: number | null
  mobile: string | null
}
export interface SnapOrder {
  orderId: number
  orderNo: string
  priority: 0 | 1
  branchNameEn: string
  branchNameUr: string | null
  referringDoctor: string | null
  registeredAtUtc: string
  collectedAtUtc: string | null
  receivedAtUtc: string | null
}
export interface SnapParameter {
  code: string
  nameEn: string
  nameUr: string | null
  groupName: string | null
  resultType: 1 | 2 | 3 | 4
  display: string
  displayUr: string | null
  unit: string | null
  flag: 0 | 1 | 2 | 3 | 4 | 5 | 6
  flagCode: string
  referenceText: string | null
}
export interface SnapTest {
  orderItemId: number
  code: string
  nameEn: string
  nameUr: string | null
  reportNotes: string | null
  resultVersion: number
  approvedAtUtc: string
  comment: string | null
  parameters: SnapParameter[]
}
export interface SnapSection { departmentNameEn: string; departmentNameUr: string | null; tests: SnapTest[] }
export interface SnapPendingTest { code: string; nameEn: string; nameUr: string | null }
export interface SnapSignatory { userId: number; nameEn: string; nameUr: string | null; designation: string | null; registrationNo: string | null; hasSignature: boolean; approved: boolean }

export interface ReportSnapshot {
  schemaVersion: number
  reportNo: string
  version: number
  language: ReportLanguage
  status: ReportStatus
  issuedAtUtc: string
  issuedByName: string
  issuedByNameUr: string | null
  amendmentReason: string | null
  replacesReportNo: string | null
  letterhead: SnapLetterhead
  patient: SnapPatient
  order: SnapOrder
  sections: SnapSection[]
  pending: SnapPendingTest[]
  signatories: SnapSignatory[]
}

export interface ReportMeta {
  id: number | null
  reportNo: string
  version: number
  status: ReportStatus
  language: ReportLanguage
  issuedAtUtc: string
  issuedByName: string
  printCount: number
  isDuplicate: boolean
  isOutdated: boolean
  fingerprint: string
  supersededByVersion: number | null
}
export interface ReportView { meta: ReportMeta; snapshot: ReportSnapshot; isPreview: boolean }

export interface ReportListItem {
  orderId: number
  orderNo: string
  patientId: number
  patientNo: string
  patientName: string
  patientNameUr: string | null
  branchName: string
  releasedCount: number
  activeTestCount: number
  latestApprovedAtUtc: string | null
  reportId: number | null
  reportNo: string | null
  version: number | null
  status: ReportStatus | null
  language: ReportLanguage | null
  issuedAtUtc: string | null
  isOutdated: boolean
  printCount: number
}

export interface ReportVersion { id: number; reportNo: string; version: number; status: ReportStatus; language: ReportLanguage; issuedAtUtc: string; issuedByName: string; amendmentReason: string | null; printCount: number; isOutdated: boolean }

export interface TemplateText { headerNote: string | null; footerNote: string | null; disclaimer: string | null }
export interface TemplateDto { en: TemplateText; ur: TemplateText; hasLogo: boolean; logoContentType: string | null; rowVersion: string | null }

export const reportKeys = {
  all: ['reports'] as const,
  list: (p: object) => ['reports', 'list', p] as const,
  versions: (orderId: number) => ['reports', 'versions', orderId] as const,
  view: (id: number) => ['reports', 'view', id] as const,
  preview: (orderId: number, lang: number) => ['reports', 'preview', orderId, lang] as const,
  template: ['reports', 'template'] as const,
}
