import type { Sex } from '../patients/patientsApi'

/* Contracts for /api/orders and /api/billing (see Lims.Application.Orders). */

export type PaymentMethod = 1 | 2 | 3 | 4 | 5
export type InvoiceStatus = 0 | 1 | 2 | 3 | 4
export type OrderItemStatus = 0 | 10 | 20 | 30 | 40 | 90

export const METHODS: PaymentMethod[] = [1, 2, 3, 4, 5]

export interface QuoteLine {
  kind: 1 | 2
  testId: number | null
  packageId: number | null
  code: string
  nameEn: string
  nameUr: string | null
  price: number
  testCodes: string[]
}
export interface Quote { lines: QuoteLine[]; gross: number; discountAmount: number; net: number }

export interface InvoiceLineDto {
  id: number
  kind: 1 | 2
  code: string
  nameEn: string
  nameUr: string | null
  price: number
  isCancelled: boolean
  cancelReason: string | null
  testCodes: string[]
  canCancel: boolean
}
export interface PaymentDto { id: number; receiptNo: string; method: PaymentMethod; amount: number; reference: string | null; receivedAtUtc: string; receivedByName: string | null }
export interface RefundDto { id: number; refundNo: string; method: PaymentMethod; amount: number; reason: string; refundedAtUtc: string; refundedByName: string | null }

export interface InvoiceDto {
  id: number
  invoiceNo: string
  gross: number
  discountPercent: number | null
  discountAmount: number
  discountReason: string | null
  net: number
  paid: number
  refunded: number
  balance: number
  status: InvoiceStatus
  lines: InvoiceLineDto[]
  payments: PaymentDto[]
  refunds: RefundDto[]
}

export interface OrderDetail {
  id: number
  orderNo: string
  createdAtUtc: string
  createdByName: string | null
  branchId: number
  branchName: string
  branchNameUr: string | null
  patient: { id: number; patientNo: string; fullName: string; fullNameUr: string | null; title: string | null; sex: Sex; dateOfBirth: string | null; mobile: string | null }
  referringDoctorId: number | null
  referringDoctorName: string | null
  priority: 0 | 1
  clinicalNotes: string | null
  status: 0 | 1
  cancelReason: string | null
  items: { id: number; invoiceLineId: number; testCode: string; testNameEn: string; testNameUr: string | null; status: OrderItemStatus }[]
  invoice: InvoiceDto
}

export interface OrderListItem {
  id: number
  orderNo: string
  createdAtUtc: string
  patientId: number
  patientNo: string
  patientName: string
  patientNameUr: string | null
  doctorName: string | null
  testCount: number
  net: number
  balance: number
  invoiceStatus: InvoiceStatus
  status: 0 | 1
  priority: 0 | 1
  branchName: string
}

export interface PaymentListItem {
  id: number
  receiptNo: string
  receivedAtUtc: string
  orderId: number
  orderNo: string
  invoiceNo: string
  patientNo: string
  patientName: string
  method: PaymentMethod
  amount: number
  reference: string | null
  receivedByName: string | null
}

export interface DrawerDto {
  id: number
  branchId: number
  userId: number
  userName: string | null
  status: 0 | 1
  openedAtUtc: string
  openingFloat: number
  cashIn: number
  cashOut: number
  expectedCash: number
  closedAtUtc: string | null
  countedCash: number | null
  variance: number | null
  notes: string | null
}

export interface DaySummary {
  date: string
  branchId: number | null
  byMethod: { method: PaymentMethod; received: number; refunded: number; count: number }[]
  totalReceived: number
  totalRefunded: number
  orderCount: number
}

export const orderKeys = {
  list: (p: object) => ['orders', 'list', p] as const,
  detail: (id: number) => ['orders', 'detail', id] as const,
  payments: (p: object) => ['billing', 'payments', p] as const,
  drawer: ['billing', 'drawer'] as const,
  drawers: ['billing', 'drawers'] as const,
  summary: ['billing', 'summary'] as const,
}

export const invoiceTone = { 0: 'danger', 1: 'warning', 2: 'success', 3: 'info', 4: 'neutral' } as const

/** A fresh one-time key per form instance; resubmitting the same form can never create a second record. */
export const newKey = () => crypto.randomUUID()
