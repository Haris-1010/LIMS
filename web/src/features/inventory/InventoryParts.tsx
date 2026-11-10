import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, Trash2 } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatNumber } from '../../lib/format'
import { useSession } from '../../app/session'
import { Button } from '../../components/ui/Button'
import { SearchInput, SelectField, Switch, TextAreaField } from '../../components/ui/Controls'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/Toast'
import type { Paged } from '../admin/adminApi'
import type { TestListItem } from '../catalog/catalogApi'
import { inventoryKeys, itemName, useRefreshInventory, useSuppliers, type Batch, type ItemDetail, type ItemListItem, type ManualMovement, type Purchase, type Shortfall, type Supplier } from './inventoryApi'

// ---------------- item ----------------

export function ItemFormModal({ item, onClose, onSaved }: { item: ItemDetail | null; onClose: () => void; onSaved?: (id: number) => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshInventory()
  const suppliers = useSuppliers()
  const [f, setF] = useState({
    code: item?.code ?? '', nameEn: item?.nameEn ?? '', nameUr: item?.nameUr ?? '', kind: String(item?.kind ?? 1), unitName: item?.unitName ?? '', reorderLevel: String(item?.reorderLevel ?? 0),
    tracksExpiry: item?.tracksExpiry ?? true, supplierId: item?.defaultSupplierId ? String(item.defaultSupplierId) : '', isActive: item?.isActive ?? true,
  })
  const set = (k: 'code' | 'nameEn' | 'nameUr' | 'unitName' | 'reorderLevel') => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value })
  const save = useMutation({
    mutationFn: () => {
      const body = { code: f.code.trim(), nameEn: f.nameEn.trim(), nameUr: f.nameUr.trim() || null, kind: Number(f.kind), unitName: f.unitName.trim(), reorderLevel: Number(f.reorderLevel) || 0, tracksExpiry: f.tracksExpiry, defaultSupplierId: f.supplierId ? Number(f.supplierId) : null }
      return item ? api<ItemDetail>(`/api/inventory/items/${item.id}`, { method: 'PUT', body: { ...body, isActive: f.isActive, rowVersion: item.rowVersion } }) : api<ItemDetail>('/api/inventory/items', { method: 'POST', body })
    },
    onSuccess: async (d) => { toast.show({ tone: 'success', title: t('common.saved') }); await refresh(); onSaved?.(d.id); onClose() },
    onError: (e) => toast.show({ tone: 'danger', title: t('inventory.actionFailed'), text: errorMessage(t, e) }),
  })
  return (
    <Modal open title={item ? t('inventory.editItem') : t('inventory.newItem')} onClose={onClose} size="md"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={save.isPending} disabled={!f.code.trim() || !f.nameEn.trim() || !f.unitName.trim()} onClick={() => save.mutate()}>{t('common.save')}</Button></>}>
      <div className="form-stack">
        <div className="form-grid">
          <Field label={t('common.code')} required disabled={!!item} value={f.code} maxLength={30} dir="ltr" onChange={set('code')} />
          <SelectField label={t('inventory.kind')} value={f.kind} onChange={(e) => setF({ ...f, kind: e.target.value })}>
            <option value="1">{t('inventory.kind1')}</option>
            <option value="2">{t('inventory.kind2')}</option>
          </SelectField>
          <Field label={t('common.nameEn')} required value={f.nameEn} maxLength={200} onChange={set('nameEn')} />
          <Field label={t('common.nameUr')} dir="rtl" value={f.nameUr} maxLength={200} onChange={set('nameUr')} />
          <Field label={t('inventory.unitName')} required value={f.unitName} maxLength={30} onChange={set('unitName')} hint={t('inventory.unitNameHint')} />
          <Field label={t('inventory.reorderLevel')} type="number" min={0} step="0.001" dir="ltr" value={f.reorderLevel} onChange={set('reorderLevel')} hint={t('inventory.reorderLevelHint')} />
          <SelectField label={t('inventory.defaultSupplier')} value={f.supplierId} onChange={(e) => setF({ ...f, supplierId: e.target.value })}>
            <option value="">—</option>
            {suppliers.data?.map((s) => <option key={s.id} value={s.id}>{itemName(s, i18n.language)}</option>)}
          </SelectField>
        </div>
        <Switch checked={f.tracksExpiry} onChange={(v) => setF({ ...f, tracksExpiry: v })} label={t('inventory.tracksExpiry')} />
        {item && <Switch checked={f.isActive} onChange={(v) => setF({ ...f, isActive: v })} label={t('common.active')} />}
      </div>
    </Modal>
  )
}

