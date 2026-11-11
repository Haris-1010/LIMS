import type { LucideIcon } from 'lucide-react'
import {
  Boxes,
  Building2,
  ClipboardCheck,
  ClipboardList,
  FileText,
  FlaskConical,
  LayoutDashboard,
  PlusCircle,
  ReceiptText,
  Settings,
  TestTubes,
  UserRound,
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

/*
 * Kept short on purpose (owner, 2026-11-12): four groups and about twelve top-level entries,
 * daily work first. Less-used pages sit one level down under a parent; every page is still here.
 * A parent without its own permission shows when the user can open at least one of its children.
 */
export const navigation: NavGroup[] = [
  {
    key: 'overview',
    items: [{ key: 'dashboard', path: '/', icon: LayoutDashboard, permission: 'dashboard.view', phase: 12 }],
  },
  {
    key: 'dailyWork',
    items: [
      { key: 'newOrder', path: '/orders/new', icon: PlusCircle, permission: 'orders.create', phase: 6 },
      { key: 'orderList', path: '/orders', icon: ClipboardList, permission: 'orders.view', phase: 6 },
      {
        key: 'patientsMenu',
        icon: UserRound,
        children: [
          { key: 'patients', path: '/patients', permission: 'patients.view', phase: 4 },
          { key: 'doctors', path: '/doctors', permission: 'doctors.view', phase: 4 },
        ],
      },
      {
        key: 'samples',
        icon: TestTubes,
        permission: 'samples.view',
        children: [
          { key: 'collection', path: '/samples/collection', permission: 'samples.collect', phase: 7 },
          { key: 'receiving', path: '/samples/receiving', permission: 'samples.receive', phase: 7 },
          { key: 'transfers', path: '/samples/transfers', permission: 'samples.view', phase: 7 },
          { key: 'tracking', path: '/samples/tracking', permission: 'samples.view', phase: 7 },
          { key: 'rejections', path: '/samples/rejections', permission: 'samples.reject', phase: 7 },
        ],
      },
      {
        key: 'results',
        icon: ClipboardCheck,
        children: [
          { key: 'worklists', path: '/worklists', permission: 'results.enter', phase: 8 },
          { key: 'resultEntry', path: '/results/entry', permission: 'results.enter', phase: 8 },
          { key: 'verification', path: '/results/verification', permission: 'results.verify', phase: 8 },
          { key: 'criticalAlerts', path: '/critical-alerts', permission: 'results.critical', phase: 8 },
        ],
      },
      { key: 'reports', path: '/reports', icon: FileText, permission: 'reports.view', phase: 9 },
    ],
  },
  {
    key: 'accounts',
    items: [
      {
        key: 'billingMenu',
        icon: ReceiptText,
        children: [
          { key: 'invoices', path: '/billing/invoices', permission: 'billing.view', phase: 6 },
          { key: 'payments', path: '/billing/payments', permission: 'payments.collect', phase: 6 },
          { key: 'cashDrawer', path: '/billing/cash-drawer', permission: 'cash.close', phase: 6 },
        ],
      },
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
      {
        key: 'inventoryMenu',
        icon: Boxes,
        children: [
          { key: 'stockItems', path: '/inventory/items', permission: 'inventory.view', phase: 11 },
          { key: 'purchases', path: '/inventory/purchases', permission: 'inventory.purchase', phase: 11 },
          { key: 'suppliers', path: '/inventory/suppliers', permission: 'inventory.view', phase: 11 },
          { key: 'stockLedger', path: '/inventory/ledger', permission: 'inventory.view', phase: 11 },
        ],
      },
    ],
  },
  {
    key: 'setup',
    items: [
      {
        key: 'labSetup',
        icon: FlaskConical,
        children: [
          { key: 'tests', path: '/catalog/tests', permission: 'catalog.view', phase: 5 },
          { key: 'packages', path: '/catalog/packages', permission: 'catalog.view', phase: 5 },
          { key: 'categories', path: '/catalog/categories', permission: 'catalog.view', phase: 5 },
          { key: 'referenceRanges', path: '/catalog/reference-ranges', permission: 'catalog.clinical', phase: 5 },
          { key: 'reportTemplate', path: '/reports/template', permission: 'reports.templates', phase: 9 },
        ],
      },
      {
        key: 'adminMenu',
        icon: Settings,
        children: [
          { key: 'users', path: '/admin/users', permission: 'users.manage', phase: 3 },
          { key: 'roles', path: '/admin/roles', permission: 'roles.manage', phase: 3 },
          { key: 'branches', path: '/admin/branches', permission: 'org.manage', phase: 3 },
          { key: 'settings', path: '/admin/settings', permission: 'settings.manage', phase: 12 },
          { key: 'backup', path: '/admin/backup', permission: 'backup.manage', phase: 13 },
          { key: 'audit', path: '/admin/audit', permission: 'audit.view', phase: 12 },
        ],
      },
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
