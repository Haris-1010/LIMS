import { useEffect, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, Trash2, Wallet } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime, formatMoney } from '../../lib/format'
import { useSession } from '../../app/session'
import { Button } from '../../components/ui/Button'
import { SearchInput, SelectField, Switch, TextAreaField } from '../../components/ui/Controls'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/Toast'
import type { Paged } from '../admin/adminApi'
import type { TestListItem } from '../catalog/catalogApi'
import { useRefreshCorporate, clientName } from './corporateHooks'
import { corporateKeys, type ClientDetail, type ClientListItem, type Contract, type CorporateInvoice, type Receipt } from './corporateApi'

// ---------------- client form ----------------

export function ClientFormModal({ client, onClose, onSaved }: { client: ClientDetail | null; onClose: () => void; onSaved?: (id: number) => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshCorporate()
  const [f, setF] = useState({
    code: client?.code ?? '', nameEn: client?.nameEn ?? '', nameUr: client?.nameUr ?? '', contactName: client?.contactName ?? '', phone: client?.phone ?? '', email: client?.email ?? '',
    addressEn: client?.addressEn ?? '', creditLimit: client?.creditLimit?.toString() ?? '', terms: String(client?.paymentTermsDays ?? 30), isActive: client?.isActive ?? true,
  })
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value })
  const save = useMutation({
    mutationFn: () => {
      const body = { code: f.code.trim(), nameEn: f.nameEn.trim(), nameUr: f.nameUr.trim() || null, contactName: f.contactName.trim() || null, phone: f.phone.trim() || null, email: f.email.trim() || null,
        addressEn: f.addressEn.trim() || null, creditLimit: f.creditLimit.trim() === '' ? null : Number(f.creditLimit), paymentTermsDays: Number(f.terms) }
      return client ? api<ClientDetail>(`/api/corporate/clients/${client.id}`, { method: 'PUT', body: { ...body, isActive: f.isActive, rowVersion: client.rowVersion } }) : api<ClientDetail>('/api/corporate/clients', { method: 'POST', body })
    },
    onSuccess: async (c) => { toast.show({ tone: 'success', title: t('common.saved') }); await refresh(); onSaved?.(c.id); onClose() },
    onError: (e) => toast.show({ tone: 'danger', title: t('corporate.actionFailed'), text: errorMessage(t, e) }),
  })
  return (
    <Modal open title={client ? t('corporate.editClient') : t('corporate.newClient')} onClose={onClose} size="md"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={save.isPending} disabled={!f.code.trim() || !f.nameEn.trim()} onClick={() => save.mutate()}>{t('common.save')}</Button></>}>
      <div className="form-stack">
        <div className="form-grid">
          <Field label={t('common.code')} required disabled={!!client} value={f.code} maxLength={20} onChange={set('code')} />
          <Field label={t('corporate.paymentTerms')} type="number" min={0} max={365} value={f.terms} onChange={set('terms')} hint={t('corporate.paymentTermsHint')} />
          <Field label={t('common.nameEn')} required value={f.nameEn} maxLength={200} onChange={set('nameEn')} />
          <Field label={t('common.nameUr')} dir="rtl" value={f.nameUr} maxLength={200} onChange={set('nameUr')} />
          <Field label={t('corporate.contactName')} value={f.contactName} maxLength={200} onChange={set('contactName')} />
          <Field label={t('common.phone')} dir="ltr" value={f.phone} maxLength={30} onChange={set('phone')} />
          <Field label={t('common.email')} type="email" dir="ltr" value={f.email} maxLength={256} onChange={set('email')} />
          <Field label={t('corporate.creditLimit')} type="number" min={0} value={f.creditLimit} onChange={set('creditLimit')} hint={t('corporate.creditLimitHint')} />
        </div>
        <Field label={t('common.addressEn')} value={f.addressEn} maxLength={300} onChange={set('addressEn')} />
        {client && <Switch checked={f.isActive} onChange={(v) => setF({ ...f, isActive: v })} label={t('common.active')} />}
      </div>
    </Modal>
  )
}

// ---------------- contract ----------------

interface PriceRow { testId: number; code: string; name: string; catalogPrice: number; price: string }

