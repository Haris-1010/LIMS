import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, FileText, Pencil, Plus, Printer, Scale, Wallet } from 'lucide-react'
import { api, ApiError } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime, formatMoney } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { EmptyState } from '../../components/ui/EmptyState'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import { StatCard } from '../../components/ui/StatCard'
import { useToast } from '../../components/ui/Toast'
import type { Paged } from '../admin/adminApi'
import { printOpenDialog } from '../results/resultsApi'
import { bucketTone, corporateKeys, type ClientDetail, type Contract, type CorporateInvoice, type Receipt, type Reconcile, type Statement } from './corporateApi'
import { AllocateModal, ClientFormModal, ContractModal, ReceiptModal } from './CorporateParts'
import { clientName } from './corporateHooks'
import '../results/results.css'

/** One client's account: balance, aging, contracts, invoices, receipts and the statement. */
export function ClientAccountPage() {
  const { t, i18n } = useTranslation()
  const { id } = useParams()
  const clientId = Number(id)
  const { can } = useSession()
  const toast = useToast()
  const [editing, setEditing] = useState(false)
  const [contract, setContract] = useState<Contract | 'new' | null>(null)
  const [receiptOpen, setReceiptOpen] = useState(false)
  const [allocating, setAllocating] = useState<Receipt | null>(null)
  const [statementOpen, setStatementOpen] = useState(false)
  const [checking, setChecking] = useState(false)

  const client = useQuery({ queryKey: corporateKeys.client(clientId), queryFn: () => api<ClientDetail>(`/api/corporate/clients/${clientId}`), enabled: Number.isInteger(clientId) })
  const invoices = useQuery({ queryKey: corporateKeys.invoices({ client: clientId }), queryFn: () => api<Paged<CorporateInvoice>>('/api/corporate/invoices', { query: { clientId, pageSize: 50 } }), enabled: client.isSuccess })
  const receipts = useQuery({ queryKey: corporateKeys.receipts({ client: clientId }), queryFn: () => api<Paged<Receipt>>('/api/corporate/receipts', { query: { clientId, pageSize: 50 } }), enabled: client.isSuccess })

  if (client.isLoading) return <div className="page"><Skeleton height={100} radius={12} /><div style={{ height: 16 }} /><Skeleton height={300} radius={12} /></div>
  const c = client.data
  if (!c) {
    return <div className="page"><Card><EmptyState icon={<Scale size={22} />} title={t('common.notFound')} text={client.error instanceof ApiError && client.error.status === 404 ? t('errors.not_found') : errorMessage(t, client.error)}
      action={<Link to="/corporate/clients" className="btn btn-secondary"><ArrowLeft size={16} className="flip-rtl" /> {t('corporate.backToClients')}</Link>} /></Card></div>
  }
  const a = c.account

  const check = async () => {
    setChecking(true)
    try {
      const r = await api<Reconcile>('/api/corporate/reconcile', { query: { clientId } })
      toast.show({ tone: r.ok ? 'success' : 'danger', title: r.ok ? t('corporate.booksAgree') : t('corporate.booksDiffer'), text: r.ok ? undefined : r.issues.slice(0, 3).join(' · ') })
    } catch (e) { toast.show({ tone: 'danger', title: t('corporate.actionFailed'), text: errorMessage(t, e) }) } finally { setChecking(false) }
  }

  return (
    <div className="page">
      <Link to="/corporate/clients" className="back-link"><ArrowLeft size={15} className="flip-rtl" aria-hidden /> {t('corporate.backToClients')}</Link>
      <div className="page-head">
        <div>
          <h2><span dir="auto">{clientName(c, i18n.language)}</span> <span className="num" style={{ color: 'var(--muted)', fontWeight: 500 }}>{c.code}</span> <Badge tone={c.isActive ? 'success' : 'neutral'} dot>{c.isActive ? t('common.active') : t('common.inactive')}</Badge></h2>
          <p>{t('corporate.days', { count: c.paymentTermsDays })}{c.contactName ? ` · ${c.contactName}` : ''}{c.phone ? ` · ${c.phone}` : ''}</p>
        </div>
        <span className="toolbar">
          {can('corporate.manage') && <Button variant="ghost" icon={<Pencil size={16} />} onClick={() => setEditing(true)}>{t('common.edit')}</Button>}
          <Button variant="ghost" icon={<FileText size={16} />} onClick={() => setStatementOpen(true)}>{t('corporate.statement')}</Button>
          <Button variant="ghost" icon={<Scale size={16} />} loading={checking} onClick={() => void check()}>{t('corporate.checkBooks')}</Button>
          {(can('corporate.receive') || can('corporate.adjust')) && <Button variant="primary" icon={<Wallet size={16} />} onClick={() => setReceiptOpen(true)}>{t('corporate.newReceipt')}</Button>}
        </span>
      </div>

      <div className="grid-stats" style={{ gridTemplateColumns: 'repeat(4, minmax(0, 1fr))', marginBottom: 16 }}>
        <StatCard label={t('corporate.balance')} value={formatMoney(a.balance)} icon={<Scale size={18} />} tone={a.balance > 0 ? 'warning' : 'success'} foot={t('corporate.balanceHint')} />
        <StatCard label={t('corporate.outstanding')} value={formatMoney(a.outstanding)} icon={<FileText size={18} />} />
        <StatCard label={t('corporate.unallocated')} value={formatMoney(a.unallocatedReceipts)} icon={<Wallet size={18} />} tone="success" />
        <StatCard label={t('corporate.creditLimit')} value={a.creditLimit === null ? '—' : formatMoney(a.creditLimit)} icon={<Scale size={18} />} foot={a.available === null ? t('corporate.noLimit') : t('corporate.available', { amount: formatMoney(a.available) })} />
      </div>

      <Card title={t('corporate.aging')} subtitle={t('corporate.agingHint')}>
        <dl className="drawer-figures" style={{ gridTemplateColumns: 'repeat(5, minmax(0, 1fr))' }}>
          {([['notDue', a.aging.notDue], ['days1To30', a.aging.days1To30], ['days31To60', a.aging.days31To60], ['days61To90', a.aging.days61To90], ['over90', a.aging.over90]] as const).map(([k, v]) => (
            <div key={k}><dt>{t(`corporate.bucket.${k}`)}</dt><dd className="num">{formatMoney(v)}</dd></div>
          ))}
        </dl>
      </Card>

      <div className="section-gap">
        <Card title={t('corporate.contracts')} actions={can('corporate.manage') ? <Button size="sm" icon={<Plus size={14} />} onClick={() => setContract('new')}>{t('corporate.newContract')}</Button> : undefined} flush>
          {c.contracts.length === 0 ? <p className="field-hint" style={{ padding: 16 }}>{t('corporate.noContracts')}</p> : (
            <ul className="ledger" style={{ padding: '0 var(--space-5)' }}>
              {c.contracts.map((k) => (
                <li key={k.id} style={{ gridTemplateColumns: 'minmax(0, 1fr) auto auto' }}>
                  <span><span className="cell-strong" dir="auto">{k.name}</span><span className="cell-sub">{k.validFrom} → {k.validTo ?? t('corporate.openEnded')} · {t('corporate.discountShort', { percent: k.discountPercent })} · {t('corporate.fixedPriceCount', { count: k.prices.length })}</span></span>
                  <Badge tone={k.isActive ? 'success' : 'neutral'} dot>{k.isActive ? t('common.active') : t('common.inactive')}</Badge>
                  {can('corporate.manage') && <Button size="sm" variant="ghost" iconOnly aria-label={t('common.edit')} icon={<Pencil size={14} />} onClick={() => setContract(k)} />}
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      <div className="grid-2-1 section-gap">
        <Card title={t('corporate.invoices')} flush>
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>{t('corporate.invoice')}</th><th>{t('corporate.due')}</th><th style={{ textAlign: 'end' }}>{t('orders.net')}</th><th style={{ textAlign: 'end' }}>{t('orders.balanceDue')}</th></tr></thead>
              <tbody>
                {invoices.data?.items.map((i) => (
                  <tr key={i.invoiceId}>
                    <td><Link to={`/orders/${i.orderId}`} className="num cell-strong">{i.invoiceNo}</Link><span className="cell-sub" dir="auto">{i18n.language === 'ur' && i.patientNameUr ? i.patientNameUr : i.patientName}</span></td>
                    <td>{i.dueDate ?? '—'} {i.bucket !== null && i.bucket > 0 && <Badge tone={bucketTone[i.bucket]}>{t(`corporate.bucketShort.${i.bucket}`)}</Badge>}</td>
                    <td className="num" style={{ textAlign: 'end' }}>{formatMoney(i.net)}</td>
                    <td className="num cell-strong" style={{ textAlign: 'end' }}>{formatMoney(i.balance)}</td>
                  </tr>
                ))}
                {invoices.data?.items.length === 0 && <tr><td colSpan={4} className="field-hint" style={{ padding: 16 }}>{t('corporate.noInvoices')}</td></tr>}
              </tbody>
            </table>
          </div>
        </Card>
        <Card title={t('corporate.receipts')} flush>
          {receipts.data?.items.length === 0 ? <p className="field-hint" style={{ padding: 16 }}>{t('corporate.noReceipts')}</p> : (
            <ul className="ledger" style={{ padding: '0 var(--space-4)' }}>
              {receipts.data?.items.map((r) => (
                <li key={r.id} style={{ gridTemplateColumns: 'minmax(0, 1fr) auto' }}>
                  <span><span className="num cell-strong">{r.receiptNo}</span> {r.kind === 2 && <Badge tone="info">{t('corporate.kindCreditNote')}</Badge>}<span className="cell-sub">{formatDateTime(r.receivedAtUtc, i18n.language)} · {formatMoney(r.amount)}</span></span>
                  {r.unallocated > 0 && (can('corporate.receive') || can('corporate.adjust')) ? <Button size="sm" onClick={() => setAllocating(r)}>{t('corporate.allocate')}</Button> : <Badge tone="success">{t('corporate.fullyAllocated')}</Badge>}
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      {editing && <ClientFormModal client={c} onClose={() => setEditing(false)} />}
      {contract && <ContractModal clientId={c.id} contract={contract === 'new' ? null : contract} onClose={() => setContract(null)} />}
      {receiptOpen && <ReceiptModal clientId={c.id} onClose={() => setReceiptOpen(false)} onCreated={(r) => { setReceiptOpen(false); setAllocating(r) }} />}
      {allocating && <AllocateModal receipt={allocating} onClose={() => setAllocating(null)} />}
      {statementOpen && <StatementModal clientId={c.id} onClose={() => setStatementOpen(false)} />}
    </div>
  )
}

function StatementModal({ clientId, onClose }: { clientId: number; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const today = new Date().toISOString().slice(0, 10)
  const [from, setFrom] = useState(`${today.slice(0, 8)}01`)
  const [to, setTo] = useState(today)
  const { data, isLoading, error } = useQuery({ queryKey: corporateKeys.statement(clientId, { from, to }), queryFn: () => api<Statement>(`/api/corporate/clients/${clientId}/statement`, { query: { from, to } }), enabled: from <= to })
  return (
    <Modal open title={t('corporate.statement')} subtitle={data ? <span dir="auto">{clientName(data.client, i18n.language)}</span> : undefined} onClose={onClose} size="xl"
      footer={<Button variant="ghost" icon={<Printer size={16} />} onClick={printOpenDialog} disabled={!data}>{t('corporate.printStatement')}</Button>}>
      <div className="form-stack">
        <div className="form-grid no-print">
          <label className="field"><span className="field-label">{t('samples.from')}</span><input className="input" type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} /></label>
          <label className="field"><span className="field-label">{t('samples.to')}</span><input className="input" type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} /></label>
        </div>
        {isLoading && <Skeleton height={160} radius={8} />}
        {error && <p className="text-warning">{errorMessage(t, error)}</p>}
        {data && (
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>{t('corporate.date')}</th><th>{t('corporate.reference')}</th><th style={{ textAlign: 'end' }}>{t('corporate.debit')}</th><th style={{ textAlign: 'end' }}>{t('corporate.credit')}</th><th style={{ textAlign: 'end' }}>{t('corporate.runningBalance')}</th></tr></thead>
              <tbody>
                <tr><td colSpan={4} className="cell-strong">{t('corporate.openingBalance')}</td><td className="num cell-strong" style={{ textAlign: 'end' }}>{formatMoney(data.openingBalance)}</td></tr>
                {data.lines.map((l, i) => (
                  <tr key={i}>
                    <td>{l.date}</td>
                    <td><span className="num cell-strong">{l.reference}</span><span className="cell-sub">{t(`corporate.lineType.${l.type}`)}{l.description ? ` · ${l.description}` : ''}</span></td>
                    <td className="num" style={{ textAlign: 'end' }}>{l.debit ? formatMoney(l.debit) : ''}</td>
                    <td className="num" style={{ textAlign: 'end' }}>{l.credit ? formatMoney(l.credit) : ''}</td>
                    <td className="num" style={{ textAlign: 'end' }}>{formatMoney(l.balance)}</td>
                  </tr>
                ))}
                <tr><td colSpan={4} className="cell-strong">{t('corporate.closingBalance')}</td><td className="num cell-strong" style={{ textAlign: 'end' }}>{formatMoney(data.closingBalance)}</td></tr>
              </tbody>
            </table>
          </div>
        )}
      </div>
    </Modal>
  )
}