// ---------------- supplier ----------------

export function SupplierFormModal({ supplier, onClose }: { supplier: Supplier | null; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshInventory()
  const [f, setF] = useState({ code: supplier?.code ?? '', nameEn: supplier?.nameEn ?? '', nameUr: supplier?.nameUr ?? '', contactName: supplier?.contactName ?? '', phone: supplier?.phone ?? '', email: supplier?.email ?? '', address: supplier?.address ?? '', isActive: supplier?.isActive ?? true })
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value })
  const save = useMutation({
    mutationFn: () => {
      const body = { code: f.code.trim(), nameEn: f.nameEn.trim(), nameUr: f.nameUr.trim() || null, contactName: f.contactName.trim() || null, phone: f.phone.trim() || null, email: f.email.trim() || null, address: f.address.trim() || null }
      return supplier ? api<Supplier[]>(`/api/inventory/suppliers/${supplier.id}`, { method: 'PUT', body: { ...body, isActive: f.isActive, rowVersion: supplier.rowVersion } }) : api<Supplier[]>('/api/inventory/suppliers', { method: 'POST', body })
    },
    onSuccess: async () => { toast.show({ tone: 'success', title: t('common.saved') }); await refresh(); onClose() },
    onError: (e) => toast.show({ tone: 'danger', title: t('inventory.actionFailed'), text: errorMessage(t, e) }),
  })
  return (
    <Modal open title={supplier ? t('inventory.editSupplier') : t('inventory.newSupplier')} onClose={onClose} size="md"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={save.isPending} disabled={!f.code.trim() || !f.nameEn.trim()} onClick={() => save.mutate()}>{t('common.save')}</Button></>}>
      <div className="form-stack">
        <div className="form-grid">
          <Field label={t('common.code')} required disabled={!!supplier} value={f.code} maxLength={20} dir="ltr" onChange={set('code')} />
          <Field label={t('inventory.contactName')} value={f.contactName} maxLength={200} onChange={set('contactName')} />
          <Field label={t('common.nameEn')} required value={f.nameEn} maxLength={200} onChange={set('nameEn')} />
          <Field label={t('common.nameUr')} dir="rtl" value={f.nameUr} maxLength={200} onChange={set('nameUr')} />
          <Field label={t('common.phone')} dir="ltr" value={f.phone} maxLength={30} onChange={set('phone')} />
          <Field label={t('common.email')} type="email" dir="ltr" value={f.email} maxLength={256} onChange={set('email')} />
        </div>
        <Field label={t('common.address')} value={f.address} maxLength={300} onChange={set('address')} />
        {supplier && <Switch checked={f.isActive} onChange={(v) => setF({ ...f, isActive: v })} label={t('common.active')} />}
      </div>
    </Modal>
  )
}

// ---------------- purchase ----------------

interface Row { key: number; itemId: string; batchNo: string; expiry: string; quantity: string; cost: string }

