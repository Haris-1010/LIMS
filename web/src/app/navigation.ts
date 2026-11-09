import type { LucideIcon } from 'lucide-react'
import {
  Activity,
  AlertTriangle,
  Archive,
  Banknote,
  Boxes,
  Building2,
  ClipboardCheck,
  ClipboardList,
  DatabaseBackup,
  FileText,
  FlaskConical,
  KeyRound,
  LayoutDashboard,
  Microscope,
  ReceiptText,
  ScrollText,
  Settings,
  Stethoscope,
  TestTubes,
  Truck,
  Users,
  UserRound,
  Wallet,
} from 'lucide-react'

/**
 * Single source of truth for the sidebar and route titles.
 * `permission` is the permission key a user needs to see the item; the security module (Phase 3)
 * filters this tree by the signed-in user's permissions.
 */
export interface NavItem {
  key: string // translation key under nav.*
  path?: string
  icon?: LucideIcon
  permission?: string
  phase?: number // implementation phase that delivers this page
  children?: NavItem[]
}

export interface NavGroup {
  key: string // translation key under nav.groups.*
  items: NavItem[]
}

export const navigation: NavGroup[] = [
  {
    key: 'overview',
    items: [{ key: 'dashboard', path: '/', icon: LayoutDashboard, permission: 'dashboard.view', phase: 12 }],
  },
  {
    key: 'frontDesk',
    items: [
      { key: 'patients', path: '/patients', icon: UserRound, permission: 'patients.view', phase: 4 },
      {
        key: 'orders',
        icon: ClipboardList,
        permission: 'orders.view',
        children: [
          { key: 'newOrder', path: '/orders/new', permission: 'orders.create', phase: 6 },
          { key: 'orderList', path: '/orders', permission: 'orders.view', phase: 6 },
        ],
      },
      { key: 'doctors', path: '/doctors', icon: Stethoscope, permission: 'doctors.view', phase: 4 },
    ],
  },
  {
    key: 'laboratory',
    items: [
      {
        key: 'samples',
        icon: TestTubes,
        permission: 'samples.view',
        children: [
          { key: 'collection', path: '/samples/collection', permission: 'samples.collect', phase: 7 },
          { key: 'tracking', path: '/samples/tracking', permission: 'samples.view', phase: 7 },
          { key: 'rejections', path: '/samples/rejections', permission: 'samples.reject', phase: 7 },
        ],
      },
      { key: 'worklists', path: '/worklists', icon: Microscope, permission: 'results.enter', phase: 8 },
      {
        key: 'results',
        icon: ClipboardCheck,
        permission: 'results.view',
        children: [
          { key: 'resultEntry', path: '/results/entry', permission: 'results.enter', phase: 8 },
          { key: 'verification', path: '/results/verification', permission: 'results.approve', phase: 8 },
        ],
      },
      { key: 'criticalAlerts', path: '/critical-alerts', icon: AlertTriangle, permission: 'results.critical', phase: 8 },
      { key: 'reports', path: '/reports', icon: FileText, permission: 'reports.view', phase: 9 },
    ],
  },
  {
    key: 'billing',
    items: [
      { key: 'invoices', path: '/billing/invoices', icon: ReceiptText, permission: 'billing.view', phase: 6 },
      { key: 'payments', path: '/billing/payments', icon: Banknote, permission: 'payments.collect', phase: 6 },
      {
        key: 'corporate',
        icon: Building2,
        permission: 'corporate.view',
        children: [
          { key: 'corporateClients', path: '/corporate/clients', permission: 'corporate.view', phase: 10 },
          { key: 'corporateInvoices', path: '/corporate/invoices', permission: 'corporate.invoice', phase: 10 },
          { key: 'corporateReceipts', path: '/corporate/receipts', permission: 'corporate.receive', phase: 10 },
        ],
      },
      { key: 'cashDrawer', path: '/billing/cash-drawer', icon: Wallet, permission: 'cash.close', phase: 6 },
    ],
  },
  {
    key: 'inventory',
    items: [
      { key: 'stockItems', path: '/inventory/items', icon: Boxes, permission: 'inventory.view', phase: 11 },
      { key: 'purchases', path: '/inventory/purchases', icon: Archive, permission: 'inventory.purchase', phase: 11 },
      { key: 'suppliers', path: '/inventory/suppliers', icon: Truck, permission: 'inventory.view', phase: 11 },
      { key: 'stockLedger', path: '/inventory/ledger', icon: Activity, permission: 'inventory.view', phase: 11 },
    ],
  },
  {
    key: 'configuration',
    items: [
      {
        key: 'catalog',
        icon: FlaskConical,
        permission: 'catalog.view',
        children: [
          { key: 'tests', path: '/catalog/tests', permission: 'catalog.view', phase: 5 },
          { key: 'categories', path: '/catalog/categories', permission: 'catalog.view', phase: 5 },
          { key: 'packages', path: '/catalog/packages', permission: 'catalog.view', phase: 5 },
          { key: 'referenceRanges', path: '/catalog/reference-ranges', permission: 'catalog.clinical', phase: 5 },
        ],
      },
      { key: 'branches', path: '/admin/branches', icon: Building2, permission: 'org.manage', phase: 3 },
    ],
  },
  {
    key: 'administration',
    items: [
      { key: 'users', path: '/admin/users', icon: Users, permission: 'users.manage', phase: 3 },
      { key: 'roles', path: '/admin/roles', icon: KeyRound, permission: 'roles.manage', phase: 3 },
      { key: 'audit', path: '/admin/audit', icon: ScrollText, permission: 'audit.view', phase: 12 },
      { key: 'settings', path: '/admin/settings', icon: Settings, permission: 'settings.manage', phase: 12 },
      { key: 'backup', path: '/admin/backup', icon: DatabaseBackup, permission: 'backup.manage', phase: 13 },
    ],
  },
]

export interface RouteMeta {
  item: NavItem
  group: NavGroup
  parent?: NavItem
}

/** Flat list of every navigable page with its group and parent, for routing and breadcrumbs. */
export const routeIndex: RouteMeta[] = navigation.flatMap((group) =>
  group.items.flatMap((item) =>
    item.children
      ? item.children.map((child) => ({ item: child, group, parent: item }))
      : [{ item, group }],
  ),
)

/**
 * Exact match first; otherwise the longest menu path that prefixes the URL, so detail pages such as
 * /patients/42 inherit the title, breadcrumbs and permission of /patients.
 */
export function findRoute(pathname: string): RouteMeta | undefined {
  const exact = routeIndex.find((r) => r.item.path === pathname)
  if (exact) return exact
  return routeIndex
    .filter((r) => r.item.path && r.item.path !== '/' && pathname.startsWith(`${r.item.path}/`))
    .sort((a, b) => b.item.path!.length - a.item.path!.length)[0]
}
