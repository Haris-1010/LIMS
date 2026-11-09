import { useEffect, useMemo, useRef, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { AlertCircle, Package, Percent, Search, UserPlus, X } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatMoney } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Switch, TextAreaField } from '../../components/ui/Controls'
import { EmptyState } from '../../components/ui/EmptyState'
import { Field } from '../../components/ui/Field'
import { useToast } from '../../components/ui/Toast'
import type { Paged } from '../admin/adminApi'
import { localName, type PackageDto, type TestListItem } from '../catalog/catalogApi'
import { formatAge, patientName, type PatientDetail, type PatientListItem } from '../patients/patientsApi'
import { PatientFormModal } from '../patients/PatientFormModal'
import { METHODS, newKey, orderKeys, type DrawerDto, type OrderDetail, type PaymentMethod, type Quote } from './ordersApi'
import './orders.css'

interface Item { kind: 'test' | 'package'; id: number; code: string; nameEn: string; nameUr: string | null; price: number }
interface DoctorOption { id: number; code: string; fullName: string; fullNameUr: string | null; hospital: string | null }

export function NewOrderPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const toast = useToast()
  const { user, can } = useSession()
  const [params] = useSearchParams()

  // One submission key per open form: a double click or a retry returns the same order.
  const submissionKey = useRef(newKey())

  const [patient, setPatient] = useState<PatientListItem | PatientDetail | null>(null)
  const [registering, setRegistering] = useState(false)
  const [doctorId, setDoctorId] = useState('')
  const [priority, setPriority] = useState<0 | 1>(0)
  const [notes, setNotes] = useState('')
  const [items, setItems] = useState<Item[]>([])
  const [discOn, setDiscOn] = useState(false)
  const [discMode, setDiscMode] = useState<'percent' | 'amount'>('percent')
  const [discValue, setDiscValue] = useState('')
  const [discReason, setDiscReason] = useState('')
  const [payNow, setPayNow] = useState(true)
  const [method, setMethod] = useState<PaymentMethod>(1)
  const [amount, setAmount] = useState('')
  const [reference, setReference] = useState('')
  const [tendered, setTendered] = useState('')
  const [failure, setFailure] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const branchSelected = user?.activeBranchId != null

  // Patient preselected from the patient profile ("New order").
  const patientParam = params.get('patient')
  useEffect(() => {
    if (!patientParam) return
    api<PatientDetail>(`/api/patients/${patientParam}`).then(setPatient).catch(() => undefined)
  }, [patientParam])

  const doctors = useQuery({ queryKey: ['doctors', 'options'], queryFn: () => api<DoctorOption[]>('/api/doctors/options'), staleTime: 60_000 })
  const drawer = useQuery({ queryKey: orderKeys.drawer, queryFn: () => api<{ drawer: DrawerDto | null }>('/api/billing/drawer'), enabled: can('payments.collect') && branchSelected })

  // Server-side pricing: the form only shows what the server returns.
  const discountBody = discOn && Number(discValue) > 0
    ? { percent: discMode === 'percent' ? Number(discValue) : null, amount: discMode === 'amount' ? Number(discValue) : null, reason: discReason || null }
    : null
  const itemsBody = items.map((i) => (i.kind === 'test' ? { testId: i.id } : { packageId: i.id }))
  const quote = useQuery({
    queryKey: ['orders', 'quote', itemsBody, discountBody?.percent, discountBody?.amount],
    queryFn: () => api<Quote>('/api/orders/quote', { method: 'POST', body: { items: itemsBody, discount: discountBody } }),
    enabled: items.length > 0 && branchSelected,
    placeholderData: (prev) => prev,
    retry: false,
  })
  const net = quote.data?.net ?? 0
  useEffect(() => setAmount(net > 0 ? String(net) : ''), [net])

  const cashNeedsDrawer = payNow && method === 1 && !drawer.data?.drawer
  const quoteError = quote.error ? errorMessage(t, quote.error) : null
  const payAmount = Number(amount)
  const change = method === 1 && Number(tendered) > payAmount ? Number(tendered) - payAmount : 0

  const addItem = (item: Item) => {
    if (items.some((i) => i.kind === item.kind && i.id === item.id)) return
    setItems((l) => [...l, item])
  }

  const submit = async () => {
    setFailure(null)
    if (!patient) return setFailure(t('errors.patient_invalid'))
    if (items.length === 0) return setFailure(t('errors.items_required'))
    if (discountBody && !discReason.trim()) return setFailure(t('errors.discount_reason_required'))
    if (payNow && (!(payAmount > 0) || payAmount > net)) return setFailure(t('errors.payment_exceeds_balance'))
    setSubmitting(true)
    try {
      const order = await api<OrderDetail>('/api/orders', {
        method: 'POST',
        body: {
          idempotencyKey: submissionKey.current,
          patientId: patient.id,
          referringDoctorId: doctorId ? Number(doctorId) : null,
          priority,
          clinicalNotes: notes.trim() || null,
          items: itemsBody,
          discount: discountBody,
          payment: payNow && payAmount > 0 ? { method, amount: payAmount, reference: reference.trim() || null } : null,
        },
      })
      toast.show({ tone: 'success', title: t('orders.created', { no: order.orderNo }) })
      await qc.invalidateQueries({ queryKey: ['orders'] })
      await qc.invalidateQueries({ queryKey: ['billing'] })
      navigate(`/orders/${order.id}`)
    } catch (e) {
      setFailure(errorMessage(t, e))
    } finally {
      setSubmitting(false)
    }
  }

  if (!branchSelected) {
    return (
      <div className="page">
        <Card><EmptyState icon={<AlertCircle size={22} />} title={t('orders.newTitle')} text={t('orders.branchRequired')} /></Card>
      </div>
    )
  }

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <h2>{t('orders.newTitle')}</h2>
          <p>{t('orders.newSubtitle')}</p>
        </div>
      </div>

      {failure && (
        <div className="banner tone-danger" role="alert" style={{ marginBottom: 16 }}>
          <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
          <span>{failure}</span>
        </div>
      )}

      <div className="order-layout">
        <div className="stack">
          <Card title={t('orders.patient')}>
            {patient ? (
              <div className="picked-patient">
                <div className="patient-avatar" style={{ width: 44, height: 44, fontSize: '1.1rem' }} aria-hidden>{patient.fullName.charAt(0).toUpperCase()}</div>
                <div style={{ flex: 1 }}>
                  <strong dir="auto">{patientName(patient, i18n.language)}</strong>
                  <span className="cell-sub"><span className="num">{patient.patientNo}</span> · {formatAge(t, patient.dateOfBirth)} · {t(`sex.${patient.sex}`)}{patient.mobile ? ` · ${patient.mobile}` : ''}</span>
                </div>
                <Button size="sm" variant="ghost" onClick={() => setPatient(null)}>{t('orders.changePatient')}</Button>
              </div>
            ) : (
              <PatientPicker onPick={setPatient} onRegister={() => setRegistering(true)} />
            )}
            <div className="form-grid" style={{ marginTop: 16 }}>
              <div className="field">
                <label className="field-label" htmlFor="doctor">{t('orders.doctor')}</label>
                <select id="doctor" className="select" value={doctorId} onChange={(e) => setDoctorId(e.target.value)}>
                  <option value="">{t('orders.selfReferred')}</option>
                  {doctors.data?.map((d) => <option key={d.id} value={d.id}>{localName({ nameEn: d.fullName, nameUr: d.fullNameUr }, i18n.language)}{d.hospital ? ` — ${d.hospital}` : ''}</option>)}
                </select>
              </div>
              <div className="field">
                <span className="field-label">{t('orders.priority')}</span>
                <div className="segmented" role="radiogroup" aria-label={t('orders.priority')}>
                  {([0, 1] as const).map((p) => (
                    <button key={p} type="button" role="radio" aria-checked={priority === p} aria-pressed={priority === p} onClick={() => setPriority(p)}>{p === 0 ? t('orders.routine') : t('orders.urgent')}</button>
                  ))}
                </div>
              </div>
              <div className="span-2"><TextAreaField label={t('orders.notes')} rows={2} value={notes} maxLength={1000} onChange={(e) => setNotes(e.target.value)} /></div>
            </div>
          </Card>

          <Card title={t('orders.testsTitle')}>
            <TestPicker onAdd={addItem} />
            {items.length === 0 ? (
              <EmptyState icon={<Package size={22} />} title={t('orders.noTests')} text={t('orders.noTestsText')} />
            ) : (
              <ul className="cart">
                {items.map((i) => (
                  <li key={`${i.kind}-${i.id}`}>
                    <span>
                      <span className="cell-strong" dir="auto">{localName(i, i18n.language)}</span>
                      <span className="cell-sub num">{i.code}{i.kind === 'package' && <> · <Badge tone="accent">{t('orders.package')}</Badge></>}</span>
                    </span>
                    <span className="num">{formatMoney(quote.data?.lines.find((l) => (l.testId ?? l.packageId) === i.id && (l.kind === 1) === (i.kind === 'test'))?.price ?? i.price)}</span>
                    <button type="button" className="icon-btn" aria-label={`${t('orders.remove')} ${i.code}`} onClick={() => setItems((l) => l.filter((x) => x !== i))}><X size={16} /></button>
                  </li>
                ))}
              </ul>
            )}
            {quoteError && <p className="field-error" role="alert" style={{ marginTop: 8 }}><AlertCircle size={13} aria-hidden /> {quoteError}</p>}
          </Card>
        </div>

        <aside className="stack bill-panel">
          <Card title={t('orders.bill')}>
            <dl className="bill-lines">
              <div><dt>{t('orders.gross')}</dt><dd className="num">{formatMoney(quote.data?.gross ?? 0)}</dd></div>
              {can('billing.discount') && (
                <div className="bill-discount">
                  <Switch checked={discOn} onChange={setDiscOn} label={t('orders.addDiscount')} />
                  {discOn && (
                    <div className="discount-fields">
                      <div className="segmented" role="radiogroup" aria-label={t('orders.discount')}>
                        <button type="button" role="radio" aria-checked={discMode === 'percent'} aria-pressed={discMode === 'percent'} onClick={() => setDiscMode('percent')}><Percent size={13} /> {t('orders.discountPercent')}</button>
                        <button type="button" role="radio" aria-checked={discMode === 'amount'} aria-pressed={discMode === 'amount'} onClick={() => setDiscMode('amount')}>{t('orders.discountAmount')}</button>
                      </div>
                      <Field label={t('orders.discountValue')} type="number" min={0} step="0.01" dir="ltr" value={discValue} onChange={(e) => setDiscValue(e.target.value)} />
                      <Field label={t('orders.discountReason')} value={discReason} maxLength={300} onChange={(e) => setDiscReason(e.target.value)} />
                    </div>
                  )}
                </div>
              )}
              {(quote.data?.discountAmount ?? 0) > 0 && <div><dt>{t('orders.discount')}</dt><dd className="num">− {formatMoney(quote.data!.discountAmount)}</dd></div>}
              <div className="bill-net"><dt>{t('orders.net')}</dt><dd className="num">{formatMoney(net)}</dd></div>
            </dl>
          </Card>

          {can('payments.collect') && (
            <Card>
              <Switch checked={payNow} onChange={setPayNow} label={t('orders.payNow')} />
              {payNow && (
                <div className="form-stack" style={{ marginTop: 14 }}>
                  <div className="method-grid" role="radiogroup" aria-label={t('orders.method')}>
                    {METHODS.map((m) => (
                      <button key={m} type="button" role="radio" aria-checked={method === m} className={`method${method === m ? ' is-on' : ''}`} onClick={() => setMethod(m)}>{t(`paymentMethod.${m}`)}</button>
                    ))}
                  </div>
                  <Field label={t('orders.amount')} type="number" min={0} step="0.01" dir="ltr" value={amount} onChange={(e) => setAmount(e.target.value)} />
                  {method === 1 ? (
                    <>
                      <Field label={t('orders.tendered')} type="number" min={0} dir="ltr" value={tendered} onChange={(e) => setTendered(e.target.value)} />
                      {change > 0 && <div className="change-due"><span>{t('orders.change')}</span><strong className="num">{formatMoney(change)}</strong></div>}
                    </>
                  ) : (
                    <Field label={t('orders.reference')} hint={t('orders.referenceHint')} dir="ltr" value={reference} maxLength={100} onChange={(e) => setReference(e.target.value)} />
                  )}
                  {cashNeedsDrawer && (
                    <div className="banner tone-warning">
                      <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
                      <span>{t('orders.drawerClosed')} <Link to="/billing/cash-drawer">{t('orders.openDrawer')}</Link></span>
                    </div>
                  )}
                </div>
              )}
            </Card>
          )}

          <Button variant="primary" size="lg" block loading={submitting} disabled={!patient || items.length === 0 || cashNeedsDrawer || !!quoteError} onClick={() => void submit()}>
            {submitting ? t('orders.submitting') : t('orders.submit')}
          </Button>
        </aside>
      </div>

      {registering && <PatientFormModal patient={null} onClose={() => setRegistering(false)} onSaved={(id) => api<PatientDetail>(`/api/patients/${id}`).then(setPatient)} />}
    </div>
  )
}