/** Stock received from a supplier: one receipt, any number of batches. The form carries a one-time key so a double click records it once. */
export function PurchaseModal({ onClose, onCreated }: { onClose: () => void; onCreated: (p: Purchase) => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshInventory()
  const [key] = useState(() => crypto.randomUUID())
  const [supplier, setSupplier] = useState('')
  const [invoiceNo, setInvoiceNo] = useState('')
  const [notes, setNotes] = useState('')
  const [rows, setRows] = useState<Row[]>([{ key: 1, itemId: '', batchNo: '', expiry: '', quantity: '', cost: '' }])
  const suppliers = useSuppliers()
  const items = useQuery({ queryKey: inventoryKeys.items({ options: true }), queryFn: () => api<Paged<ItemListItem>>('/api/inventory/items', { query: { isActive: true, pageSize: 100 } }), staleTime: 30_000 })
  const itemOf = (id: string) => items.data?.items.find((i) => String(i.id) === id)
  const setRow = (k: number, patch: Partial<Row>) => setRows(rows.map((r) => (r.key === k ? { ...r, ...patch } : r)))

  const create = useMutation({
    mutationFn: () => api<Purchase>('/api/inventory/purchases', {
      method: 'POST',
      body: {
        idempotencyKey: key, supplierId: Number(supplier), supplierInvoiceNo: invoiceNo.trim() || null, notes: notes.trim() || null,
        lines: rows.map((r) => ({ itemId: Number(r.itemId), batchNo: r.batchNo.trim(), expiryDate: r.expiry || null, quantity: Number(r.quantity), unitCost: r.cost === '' ? null : Number(r.cost) })),
      },
    }),
    onSuccess: async (p) => { toast.show({ tone: 'success', title: t('inventory.purchaseRecorded'), text: p.receiptNo }); await refresh(); onCreated(p) },
    onError: (e) => toast.show({ tone: 'danger', title: t('inventory.actionFailed'), text: errorMessage(t, e) }),
  })
  const valid = supplier && rows.every((r) => r.itemId && r.batchNo.trim() && Number(r.quantity) > 0 && (!itemOf(r.itemId)?.tracksExpiry || r.expiry))

  return (
    <Modal open title={t('inventory.newPurchase')} subtitle={t('inventory.purchaseHint')} onClose={onClose} size="xl"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={create.isPending} disabled={!valid} onClick={() => create.mutate()}>{t('inventory.record')}</Button></>}>
      <div className="form-stack">
        <div className="form-grid">
          <SelectField label={t('inventory.supplier')} required value={supplier} onChange={(e) => setSupplier(e.target.value)}>
            <option value="">{t('inventory.chooseSupplier')}</option>
            {suppliers.data?.filter((s) => s.isActive).map((s) => <option key={s.id} value={s.id}>{itemName(s, i18n.language)}</option>)}
          </SelectField>
          <Field label={t('inventory.supplierInvoiceNo')} value={invoiceNo} maxLength={60} onChange={(e) => setInvoiceNo(e.target.value)} />
        </div>
        <div className="table-wrap">
          <table className="table">
            <thead><tr><th>{t('inventory.item')}</th><th style={{ width: 150 }}>{t('inventory.batchNo')}</th><th style={{ width: 160 }}>{t('inventory.expiryDate')}</th><th style={{ width: 110 }}>{t('inventory.quantity')}</th><th style={{ width: 110 }}>{t('inventory.unitCost')}</th><th /></tr></thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.key}>
                  <td>
                    <select className="input" aria-label={t('inventory.item')} value={r.itemId} onChange={(e) => setRow(r.key, { itemId: e.target.value })}>
                      <option value="">{t('inventory.chooseItem')}</option>
                      {items.data?.items.map((i) => <option key={i.id} value={i.id}>{i.code} · {itemName(i, i18n.language)} ({i.unitName})</option>)}
                    </select>
                  </td>
                  <td><input className="input" aria-label={t('inventory.batchNo')} dir="ltr" maxLength={60} value={r.batchNo} onChange={(e) => setRow(r.key, { batchNo: e.target.value })} /></td>
                  <td><input className="input" type="date" aria-label={t('inventory.expiryDate')} dir="ltr" value={r.expiry} disabled={r.itemId !== '' && !itemOf(r.itemId)?.tracksExpiry} onChange={(e) => setRow(r.key, { expiry: e.target.value })} /></td>
                  <td><input className="input" type="number" min={0} step="0.001" aria-label={t('inventory.quantity')} dir="ltr" value={r.quantity} onChange={(e) => setRow(r.key, { quantity: e.target.value })} /></td>
                  <td><input className="input" type="number" min={0} step="0.01" aria-label={t('inventory.unitCost')} dir="ltr" value={r.cost} onChange={(e) => setRow(r.key, { cost: e.target.value })} /></td>
                  <td>{rows.length > 1 && <button type="button" className="icon-btn" aria-label={t('common.delete')} onClick={() => setRows(rows.filter((x) => x.key !== r.key))}><Trash2 size={15} /></button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div><Button size="sm" icon={<Plus size={14} />} onClick={() => setRows([...rows, { key: Math.max(...rows.map((r) => r.key)) + 1, itemId: '', batchNo: '', expiry: '', quantity: '', cost: '' }])}>{t('inventory.addLine')}</Button></div>
        <TextAreaField label={t('common.notes')} value={notes} maxLength={500} rows={2} onChange={(e) => setNotes(e.target.value)} />
      </div>
    </Modal>
  )
}

// ---------------- manual change and transfer ----------------

