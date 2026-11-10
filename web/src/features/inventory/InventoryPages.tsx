import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { AlertTriangle, Boxes, ClipboardList, PackagePlus, Plus, Truck } from 'lucide-react'
import { api } from '../../lib/api'
import { formatDateTime, formatNumber } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { SearchInput, SelectField, Switch, Tabs } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Modal } from '../../components/ui/Modal'
import type { Paged } from '../admin/adminApi'
import { ItemFormModal, PurchaseModal, ResolveShortfallModal, SupplierFormModal } from './InventoryParts'
import { inventoryKeys, itemName, MOVEMENT_TYPES, useInventoryAlerts, useSuppliers, type ItemListItem, type Movement, type Purchase, type Shortfall, type Supplier } from './inventoryApi'

const PAGE_SIZE = 25

// ---------------- stock items ----------------

export function StockItemsPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { can } = useSession()
  const [search, setSearch] = useState('')
  const [lowOnly, setLowOnly] = useState(false)
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const alerts = useInventoryAlerts()
  const params = { search, lowOnly: lowOnly || undefined, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: inventoryKeys.items(params), queryFn: () => api<Paged<ItemListItem>>('/api/inventory/items', { query: params }), placeholderData: keepPreviousData })
  const a = alerts.data

  const columns: Column<ItemListItem>[] = [
    { key: 'code', header: t('common.code'), width: 130, render: (i) => <span className="num cell-strong">{i.code}</span> },
    { key: 'name', header: t('inventory.item'), render: (i) => <span><span dir="auto">{itemName(i, i18n.language)}</span><span className="cell-sub">{t(`inventory.kind${i.kind}`)}</span></span> },
    {
      key: 'onHand', header: t('inventory.onHand'), align: 'end',
      render: (i) => <span><span className="num cell-strong">{formatNumber(i.onHand)}</span> <span className="field-hint">{i.unitName}</span>{i.isLow && <> <Badge tone="warning">{t('inventory.low')}</Badge></>}</span>,
    },
    { key: 'reorder', header: t('inventory.reorderLevel'), align: 'end', render: (i) => (i.reorderLevel > 0 ? <span className="num">{formatNumber(i.reorderLevel)}</span> : <span className="field-hint">—</span>) },
    {
      key: 'expiry', header: t('inventory.expiry'),
      render: (i) => (i.expiredQuantity > 0 ? <Badge tone="danger">{t('inventory.expiredQty', { count: i.expiredQuantity })}</Badge> : i.expiringQuantity > 0 ? <Badge tone="warning">{t('inventory.expiringQty', { count: i.expiringQuantity })}</Badge> : <span className="field-hint">—</span>),
    },
    { key: 'status', header: t('common.status'), render: (i) => <Badge tone={i.isActive ? 'success' : 'neutral'} dot>{i.isActive ? t('common.active') : t('common.inactive')}</Badge> },
  ]

  return (
    <div className="page">
      <div className="page-head">
        <div><h2>{t('inventory.itemsTitle')}</h2><p>{t('inventory.itemsSubtitle')}</p></div>
        {can('inventory.manage') && <Button variant="primary" icon={<Plus size={16} />} onClick={() => setCreating(true)}>{t('inventory.newItem')}</Button>}
      </div>
      {a && (a.low.length + a.expiring.length + a.expired.length + a.openShortfalls > 0) && (
        <section className="card">
          <div className="card-body" style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
            <AlertTriangle size={16} aria-hidden />
            {a.low.length > 0 && <Badge tone="warning">{t('inventory.alertLow', { count: a.low.length })}</Badge>}
            {a.expiring.length > 0 && <Badge tone="warning">{t('inventory.alertExpiring', { count: a.expiring.length, days: a.expiryAlertDays })}</Badge>}
            {a.expired.length > 0 && <Badge tone="danger">{t('inventory.alertExpired', { count: a.expired.length })}</Badge>}
            {a.openShortfalls > 0 && <Link to="/inventory/ledger?tab=shortfalls"><Badge tone="danger">{t('inventory.alertShortfalls', { count: a.openShortfalls })}</Badge></Link>}
          </div>
        </section>
      )}
      <section className="card">
        <div className="card-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('inventory.searchItems')} />
          <Switch checked={lowOnly} onChange={(v) => { setLowOnly(v); setPage(1) }} label={t('inventory.lowOnly')} />
        </div>
        <DataTable columns={columns} rows={data?.items} rowKey={(i) => i.id} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage} onRowClick={(i) => navigate(`/inventory/items/${i.id}`)}
          empty={search || lowOnly ? undefined : <EmptyState icon={<Boxes size={22} />} title={t('inventory.noItemsTitle')} text={t('inventory.noItemsText')} />} />
      </section>
      {creating && <ItemFormModal item={null} onClose={() => setCreating(false)} onSaved={(id) => navigate(`/inventory/items/${id}`)} />}
    </div>
  )
}

