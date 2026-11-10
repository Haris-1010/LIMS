/* Contracts for /api/corporate (see Lims.Application.Corporate). */

export type ReceiptKind = 1 | 2
export type AgingBucket = 0 | 1 | 2 | 3 | 4

export interface Aging { notDue: number; days1To30: number; days31To60: number; days61To90: number; over90: number; total: number; overdue: number }
export interface AccountSummary { clientId: number; outstanding: number; unallocatedReceipts: number; balance: number; creditLimit: number | null; available: number | null; aging: Aging }

export interface ClientListItem { id: number; code: string; nameEn: string; nameUr: string | null; contactName: string | null; phone: string | null; creditLimit: number | null; paymentTermsDays: number; isActive: boolean; balance: number; overdue: number }

export interface ContractPrice { testId: number; testCode: string; testNameEn: string; testNameUr: string | null; price: number; catalogPrice: number }
export interface Contract { id: number; name: string; validFrom: string; validTo: string | null; discountPercent: number; isActive: boolean; prices: ContractPrice[]; rowVersion: string }

export interface ClientDetail {
  id: number
  code: string
  nameEn: string
  nameUr: string | null
  contactName: string | null
  phone: string | null
  email: string | null
  addressEn: string | null
  addressUr: string | null
  creditLimit: number | null
  paymentTermsDays: number
  isActive: boolean
  rowVersion: string
  contracts: Contract[]
  account: AccountSummary
}

export interface CorporateInvoice {
  invoiceId: number
  invoiceNo: string
  orderId: number
  orderNo: string
  clientId: number
  clientName: string
  patientName: string
  patientNameUr: string | null
  createdAtUtc: string
  dueDate: string | null
  net: number
  paid: number
  balance: number
  status: number
  bucket: AgingBucket | null
}

export interface Allocation { id: number; invoiceId: number; invoiceNo: string; amount: number; allocatedAtUtc: string; byName: string | null }
export interface Receipt {
  id: number
  receiptNo: string
  clientId: number
  clientName: string
  kind: ReceiptKind
  method: number | null
  amount: number
  allocated: number
  unallocated: number
  reference: string | null
  reason: string | null
  receivedAtUtc: string
  receivedByName: string | null
  rowVersion: string
  allocations: Allocation[]
}

export interface StatementLine { date: string; type: 'Invoice' | 'Receipt' | 'CreditNote'; reference: string; description: string | null; debit: number; credit: number; balance: number }
export interface Statement { client: ClientListItem; from: string; to: string; openingBalance: number; lines: StatementLine[]; closingBalance: number; account: AccountSummary }

export interface Reconcile { ok: boolean; issues: string[] }

export const corporateKeys = {
  all: ['corporate'] as const,
  clients: (p: object) => ['corporate', 'clients', p] as const,
  client: (id: number) => ['corporate', 'client', id] as const,
  invoices: (p: object) => ['corporate', 'invoices', p] as const,
  receipts: (p: object) => ['corporate', 'receipts', p] as const,
  statement: (id: number, p: object) => ['corporate', 'statement', id, p] as const,
  reconcile: (id: number | null) => ['corporate', 'reconcile', id] as const,
}

export const bucketTone = { 0: 'neutral', 1: 'warning', 2: 'warning', 3: 'danger', 4: 'danger' } as const
