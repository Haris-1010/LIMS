/* Contracts for /api/inventory (see Lims.Application.Inventory). */
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api'
import { useSession } from '../../app/session'
import type { Tone } from '../../components/ui/Badge'

export type StockItemKind = 1 | 2
/** 0 = fine, 1 = expiring soon, 2 = expired. */
export type ExpiryState = 0 | 1 | 2
/** 1 purchase in, 2 consumption, 3 issue, 4 adjust in, 5 adjust out, 6 write-off, 7 transfer out, 8 transfer in. */
export type MovementType = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8
/** What a person may do to a batch by hand. */
export type ManualMovement = 1 | 2 | 3 | 4

export interface ItemListItem {
  id: number; code: string; nameEn: string; nameUr: string | null; kind: StockItemKind; unitName: string; reorderLevel: number
  tracksExpiry: boolean; isActive: boolean; onHand: number; isLow: boolean; expiringQuantity: number; expiredQuantity: number
}

export interface Batch { id: number; branchId: number; branchName: string; batchNo: string; expiryDate: string | null; quantityOnHand: number; unitCost: number | null; expiry: ExpiryState }
export interface ProfileLine { testId: number; testCode: string; testNameEn: string; testNameUr: string | null; quantityPerTest: number }

export interface ItemDetail {
  id: number; code: string; nameEn: string; nameUr: string | null; kind: StockItemKind; unitName: string; reorderLevel: number; tracksExpiry: boolean
  defaultSupplierId: number | null; isActive: boolean; rowVersion: string; batches: Batch[]; profile: ProfileLine[]
}

export interface Supplier { id: number; code: string; nameEn: string; nameUr: string | null; contactName: string | null; phone: string | null; email: string | null; address: string | null; isActive: boolean; rowVersion: string }

export interface PurchaseLine { itemId: number; itemCode: string; itemName: string; batchNo: string; expiryDate: string | null; quantity: number; unitCost: number | null }
export interface Purchase { id: number; receiptNo: string; supplierId: number; supplierName: string; supplierInvoiceNo: string | null; receivedAtUtc: string; branchName: string; notes: string | null; total: number; lines: PurchaseLine[] }

export interface Movement {
  id: number; atUtc: string; itemId: number; itemCode: string; itemName: string; itemNameUr: string | null; batchId: number; batchNo: string; branchId: number; branchName: string
  type: MovementType; quantity: number; balanceAfter: number; reason: string | null; reference: string | null; userName: string | null
}

export interface Shortfall { id: number; itemId: number; itemCode: string; itemName: string; unitName: string; branchId: number; branchName: string; quantity: number; reference: string | null; atUtc: string; resolvedAtUtc: string | null; resolutionNote: string | null }

export interface LowStock { itemId: number; code: string; name: string; nameUr: string | null; unitName: string; onHand: number; reorderLevel: number }
export interface ExpiryAlert { batchId: number; itemId: number; code: string; name: string; nameUr: string | null; batchNo: string; branchName: string; expiryDate: string; quantity: number; daysLeft: number }
export interface InventoryAlerts { low: LowStock[]; expiring: ExpiryAlert[]; expired: ExpiryAlert[]; openShortfalls: number; expiryAlertDays: number }

export const inventoryKeys = {
  all: ['inventory'] as const,
  items: (p?: unknown) => ['inventory', 'items', p] as const,
  item: (id: number) => ['inventory', 'item', id] as const,
  suppliers: (p?: unknown) => ['inventory', 'suppliers', p] as const,
  purchases: (p?: unknown) => ['inventory', 'purchases', p] as const,
  ledger: (p?: unknown) => ['inventory', 'ledger', p] as const,
  shortfalls: (p?: unknown) => ['inventory', 'shortfalls', p] as const,
  alerts: ['inventory', 'alerts'] as const,
}

export function useRefreshInventory() {
  const qc = useQueryClient()
  return () => qc.invalidateQueries({ queryKey: inventoryKeys.all })
}

export const itemName = (x: { nameEn: string; nameUr: string | null }, lang: string) => (lang === 'ur' && x.nameUr ? x.nameUr : x.nameEn)

export const expiryTone = (e: ExpiryState): Tone => (e === 2 ? 'danger' : e === 1 ? 'warning' : 'neutral')

/** Movement types that put stock in (positive) as opposed to taking it out. */
export const MOVEMENT_TYPES: MovementType[] = [1, 2, 3, 4, 5, 6, 7, 8]

export function useInventoryAlerts() {
  const { can } = useSession()
  return useQuery({ queryKey: inventoryKeys.alerts, queryFn: () => api<InventoryAlerts>('/api/inventory/alerts'), enabled: can('inventory.view'), staleTime: 15_000 })
}

export function useSuppliers(includeInactive = false) {
  return useQuery({ queryKey: inventoryKeys.suppliers({ includeInactive }), queryFn: () => api<Supplier[]>('/api/inventory/suppliers', { query: { includeInactive } }), staleTime: 30_000 })
}