// ---------------- suppliers ----------------

export function SuppliersPage() {
  const { t, i18n } = useTranslation()
  const { can } = useSession()
  const [showInactive, setShowInactive] = useState(false)
  const [editing, setEditing] = useState<Supplier | 'new' | null>(null)
  const { data, isLoading } = useSuppliers(showInactive)
  const columns: Column<Supplier>[] = [
    { key: 'code', header: t('common.code'), width: 120, render: (s) => <span className="num cell-strong">{s.code}</span> },
    { key: 'name', header: t('inventory.supplier'), render: (s) => <span><span dir="auto">{itemName(s, i18n.language)}</span>{s.contactName && <span className="cell-sub" dir="auto">{s.contactName}</span>}</span> },
    { key: 'phone', header: t('common.phone'), render: (s) => (s.phone ? <span className="num" dir="ltr">{s.phone}</span> : '—') },
    { key: 'email', header: t('common.email'), render: (s) => (s.email ? <span dir="ltr">{s.email}</span> : '—') },
    { key: 'status', header: t('common.status'), render: (s) => <Badge tone={s.isActive ? 'success' : 'neutral'} dot>{s.isActive ? t('common.active') : t('common.inactive')}</Badge> },
  ]
  return (
    <div className="page">
      <div className="page-head">
        <div><h2>{t('inventory.suppliersTitle')}</h2><p>{t('inventory.suppliersSubtitle')}</p></div>
        {can('inventory.manage') && <Button variant="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>{t('inventory.newSupplier')}</Button>}
      </div>
      <section className="card">
        <div className="card-toolbar"><Switch checked={showInactive} onChange={setShowInactive} label={t('inventory.showInactive')} /></div>
        <DataTable columns={columns} rows={data} rowKey={(s) => s.id} loading={isLoading} onRowClick={can('inventory.manage') ? (s) => setEditing(s) : undefined}
          empty={<EmptyState icon={<Truck size={22} />} title={t('inventory.noSuppliersTitle')} text={t('inventory.noSuppliersText')} />} />
      </section>
      {editing && <SupplierFormModal supplier={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </div>
  )
}

// ---------------- purchases ----------------

export function PurchasesPage() {
  const { t, i18n } = useTranslation()
  const { can } = useSession()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const [viewing, setViewing] = useState<Purchase | null>(null)
  const params = { search, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: inventoryKeys.purchases(params), queryFn: () => api<Paged<Purchase>>('/api/inventory/purchases', { query: params }), placeholderData: keepPreviousData })
  const columns: Column<Purchase>[] = [
    { key: 'no', header: t('inventory.receiptNo'), width: 150, render: (p) => <span className="num cell-strong">{p.receiptNo}</span> },
    { key: 'supplier', header: t('inventory.supplier'), render: (p) => <span><span dir="auto">{p.supplierName}</span>{p.supplierInvoiceNo && <span className="cell-sub num">{p.supplierInvoiceNo}</span>}</span> },
    { key: 'date', header: t('common.date'), render: (p) => formatDateTime(p.receivedAtUtc, i18n.language) },
    { key: 'branch', header: t('common.branch'), render: (p) => p.branchName },
    { key: 'lines', header: t('inventory.lines'), align: 'end', render: (p) => <span className="num">{p.lines.length}</span> },
    { key: 'total', header: t('inventory.total'), align: 'end', render: (p) => <span className="num">{formatNumber(p.total)}</span> },
  ]
  return (
    <div className="page">
      <div className="page-head">
        <div><h2>{t('inventory.purchasesTitle')}</h2><p>{t('inventory.purchasesSubtitle')}</p></div>
        {can('inventory.purchase') && <Button variant="primary" icon={<PackagePlus size={16} />} onClick={() => setCreating(true)}>{t('inventory.newPurchase')}</Button>}
      </div>
      <section className="card">
        <div className="card-toolbar"><SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('inventory.searchPurchases')} /></div>
        <DataTable columns={columns} rows={data?.items} rowKey={(p) => p.id} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage} onRowClick={setViewing}
          empty={search ? undefined : <EmptyState icon={<ClipboardList size={22} />} title={t('inventory.noPurchasesTitle')} text={t('inventory.noPurchasesText')} />} />
      </section>
      {creating && <PurchaseModal onClose={() => setCreating(false)} onCreated={(p) => { setCreating(false); setViewing(p) }} />}
      {viewing && (
        <Modal open title={viewing.receiptNo} subtitle={`${viewing.supplierName} · ${formatDateTime(viewing.receivedAtUtc, i18n.language)}`} onClose={() => setViewing(null)} size="lg" footer={<Button variant="ghost" onClick={() => setViewing(null)}>{t('common.close')}</Button>}>
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>{t('inventory.item')}</th><th>{t('inventory.batchNo')}</th><th>{t('inventory.expiryDate')}</th><th style={{ textAlign: 'end' }}>{t('inventory.quantity')}</th><th style={{ textAlign: 'end' }}>{t('inventory.unitCost')}</th></tr></thead>
              <tbody>{viewing.lines.map((l, i) => (
                <tr key={i}><td><span dir="auto">{l.itemName}</span><span className="cell-sub num">{l.itemCode}</span></td><td className="num">{l.batchNo}</td><td className="num">{l.expiryDate ?? '—'}</td><td className="num" style={{ textAlign: 'end' }}>{formatNumber(l.quantity)}</td><td className="num" style={{ textAlign: 'end' }}>{l.unitCost === null ? '—' : formatNumber(l.unitCost)}</td></tr>
              ))}</tbody>
            </table>
          </div>
          {viewing.notes && <p className="field-hint" dir="auto">{viewing.notes}</p>}
        </Modal>
      )}
    </div>
  )
}