/* ---------------- Patient picker ---------------- */
function PatientPicker({ onPick, onRegister }: { onPick: (p: PatientListItem) => void; onRegister: () => void }) {
  const { t, i18n } = useTranslation()
  const { can } = useSession()
  const [text, setText] = useState('')
  const [debounced, setDebounced] = useState('')
  useEffect(() => {
    const h = window.setTimeout(() => setDebounced(text.trim()), 250)
    return () => window.clearTimeout(h)
  }, [text])
  const { data } = useQuery({
    queryKey: ['patients', 'picker', debounced],
    queryFn: () => api<Paged<PatientListItem>>('/api/patients', { query: { search: debounced, pageSize: 6 } }),
    enabled: debounced.length >= 2,
  })
  return (
    <div>
      <div className="input-wrap">
        <Search size={16} className="input-icon" aria-hidden />
        <input className="input has-icon" autoFocus value={text} placeholder={t('orders.searchPatient')} aria-label={t('orders.searchPatient')} onChange={(e) => setText(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter' && data?.items.length === 1) onPick(data.items[0]!) }} />
      </div>
      {debounced.length >= 2 && (
        <ul className="picker-list">
          {data?.items.map((p) => (
            <li key={p.id}>
              <button type="button" onClick={() => onPick(p)}>
                <span className="num cell-strong">{p.patientNo}</span>
                <span dir="auto">{patientName(p, i18n.language)}</span>
                <span className="cell-sub" style={{ display: 'inline' }}>{formatAge(t, p.dateOfBirth)} · {t(`sex.short${p.sex}`)}{p.mobile ? ` · ${p.mobile}` : ''}</span>
              </button>
            </li>
          ))}
          {data && data.items.length === 0 && <li className="picker-empty">{t('orders.noPatientMatch')}</li>}
        </ul>
      )}
      {can('patients.create') && <Button size="sm" variant="ghost" icon={<UserPlus size={14} />} onClick={onRegister} style={{ marginTop: 8 }}>{t('orders.registerNew')}</Button>}
    </div>
  )
}

/* ---------------- Test & package picker ---------------- */
function TestPicker({ onAdd }: { onAdd: (i: Item) => void }) {
  const { t, i18n } = useTranslation()
  const [text, setText] = useState('')
  const [debounced, setDebounced] = useState('')
  useEffect(() => {
    const h = window.setTimeout(() => setDebounced(text.trim()), 200)
    return () => window.clearTimeout(h)
  }, [text])
  const tests = useQuery({
    queryKey: ['catalog', 'order-picker', debounced],
    queryFn: () => api<Paged<TestListItem>>('/api/catalog/tests', { query: { search: debounced, isActive: true, pageSize: 8 } }),
    enabled: debounced.length >= 1,
  })
  const packages = useQuery({ queryKey: ['catalog', 'packages'], queryFn: () => api<PackageDto[]>('/api/catalog/packages'), staleTime: 60_000 })

  const results = useMemo<Item[]>(() => {
    if (!debounced) return []
    const q = debounced.toLowerCase()
    const pk = (packages.data ?? []).filter((p) => p.isActive && (p.code.toLowerCase().includes(q) || p.nameEn.toLowerCase().includes(q) || (p.nameUr ?? '').includes(debounced)))
      .map<Item>((p) => ({ kind: 'package', id: p.id, code: p.code, nameEn: p.nameEn, nameUr: p.nameUr, price: p.price }))
    const ts = (tests.data?.items ?? []).map<Item>((x) => ({ kind: 'test', id: x.id, code: x.code, nameEn: x.nameEn, nameUr: x.nameUr, price: x.price }))
    return [...ts, ...pk]
  }, [debounced, tests.data, packages.data])

  const pick = (i: Item) => { onAdd(i); setText(''); setDebounced('') }

  return (
    <div style={{ marginBottom: 12 }}>
      <div className="input-wrap">
        <Search size={16} className="input-icon" aria-hidden />
        <input
          className="input has-icon"
          value={text}
          placeholder={t('orders.searchTests')}
          aria-label={t('orders.searchTests')}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={(e) => {
            if (e.key !== 'Enter' || !text.trim()) return
            e.preventDefault()
            // Enter adds an exact code match, or the only result.
            const exact = results.find((r) => r.code.toLowerCase() === text.trim().toLowerCase()) ?? (results.length === 1 ? results[0] : undefined)
            if (exact) pick(exact)
          }}
        />
      </div>
      {results.length > 0 && (
        <ul className="picker-list">
          {results.map((r) => (
            <li key={`${r.kind}-${r.id}`}>
              <button type="button" onClick={() => pick(r)}>
                <span className="num cell-strong">{r.code}</span>
                <span dir="auto">{localName(r, i18n.language)} {r.kind === 'package' && <Badge tone="accent">{t('orders.package')}</Badge>}</span>
                <span className="num">{formatMoney(r.price)}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