export function ContractModal({ clientId, contract, onClose }: { clientId: number; contract: Contract | null; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshCorporate()
  const [name, setName] = useState(contract?.name ?? '')
  const [from, setFrom] = useState(contract?.validFrom ?? new Date().toISOString().slice(0, 10))
  const [to, setTo] = useState(contract?.validTo ?? '')
  const [discount, setDiscount] = useState(String(contract?.discountPercent ?? 0))
  const [active, setActive] = useState(contract?.isActive ?? true)
  const [rows, setRows] = useState<PriceRow[]>(contract?.prices.map((p) => ({ testId: p.testId, code: p.testCode, name: i18n.language === 'ur' && p.testNameUr ? p.testNameUr : p.testNameEn, catalogPrice: p.catalogPrice, price: String(p.price) })) ?? [])
  const [search, setSearch] = useState('')
  const found = useQuery({ queryKey: ['catalog', 'tests', 'picker', search], queryFn: () => api<Paged<TestListItem>>('/api/catalog/tests', { query: { search, pageSize: 8, status: 'active' } }), enabled: search.trim().length >= 2 })

  const save = useMutation({
    mutationFn: () => {
      const body = { name: name.trim(), validFrom: from, validTo: to || null, discountPercent: Number(discount), prices: rows.map((r) => ({ testId: r.testId, price: Number(r.price) })) }
      return contract
        ? api(`/api/corporate/clients/contracts/${contract.id}`, { method: 'PUT', body: { ...body, isActive: active, rowVersion: contract.rowVersion } })
        : api(`/api/corporate/clients/${clientId}/contracts`, { method: 'POST', body })
    },
    onSuccess: async () => { toast.show({ tone: 'success', title: t('common.saved') }); await refresh(); onClose() },
    onError: (e) => toast.show({ tone: 'danger', title: t('corporate.actionFailed'), text: errorMessage(t, e) }),
  })
  const add = (x: TestListItem) => {
    if (rows.some((r) => r.testId === x.id)) return
    setRows([...rows, { testId: x.id, code: x.code, name: localOf(x), catalogPrice: x.price, price: String(x.price) }])
    setSearch('')
  }
  const localOf = (x: TestListItem) => (i18n.language === 'ur' && x.nameUr ? x.nameUr : x.nameEn)
  const valid = name.trim() && from && rows.every((r) => r.price !== '' && Number(r.price) >= 0) && Number(discount) >= 0 && Number(discount) <= 100

  return (
    <Modal open title={contract ? t('corporate.editContract') : t('corporate.newContract')} subtitle={t('corporate.contractHint')} onClose={onClose} size="lg"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={save.isPending} disabled={!valid} onClick={() => save.mutate()}>{t('common.save')}</Button></>}>
      <div className="form-stack">
        <div className="form-grid">
          <Field label={t('corporate.contractName')} required value={name} maxLength={200} onChange={(e) => setName(e.target.value)} />
          <Field label={t('corporate.discountPercent')} type="number" min={0} max={100} step="0.01" value={discount} onChange={(e) => setDiscount(e.target.value)} />
          <Field label={t('corporate.validFrom')} type="date" required value={from} onChange={(e) => setFrom(e.target.value)} />
          <Field label={t('corporate.validTo')} type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} hint={t('corporate.validToHint')} />
        </div>
        {contract && <Switch checked={active} onChange={setActive} label={t('common.active')} />}
        <div>
          <h4 className="form-section-title">{t('corporate.fixedPrices')}</h4>
          <SearchInput value={search} onChange={setSearch} placeholder={t('corporate.searchTests')} delay={200} />
          {found.data && search.trim().length >= 2 && (
            <ul className="picker-list">
              {found.data.items.map((x) => <li key={x.id}><button type="button" onClick={() => add(x)}><span className="num">{x.code}</span><span dir="auto">{localOf(x)}</span><span className="num">{formatMoney(x.price)}</span></button></li>)}
              {found.data.items.length === 0 && <li className="picker-empty">{t('common.search')}: —</li>}
            </ul>
          )}
          {rows.length > 0 && (
            <div className="table-wrap" style={{ marginTop: 8 }}>
              <table className="table">
                <thead><tr><th>{t('corporate.test')}</th><th style={{ textAlign: 'end' }}>{t('corporate.catalogPrice')}</th><th style={{ width: 150 }}>{t('corporate.contractPrice')}</th><th /></tr></thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={r.testId}>
                      <td><span dir="auto" className="cell-strong">{r.name}</span><span className="cell-sub num">{r.code}</span></td>
                      <td className="num" style={{ textAlign: 'end' }}>{formatMoney(r.catalogPrice)}</td>
                      <td><input className="input" type="number" min={0} step="0.01" dir="ltr" value={r.price} aria-label={r.code} onChange={(e) => setRows(rows.map((x) => (x.testId === r.testId ? { ...x, price: e.target.value } : x)))} /></td>
                      <td><button type="button" className="icon-btn" aria-label={t('common.delete')} onClick={() => setRows(rows.filter((x) => x.testId !== r.testId))}><Trash2 size={15} /></button></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </Modal>
  )
}

// ---------------- receipt and allocation ----------------

export function ReceiptModal({ clientId, onClose, onCreated }: { clientId?: number; onClose: () => void; onCreated: (r: Receipt) => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const { can } = useSession()
  const refresh = useRefreshCorporate()
  const [key] = useState(() => crypto.randomUUID())
  const [client, setClient] = useState(clientId ? String(clientId) : '')
  const [kind, setKind] = useState<1 | 2>(can('corporate.receive') ? 1 : 2)
  const [method, setMethod] = useState('3')
  const [amount, setAmount] = useState('')
  const [reference, setReference] = useState('')
  const [reason, setReason] = useState('')
  const clients = useQuery({ queryKey: corporateKeys.clients({ options: true }), queryFn: () => api<Paged<ClientListItem>>('/api/corporate/clients', { query: { pageSize: 100 } }), enabled: !clientId })
  const create = useMutation({
    mutationFn: () => api<Receipt>('/api/corporate/receipts', { method: 'POST', body: { idempotencyKey: key, clientId: Number(client), kind, method: kind === 1 ? Number(method) : null, amount: Number(amount), reference: reference.trim() || null, reason: reason.trim() || null } }),
    onSuccess: async (r) => { toast.show({ tone: 'success', title: t('corporate.receiptRecorded'), text: r.receiptNo }); await refresh(); onCreated(r) },
    onError: (e) => toast.show({ tone: 'danger', title: t('corporate.actionFailed'), text: errorMessage(t, e) }),
  })
  const ok = client && Number(amount) > 0 && (kind === 1 || reason.trim())
  return (
    <Modal open title={kind === 1 ? t('corporate.newReceipt') : t('corporate.newCreditNote')} subtitle={t('corporate.receiptHint')} onClose={onClose} size="md"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" icon={<Wallet size={16} />} loading={create.isPending} disabled={!ok} onClick={() => create.mutate()}>{t('corporate.record')}</Button></>}>
      <div className="form-stack">
        {!clientId && (
          <SelectField label={t('corporate.client')} required value={client} onChange={(e) => setClient(e.target.value)}>
            <option value="">{t('corporate.chooseClient')}</option>
            {clients.data?.items.map((c) => <option key={c.id} value={c.id}>{clientName(c, i18n.language)} ({c.code})</option>)}
          </SelectField>
        )}
        {can('corporate.receive') && can('corporate.adjust') && (
          <SelectField label={t('corporate.kind')} value={kind} onChange={(e) => setKind(Number(e.target.value) as 1 | 2)}>
            <option value={1}>{t('corporate.kindReceipt')}</option>
            <option value={2}>{t('corporate.kindCreditNote')}</option>
          </SelectField>
        )}
        <Field label={t('orders.amount')} type="number" min={0} step="0.01" required dir="ltr" value={amount} onChange={(e) => setAmount(e.target.value)} />
        {kind === 1 ? (
          <>
            <SelectField label={t('orders.method')} value={method} onChange={(e) => setMethod(e.target.value)}>
              {[2, 3, 4, 5].map((m) => <option key={m} value={m}>{t(`paymentMethod.${m}`)}</option>)}
            </SelectField>
            <Field label={t('orders.reference')} value={reference} maxLength={100} onChange={(e) => setReference(e.target.value)} />
          </>
        ) : <TextAreaField label={`${t('corporate.creditReason')} *`} value={reason} maxLength={500} rows={3} onChange={(e) => setReason(e.target.value)} />}
      </div>
    </Modal>
  )
}

/** Match a receipt to the invoices it pays. Amounts are capped by what the receipt has left and what each invoice owes; the server checks again. */
export function AllocateModal({ receipt, onClose }: { receipt: Receipt; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshCorporate()
  const [amounts, setAmounts] = useState<Record<number, string>>({})
  const invoices = useQuery({
    queryKey: corporateKeys.invoices({ allocate: receipt.clientId }),
    queryFn: () => api<Paged<CorporateInvoice>>('/api/corporate/invoices', { query: { clientId: receipt.clientId, outstandingOnly: true, pageSize: 100 } }),
  })
  // Oldest invoices first (lowest id), the usual way to apply a payment.
  const list = [...(invoices.data?.items ?? [])].sort((a, b) => a.invoiceId - b.invoiceId)
  const total = Object.values(amounts).reduce((s, v) => s + (Number(v) || 0), 0)
  const left = receipt.unallocated - total
  useEffect(() => setAmounts({}), [receipt.id])

  const auto = () => {
    let remaining = receipt.unallocated
    const next: Record<number, string> = {}
    for (const i of list) { const take = Math.min(remaining, i.balance); if (take > 0) { next[i.invoiceId] = take.toFixed(2); remaining = Math.round((remaining - take) * 100) / 100 } }
    setAmounts(next)
  }
  const allocate = useMutation({
    mutationFn: () => api<Receipt>(`/api/corporate/receipts/${receipt.id}/allocate`, { method: 'POST', body: { rowVersion: receipt.rowVersion, items: Object.entries(amounts).filter(([, v]) => Number(v) > 0).map(([id, v]) => ({ invoiceId: Number(id), amount: Number(v) })) } }),
    onSuccess: async () => { toast.show({ tone: 'success', title: t('corporate.allocated') }); await refresh(); onClose() },
    onError: (e) => toast.show({ tone: 'danger', title: t('corporate.actionFailed'), text: errorMessage(t, e) }),
  })
  const over = left < -0.004 || list.some((i) => Number(amounts[i.invoiceId] ?? 0) > i.balance + 0.004)

  return (
    <Modal open title={t('corporate.allocateTitle')} subtitle={<span><span className="num">{receipt.receiptNo}</span> · {t('corporate.unallocated')}: <span className="num">{formatMoney(receipt.unallocated)}</span></span>} onClose={onClose} size="lg"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={allocate.isPending} disabled={total <= 0 || over} onClick={() => allocate.mutate()}>{t('corporate.allocate')}</Button></>}>
      <div className="form-stack">
        <div className="toolbar"><Button size="sm" icon={<Plus size={14} />} onClick={auto}>{t('corporate.autoAllocate')}</Button><span className={`field-hint${over ? ' text-warning' : ''}`}>{t('corporate.leftToAllocate')}: <span className="num">{formatMoney(left)}</span></span></div>
        {list.length === 0 ? <p className="field-hint">{t('corporate.noOutstanding')}</p> : (
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>{t('corporate.invoice')}</th><th>{t('corporate.due')}</th><th style={{ textAlign: 'end' }}>{t('orders.balanceDue')}</th><th style={{ width: 170 }}>{t('corporate.allocateAmount')}</th></tr></thead>
              <tbody>
                {list.map((i) => (
                  <tr key={i.invoiceId}>
                    <td><span className="num cell-strong">{i.invoiceNo}</span><span className="cell-sub" dir="auto">{i18n.language === 'ur' && i.patientNameUr ? i.patientNameUr : i.patientName}</span></td>
                    <td>{i.dueDate ?? '—'}</td>
                    <td className="num" style={{ textAlign: 'end' }}>{formatMoney(i.balance)}</td>
                    <td><input className="input" type="number" min={0} max={i.balance} step="0.01" dir="ltr" aria-label={i.invoiceNo} value={amounts[i.invoiceId] ?? ''} onChange={(e) => setAmounts({ ...amounts, [i.invoiceId]: e.target.value })} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {receipt.allocations.length > 0 && (
          <div>
            <h4 className="form-section-title">{t('corporate.alreadyAllocated')}</h4>
            <ul className="ledger">{receipt.allocations.map((a) => <li key={a.id} style={{ gridTemplateColumns: 'minmax(0,1fr) auto' }}><span><span className="num cell-strong">{a.invoiceNo}</span><span className="cell-sub">{formatDateTime(a.allocatedAtUtc, i18n.language)}{a.byName ? ` · ${a.byName}` : ''}</span></span><span className="num">{formatMoney(a.amount)}</span></li>)}</ul>
          </div>
        )}
      </div>
    </Modal>
  )
}