// ---------------- ledger and shortfalls ----------------

export function LedgerPage() {
  const { t, i18n } = useTranslation()
  const { can } = useSession()
  const [tab, setTab] = useState<'ledger' | 'shortfalls'>(new URLSearchParams(window.location.search).get('tab') === 'shortfalls' ? 'shortfalls' : 'ledger')
  const alerts = useInventoryAlerts()
  return (
    <div className="page">
      <div className="page-head"><div><h2>{t('inventory.ledgerTitle')}</h2><p>{t('inventory.ledgerSubtitle')}</p></div></div>
      <Tabs tabs={[{ key: 'ledger', label: t('inventory.tabLedger') }, { key: 'shortfalls', label: t('inventory.tabShortfalls'), count: alerts.data?.openShortfalls }]} value={tab} onChange={setTab} />
      {tab === 'ledger' ? <LedgerTable /> : <ShortfallTable canResolve={can('inventory.adjust')} lang={i18n.language} />}
    </div>
  )
}

function LedgerTable() {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const [itemId, setItemId] = useState('')
  const [type, setType] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const items = useQuery({ queryKey: inventoryKeys.items({ options: true }), queryFn: () => api<Paged<ItemListItem>>('/api/inventory/items', { query: { pageSize: 100 } }), staleTime: 30_000 })
  const params = { search, itemId: itemId || undefined, type: type || undefined, from: from || undefined, to: to || undefined, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: inventoryKeys.ledger(params), queryFn: () => api<Paged<Movement>>('/api/inventory/ledger', { query: params }), placeholderData: keepPreviousData })
  const reset = <T,>(set: (v: T) => void) => (v: T) => { set(v); setPage(1) }
  const columns: Column<Movement>[] = [
    { key: 'at', header: t('common.date'), width: 170, render: (m) => formatDateTime(m.atUtc, i18n.language) },
    { key: 'item', header: t('inventory.item'), render: (m) => <span><span dir="auto">{i18n.language === 'ur' && m.itemNameUr ? m.itemNameUr : m.itemName}</span><span className="cell-sub num">{m.itemCode} · {m.batchNo}</span></span> },
    { key: 'type', header: t('inventory.movement'), render: (m) => <Badge tone={m.quantity > 0 ? 'success' : 'neutral'}>{t(`inventory.movementType.${m.type}`)}</Badge> },
    { key: 'qty', header: t('inventory.quantity'), align: 'end', render: (m) => <span className="num cell-strong" dir="ltr">{m.quantity > 0 ? '+' : ''}{formatNumber(m.quantity)}</span> },
    { key: 'bal', header: t('inventory.balanceAfter'), align: 'end', render: (m) => <span className="num">{formatNumber(m.balanceAfter)}</span> },
    { key: 'ref', header: t('inventory.reference'), render: (m) => <span><span className="num">{m.reference ?? '—'}</span>{m.reason && <span className="cell-sub" dir="auto">{m.reason}</span>}</span> },
    { key: 'branch', header: t('common.branch'), render: (m) => <span>{m.branchName}{m.userName && <span className="cell-sub">{m.userName}</span>}</span> },
  ]
  return (
    <section className="card">
      <div className="card-toolbar" style={{ flexWrap: 'wrap', gap: 8 }}>
        <SearchInput value={search} onChange={reset(setSearch)} placeholder={t('inventory.searchLedger')} />
        <SelectField label={t('inventory.item')} value={itemId} onChange={(e) => reset(setItemId)(e.target.value)}>
          <option value="">{t('common.all')}</option>
          {items.data?.items.map((i) => <option key={i.id} value={i.id}>{i.code} · {itemName(i, i18n.language)}</option>)}
        </SelectField>
        <SelectField label={t('inventory.movement')} value={type} onChange={(e) => reset(setType)(e.target.value)}>
          <option value="">{t('common.all')}</option>
          {MOVEMENT_TYPES.map((m) => <option key={m} value={m}>{t(`inventory.movementType.${m}`)}</option>)}
        </SelectField>
        <label className="field-inline"><span className="field-hint">{t('common.from')}</span><input className="input" type="date" dir="ltr" value={from} max={to || undefined} onChange={(e) => reset(setFrom)(e.target.value)} /></label>
        <label className="field-inline"><span className="field-hint">{t('common.to')}</span><input className="input" type="date" dir="ltr" value={to} min={from || undefined} onChange={(e) => reset(setTo)(e.target.value)} /></label>
      </div>
      <DataTable columns={columns} rows={data?.items} rowKey={(m) => m.id} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage}
        empty={<EmptyState icon={<ClipboardList size={22} />} title={t('inventory.noMovementsTitle')} text={t('inventory.noMovementsText')} />} />
    </section>
  )
}

