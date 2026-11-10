import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { AlertCircle, ArrowLeft, Banknote, ClipboardList, Printer, RotateCcw, XCircle } from 'lucide-react'
import { api, ApiError } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime, formatMoney } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { EmptyState } from '../../components/ui/EmptyState'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import { TextAreaField } from '../../components/ui/Controls'
import { useToast } from '../../components/ui/Toast'
import { localName } from '../catalog/catalogApi'
import { formatAge, patientName } from '../patients/patientsApi'
import { METHODS, invoiceTone, newKey, orderKeys, type InvoiceLineDto, type OrderDetail, type PaymentDto, type PaymentMethod } from './ordersApi'
import { OrderResultsCard } from '../results/OrderResultsCard'
import { OrderSamplesCard } from '../samples/OrderSamplesCard'
import { Receipt } from './Receipt'
import './orders.css'

export function OrderDetailPage() {
  const { t, i18n } = useTranslation()
  const { id } = useParams()
  const orderId = Number(id)
  const qc = useQueryClient()
  const toast = useToast()
  const { can } = useSession()
  const [pay, setPay] = useState(false)
  const [refund, setRefund] = useState(false)
  const [cancelling, setCancelling] = useState<InvoiceLineDto | 'order' | null>(null)
  const [receipt, setReceipt] = useState<PaymentDto | null>(null)

  const { data: o, isLoading, error } = useQuery({
    queryKey: orderKeys.detail(orderId),
    queryFn: () => api<OrderDetail>(`/api/orders/${orderId}`),
    enabled: Number.isInteger(orderId),
  })

  const refresh = async () => {
    await qc.invalidateQueries({ queryKey: ['orders'] })
    await qc.invalidateQueries({ queryKey: ['billing'] })
  }

  if (isLoading) return <div className="page"><Skeleton height={120} radius={12} /><div style={{ height: 16 }} /><Skeleton height={300} radius={12} /></div>
  if (!o) {
    return (
      <div className="page">
        <Card>
          <EmptyState icon={<ClipboardList size={22} />} title={t('common.notFound')} text={error instanceof ApiError && error.status === 404 ? t('errors.not_found') : t('errors.server_error')}
            action={<Link to="/orders" className="btn btn-secondary"><ArrowLeft size={16} className="flip-rtl" /> {t('orders.backToOrders')}</Link>} />
        </Card>
      </div>
    )
  }

  const inv = o.invoice
  const cancelled = o.status === 1

  return (
    <div className="page">
      <Link to="/orders" className="back-link no-print"><ArrowLeft size={15} className="flip-rtl" aria-hidden /> {t('orders.backToOrders')}</Link>

      <section className="card order-hero no-print">
        <div style={{ flex: 1, minWidth: 0 }}>
          <div className="patient-hero-top">
            <h2 className="num">{o.orderNo}</h2>
            <Badge tone={invoiceTone[inv.status]} dot>{t(`invoiceStatus.${inv.status}`)}</Badge>
            {o.priority === 1 && <Badge tone="danger">{t('orders.urgent')}</Badge>}
            {o.corporateClientId && <Badge tone="info">{t('orders.creditOrder', { client: o.corporateClientName })}</Badge>}
            {cancelled && <Badge>{t('orders.cancelled')}</Badge>}
          </div>
          <dl className="test-facts" style={{ gridTemplateColumns: 'repeat(4, minmax(0, auto))' }}>
            <div><dt>{t('orders.patientCol')}</dt><dd><Link to={`/patients/${o.patient.id}`} dir="auto">{patientName(o.patient, i18n.language)}</Link> <span className="num" style={{ color: 'var(--muted)' }}>{o.patient.patientNo}</span></dd></div>
            <div><dt>{t('patients.ageSex')}</dt><dd>{formatAge(t, o.patient.dateOfBirth)} · {t(`sex.short${o.patient.sex}`)}</dd></div>
            <div><dt>{t('orders.doctorCol')}</dt><dd dir="auto">{o.referringDoctorName ?? t('orders.selfReferred')}</dd></div>
            <div><dt>{t('orders.registeredAt')}</dt><dd>{formatDateTime(o.createdAtUtc, i18n.language)}</dd></div>
            <div><dt>{t('orders.branch')}</dt><dd>{localName({ nameEn: o.branchName, nameUr: o.branchNameUr }, i18n.language)}</dd></div>
            {o.createdByName && <div><dt>{t('orders.receivedBy')}</dt><dd>{o.createdByName}</dd></div>}
          </dl>
          {o.clinicalNotes && <p className="field-hint" style={{ marginTop: 10 }} dir="auto">{o.clinicalNotes}</p>}
          {cancelled && o.cancelReason && <p className="text-warning" style={{ marginTop: 8 }}>{t('orders.cancelledBecause', { reason: o.cancelReason })}</p>}
        </div>
        <div className="patient-hero-actions">
          {!cancelled && can('orders.cancel') && <Button variant="ghost" icon={<XCircle size={16} />} onClick={() => setCancelling('order')}>{t('orders.cancelOrder')}</Button>}
        </div>
      </section>

      <div className="grid-2-1 section-gap no-print">
        <Card title={t('orders.invoice')} subtitle={<span className="num">{inv.invoiceNo}</span>} flush>
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>{t('orders.item')}</th><th>{t('orders.status')}</th><th style={{ textAlign: 'end' }}>{t('orders.price')}</th><th /></tr></thead>
              <tbody>
                {inv.lines.map((l) => {
                  const item = o.items.find((i) => i.invoiceLineId === l.id)
                  return (
                    <tr key={l.id} className={l.isCancelled ? 'is-retired' : undefined}>
                      <td>
                        <span className={`cell-strong${l.isCancelled ? ' struck' : ''}`} dir="auto">{localName(l, i18n.language)}</span>
                        <span className="cell-sub num">{l.code}{l.kind === 2 && ` · ${t('orders.includes', { codes: l.testCodes.join(', ') })}`}</span>
                      </td>
                      <td>{l.isCancelled ? <Badge>{t('orders.cancelled')}</Badge> : item ? <Badge tone="primary" dot>{t(`itemStatus.${item.status}`)}</Badge> : null}{l.cancelReason && <span className="cell-sub">{l.cancelReason}</span>}</td>
                      <td className={`num${l.isCancelled ? ' struck' : ''}`} style={{ textAlign: 'end' }}>{formatMoney(l.price)}</td>
                      <td style={{ textAlign: 'end' }}>{l.canCancel && can('orders.cancel') && !cancelled && <Button size="sm" variant="ghost" onClick={() => setCancelling(l)}>{t('orders.cancelTest')}</Button>}</td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
          <dl className="bill-lines invoice-totals">
            <div><dt>{t('orders.gross')}</dt><dd className="num">{formatMoney(inv.gross)}</dd></div>
            {inv.discountAmount > 0 && <div><dt>{t('orders.discount')}{inv.discountPercent ? ` (${inv.discountPercent}%)` : ''}{inv.discountReason && <span className="cell-sub">{inv.discountReason}</span>}</dt><dd className="num">− {formatMoney(inv.discountAmount)}</dd></div>}
            <div className="bill-net"><dt>{t('orders.net')}</dt><dd className="num">{formatMoney(inv.net)}</dd></div>
            <div><dt>{t('orders.paid')}</dt><dd className="num">{formatMoney(inv.paid)}</dd></div>
            {inv.refunded > 0 && <div><dt>{t('orders.refunded')}</dt><dd className="num">− {formatMoney(inv.refunded)}</dd></div>}
            <div className={`bill-balance${inv.balance !== 0 ? ' is-open' : ''}`}><dt>{inv.balance < 0 ? t('orders.refundDue') : t('orders.balanceDue')}</dt><dd className="num">{formatMoney(Math.abs(inv.balance))}</dd></div>
          </dl>
        </Card>

        <Card
          title={t('orders.payments')}
          actions={
            <span className="toolbar">
              {inv.balance > 0 && can('payments.collect') && !o.corporateClientId && <Button size="sm" variant="primary" icon={<Banknote size={14} />} onClick={() => setPay(true)}>{t('orders.collectPayment')}</Button>}
              {inv.balance < 0 && can('billing.refund') && <Button size="sm" variant="primary" icon={<RotateCcw size={14} />} onClick={() => setRefund(true)}>{t('orders.refund')}</Button>}
            </span>
          }
        >
          {inv.payments.length === 0 && inv.refunds.length === 0 ? (
            <p className="field-hint">{t('orders.noPayments')}</p>
          ) : (
            <ul className="ledger">
              {inv.payments.map((p) => (
                <li key={`p${p.id}`}>
                  <span>
                    <span className="cell-strong num">{p.receiptNo}</span>
                    <span className="cell-sub">{t(`paymentMethod.${p.method}`)}{p.reference ? ` · ${p.reference}` : ''} · {formatDateTime(p.receivedAtUtc, i18n.language)}</span>
                  </span>
                  <span className="num cell-strong">{formatMoney(p.amount)}</span>
                  <button type="button" className="icon-btn" aria-label={`${t('orders.printReceipt')} ${p.receiptNo}`} title={t('orders.printReceipt')} onClick={() => setReceipt(p)}><Printer size={16} /></button>
                </li>
              ))}
              {inv.refunds.map((r) => (
                <li key={`r${r.id}`} className="is-refund">
                  <span>
                    <span className="cell-strong num">{r.refundNo}</span>
                    <span className="cell-sub">{t('orders.refunds')} · {t(`paymentMethod.${r.method}`)} · {r.reason}</span>
                  </span>
                  <span className="num cell-strong">− {formatMoney(r.amount)}</span>
                  <span />
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      {can('samples.view') && <div className="section-gap no-print"><OrderSamplesCard orderId={o.id} cancelled={cancelled} /></div>}
      {can('results.view') && <div className="section-gap no-print"><OrderResultsCard orderId={o.id} /></div>}

      {receipt && <Receipt order={o} payment={receipt} onDone={() => setReceipt(null)} />}
      {pay && <PaymentModal order={o} onClose={() => setPay(false)} onDone={async (p) => { setPay(false); toast.show({ tone: 'success', title: t('orders.paymentDone') }); await refresh(); if (p) setReceipt(p) }} />}
      {refund && <RefundModal order={o} onClose={() => setRefund(false)} onDone={async () => { setRefund(false); toast.show({ tone: 'success', title: t('orders.refundDone') }); await refresh() }} />}
      {cancelling && (
        <CancelModal
          order={o}
          target={cancelling}
          onClose={() => setCancelling(null)}
          onDone={async () => { setCancelling(null); await refresh() }}
        />
      )}
    </div>
  )
}

/* ---------------- Payment ---------------- */
function PaymentModal({ order, onClose, onDone }: { order: OrderDetail; onClose: () => void; onDone: (p?: PaymentDto) => void }) {
  const { t } = useTranslation()
  const key = useState(newKey)[0]
  const inv = order.invoice
  const [method, setMethod] = useState<PaymentMethod>(1)
  const [amount, setAmount] = useState(String(inv.balance))
  const [reference, setReference] = useState('')
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const submit = async () => {
    const value = Number(amount)
    if (!(value > 0) || value > inv.balance) return setFailure(t('errors.payment_exceeds_balance'))
    setSaving(true)
    setFailure(null)
    try {
      const before = new Set(inv.payments.map((p) => p.id))
      const res = await api<OrderDetail>(`/api/billing/invoices/${inv.id}/payments`, { method: 'POST', body: { idempotencyKey: key, method, amount: value, reference: reference.trim() || null } })
      onDone(res.invoice.payments.find((p) => !before.has(p.id)))
    } catch (e) {
      setFailure(errorMessage(t, e))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal open size="sm" onClose={onClose} title={t('orders.paymentTitle')} subtitle={`${order.orderNo} · ${t('orders.balanceDue')}: ${formatMoney(inv.balance)}`}
      footer={<><Button onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={saving} onClick={() => void submit()}>{t('orders.collectPayment')}</Button></>}>
      {failure && <div className="banner tone-danger" role="alert" style={{ marginBottom: 14 }}><AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} /><span>{failure}</span></div>}
      <form className="form-stack" onSubmit={(e) => { e.preventDefault(); void submit() }}>
        <div className="method-grid" role="radiogroup" aria-label={t('orders.method')}>
          {METHODS.map((m) => <button key={m} type="button" role="radio" aria-checked={method === m} className={`method${method === m ? ' is-on' : ''}`} onClick={() => setMethod(m)}>{t(`paymentMethod.${m}`)}</button>)}
        </div>
        <Field label={t('orders.amount')} type="number" min={0} step="0.01" dir="ltr" autoFocus value={amount} onChange={(e) => setAmount(e.target.value)} />
        {method !== 1 && <Field label={t('orders.reference')} hint={t('orders.referenceHint')} dir="ltr" value={reference} maxLength={100} onChange={(e) => setReference(e.target.value)} />}
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}

/* ---------------- Refund ---------------- */
function RefundModal({ order, onClose, onDone }: { order: OrderDetail; onClose: () => void; onDone: () => void }) {
  const { t } = useTranslation()
  const key = useState(newKey)[0]
  const due = -order.invoice.balance
  const [method, setMethod] = useState<PaymentMethod>(order.invoice.payments.at(-1)?.method ?? 1)
  const [amount, setAmount] = useState(String(due))
  const [reason, setReason] = useState('')
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const submit = async () => {
    const value = Number(amount)
    if (!(value > 0) || value > due) return setFailure(t('errors.refund_exceeds_due'))
    if (!reason.trim()) return setFailure(t('errors.refund_reason_required'))
    setSaving(true)
    setFailure(null)
    try {
      await api(`/api/billing/invoices/${order.invoice.id}/refunds`, { method: 'POST', body: { idempotencyKey: key, method, amount: value, reason: reason.trim() } })
      onDone()
    } catch (e) {
      setFailure(errorMessage(t, e))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal open size="sm" onClose={onClose} title={t('orders.refundTitle')} subtitle={`${order.orderNo} · ${t('orders.refundDue')}: ${formatMoney(due)}`}
      footer={<><Button onClick={onClose}>{t('common.cancel')}</Button><Button variant="danger" loading={saving} onClick={() => void submit()}>{t('orders.refund')}</Button></>}>
      {failure && <div className="banner tone-danger" role="alert" style={{ marginBottom: 14 }}><AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} /><span>{failure}</span></div>}
      <form className="form-stack" onSubmit={(e) => { e.preventDefault(); void submit() }}>
        <div className="method-grid" role="radiogroup" aria-label={t('orders.method')}>
          {METHODS.map((m) => <button key={m} type="button" role="radio" aria-checked={method === m} className={`method${method === m ? ' is-on' : ''}`} onClick={() => setMethod(m)}>{t(`paymentMethod.${m}`)}</button>)}
        </div>
        <Field label={t('orders.amount')} hint={t('orders.refundHint')} type="number" min={0} step="0.01" dir="ltr" autoFocus value={amount} onChange={(e) => setAmount(e.target.value)} />
        <TextAreaField label={t('orders.refundReason')} rows={2} value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}

/* ---------------- Cancel ---------------- */
function CancelModal({ order, target, onClose, onDone }: { order: OrderDetail; target: InvoiceLineDto | 'order'; onClose: () => void; onDone: () => void }) {
  const { t, i18n } = useTranslation()
  const [reason, setReason] = useState('')
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const whole = target === 'order'

  const submit = async () => {
    if (!reason.trim()) return setFailure(t('errors.cancel_reason_required'))
    setSaving(true)
    setFailure(null)
    try {
      await api(whole ? `/api/orders/${order.id}/cancel` : `/api/orders/${order.id}/lines/${target.id}/cancel`, { method: 'POST', body: { reason: reason.trim() } })
      onDone()
    } catch (e) {
      setFailure(errorMessage(t, e))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal open size="sm" onClose={onClose} title={whole ? t('orders.cancelOrderTitle') : t('orders.cancelTestTitle', { name: localName(target, i18n.language) })} subtitle={t('orders.cancelText')}
      footer={<><Button onClick={onClose}>{t('common.cancel')}</Button><Button variant="danger" loading={saving} onClick={() => void submit()}>{whole ? t('orders.cancelOrder') : t('orders.cancelTest')}</Button></>}>
      {failure && <div className="banner tone-danger" role="alert" style={{ marginBottom: 14 }}><AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} /><span>{failure}</span></div>}
      <TextAreaField label={t('orders.cancelReason')} rows={3} autoFocus value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
    </Modal>
  )
}