export function MovementModal({ batch, item, kind, onClose }: { batch: Batch; item: ItemDetail; kind: ManualMovement; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshInventory()
  const [qty, setQty] = useState('')
  const [reason, setReason] = useState('')
  const needsReason = kind !== 1
  const stockOut = kind !== 2
  const save = useMutation({
    mutationFn: () => api('/api/inventory/movements', { method: 'POST', body: { batchId: batch.id, kind, quantity: Number(qty), reason: reason.trim() || null } }),
    onSuccess: async () => { toast.show({ tone: 'success', title: t('inventory.stockUpdated') }); await refresh(); onClose() },
    onError: (e) => toast.show({ tone: 'danger', title: t('inventory.actionFailed'), text: errorMessage(t, e) }),
  })
  const tooMuch = stockOut && Number(qty) > batch.quantityOnHand
  return (
    <Modal open title={t(`inventory.manual${kind}`)} subtitle={<span><span className="num">{item.code}</span> · {t('inventory.batchNo')} <span className="num">{batch.batchNo}</span> · {t('inventory.onHand')}: <span className="num">{formatNumber(batch.quantityOnHand)}</span> {item.unitName}</span>} onClose={onClose} size="sm"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={save.isPending} disabled={!(Number(qty) > 0) || tooMuch || (needsReason && !reason.trim())} onClick={() => save.mutate()}>{t('common.save')}</Button></>}>
      <div className="form-stack">
        <Field label={`${t('inventory.quantity')} (${item.unitName})`} type="number" min={0} step="0.001" required dir="ltr" value={qty} onChange={(e) => setQty(e.target.value)} error={tooMuch ? t('errors.insufficient_stock') : undefined} />
        <TextAreaField label={needsReason ? `${t('inventory.reason')} *` : t('inventory.reason')} value={reason} maxLength={300} rows={2} onChange={(e) => setReason(e.target.value)} />
      </div>
    </Modal>
  )
}

export function TransferModal({ batch, item, onClose }: { batch: Batch; item: ItemDetail; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const { user } = useSession()
  const toast = useToast()
  const refresh = useRefreshInventory()
  const [to, setTo] = useState('')
  const [qty, setQty] = useState('')
  const [notes, setNotes] = useState('')
  const save = useMutation({
    mutationFn: () => api('/api/inventory/transfers', { method: 'POST', body: { batchId: batch.id, toBranchId: Number(to), quantity: Number(qty), notes: notes.trim() || null } }),
    onSuccess: async () => { toast.show({ tone: 'success', title: t('inventory.transferred') }); await refresh(); onClose() },
    onError: (e) => toast.show({ tone: 'danger', title: t('inventory.actionFailed'), text: errorMessage(t, e) }),
  })
  const targets = (user?.branches ?? []).filter((b) => b.id !== batch.branchId)
  return (
    <Modal open title={t('inventory.transfer')} subtitle={<span><span className="num">{item.code}</span> · {t('inventory.batchNo')} <span className="num">{batch.batchNo}</span> · {batch.branchName} · <span className="num">{formatNumber(batch.quantityOnHand)}</span> {item.unitName}</span>} onClose={onClose} size="sm"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={save.isPending} disabled={!to || !(Number(qty) > 0) || Number(qty) > batch.quantityOnHand} onClick={() => save.mutate()}>{t('inventory.transfer')}</Button></>}>
      <div className="form-stack">
        <SelectField label={t('inventory.toBranch')} required value={to} onChange={(e) => setTo(e.target.value)}>
          <option value="">{t('inventory.chooseBranch')}</option>
          {targets.map((b) => <option key={b.id} value={b.id}>{i18n.language === 'ur' && b.nameUr ? b.nameUr : b.nameEn}</option>)}
        </SelectField>
        <Field label={`${t('inventory.quantity')} (${item.unitName})`} type="number" min={0} step="0.001" required dir="ltr" value={qty} onChange={(e) => setQty(e.target.value)} error={Number(qty) > batch.quantityOnHand ? t('errors.insufficient_stock') : undefined} />
        <TextAreaField label={t('common.notes')} value={notes} maxLength={300} rows={2} onChange={(e) => setNotes(e.target.value)} />
      </div>
    </Modal>
  )
}

// ---------------- consumption profile ----------------

interface ProfileRow { testId: number; code: string; name: string; qty: string }