function ShortfallTable({ canResolve, lang }: { canResolve: boolean; lang: string }) {
  const { t } = useTranslation()
  const [openOnly, setOpenOnly] = useState(true)
  const [page, setPage] = useState(1)
  const [resolving, setResolving] = useState<Shortfall | null>(null)
  const params = { openOnly, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: inventoryKeys.shortfalls(params), queryFn: () => api<Paged<Shortfall>>('/api/inventory/shortfalls', { query: params }), placeholderData: keepPreviousData })
  const columns: Column<Shortfall>[] = [
    { key: 'at', header: t('common.date'), width: 170, render: (s) => formatDateTime(s.atUtc, lang) },
    { key: 'item', header: t('inventory.item'), render: (s) => <span><span dir="auto">{s.itemName}</span><span className="cell-sub num">{s.itemCode}</span></span> },
    { key: 'qty', header: t('inventory.missing'), align: 'end', render: (s) => <span><span className="num cell-strong">{formatNumber(s.quantity)}</span> <span className="field-hint">{s.unitName}</span></span> },
    { key: 'branch', header: t('common.branch'), render: (s) => s.branchName },
    { key: 'status', header: t('common.status'), render: (s) => (s.resolvedAtUtc ? <span><Badge tone="success" dot>{t('inventory.resolved')}</Badge>{s.resolutionNote && <span className="cell-sub" dir="auto">{s.resolutionNote}</span>}</span> : <Badge tone="danger" dot>{t('inventory.open')}</Badge>) },
    { key: 'act', header: '', align: 'end', render: (s) => (!s.resolvedAtUtc && canResolve ? <Button size="sm" onClick={() => setResolving(s)}>{t('inventory.resolve')}</Button> : null) },
  ]
  return (
    <section className="card">
      <div className="card-toolbar"><Switch checked={openOnly} onChange={(v) => { setOpenOnly(v); setPage(1) }} label={t('inventory.openOnly')} /></div>
      <p className="field-hint" style={{ padding: '0 16px' }}>{t('inventory.shortfallHint')}</p>
      <DataTable columns={columns} rows={data?.items} rowKey={(s) => s.id} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage}
        empty={<EmptyState icon={<AlertTriangle size={22} />} title={t('inventory.noShortfallsTitle')} text={t('inventory.noShortfallsText')} />} />
      {resolving && <ResolveShortfallModal shortfall={resolving} onClose={() => setResolving(null)} />}
    </section>
  )
}