/** Which tests use this item and how much each run uses. Charged to stock when a result is submitted. */
export function ProfileModal({ item, onClose }: { item: ItemDetail; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshInventory()
  const [search, setSearch] = useState('')
  const [rows, setRows] = useState<ProfileRow[]>(item.profile.map((p) => ({ testId: p.testId, code: p.testCode, name: i18n.language === 'ur' && p.testNameUr ? p.testNameUr : p.testNameEn, qty: String(p.quantityPerTest) })))
  const found = useQuery({ queryKey: ['catalog', 'tests', 'picker', search], queryFn: () => api<Paged<TestListItem>>('/api/catalog/tests', { query: { search, pageSize: 8, status: 'active' } }), enabled: search.trim().length >= 2 })
  const add = (x: TestListItem) => {
    if (rows.some((r) => r.testId === x.id)) return
    setRows([...rows, { testId: x.id, code: x.code, name: i18n.language === 'ur' && x.nameUr ? x.nameUr : x.nameEn, qty: '1' }])
    setSearch('')
  }
  const save = useMutation({
    mutationFn: () => api(`/api/inventory/items/${item.id}/profile`, { method: 'PUT', body: { tests: rows.map((r) => ({ testId: r.testId, quantityPerTest: Number(r.qty) })) } }),
    onSuccess: async () => { toast.show({ tone: 'success', title: t('common.saved') }); await refresh(); onClose() },
    onError: (e) => toast.show({ tone: 'danger', title: t('inventory.actionFailed'), text: errorMessage(t, e) }),
  })
  return (
    <Modal open title={t('inventory.profileTitle')} subtitle={t('inventory.profileHint', { unit: item.unitName })} onClose={onClose} size="lg"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={save.isPending} disabled={rows.some((r) => !(Number(r.qty) > 0))} onClick={() => save.mutate()}>{t('common.save')}</Button></>}>
      <div className="form-stack">
        <SearchInput value={search} onChange={setSearch} placeholder={t('inventory.searchTests')} delay={200} />
        {found.data && search.trim().length >= 2 && (
          <ul className="picker-list">
            {found.data.items.map((x) => <li key={x.id}><button type="button" onClick={() => add(x)}><span className="num">{x.code}</span><span dir="auto">{i18n.language === 'ur' && x.nameUr ? x.nameUr : x.nameEn}</span></button></li>)}
            {found.data.items.length === 0 && <li className="picker-empty">—</li>}
          </ul>
        )}
        {rows.length === 0 ? <p className="field-hint">{t('inventory.noProfile')}</p> : (
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>{t('inventory.test')}</th><th style={{ width: 200 }}>{t('inventory.perTest', { unit: item.unitName })}</th><th /></tr></thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.testId}>
                    <td><span dir="auto" className="cell-strong">{r.name}</span><span className="cell-sub num">{r.code}</span></td>
                    <td><input className="input" type="number" min={0} step="0.001" dir="ltr" aria-label={r.code} value={r.qty} onChange={(e) => setRows(rows.map((x) => (x.testId === r.testId ? { ...x, qty: e.target.value } : x)))} /></td>
                    <td><button type="button" className="icon-btn" aria-label={t('common.delete')} onClick={() => setRows(rows.filter((x) => x.testId !== r.testId))}><Trash2 size={15} /></button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </Modal>
  )
}

// ---------------- shortfall ----------------

export function ResolveShortfallModal({ shortfall, onClose }: { shortfall: Shortfall; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshInventory()
  const [note, setNote] = useState('')
  const save = useMutation({
    mutationFn: () => api(`/api/inventory/shortfalls/${shortfall.id}/resolve`, { method: 'POST', body: { note: note.trim() || null } }),
    onSuccess: async () => { toast.show({ tone: 'success', title: t('inventory.shortfallResolved') }); await refresh(); onClose() },
    onError: (e) => toast.show({ tone: 'danger', title: t('inventory.actionFailed'), text: errorMessage(t, e) }),
  })
  return (
    <Modal open title={t('inventory.resolveShortfall')} subtitle={<span><span className="num">{shortfall.itemCode}</span> · <span className="num">{formatNumber(shortfall.quantity)}</span> {shortfall.unitName} · {shortfall.branchName}</span>} onClose={onClose} size="sm"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={save.isPending} onClick={() => save.mutate()}>{t('inventory.markResolved')}</Button></>}>
      <div className="form-stack">
        <p className="field-hint">{t('inventory.resolveHint')}</p>
        <TextAreaField label={t('inventory.resolutionNote')} value={note} maxLength={300} rows={3} onChange={(e) => setNote(e.target.value)} />
      </div>
    </Modal>
  )
}
