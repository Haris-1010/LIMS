import { Fragment, useEffect, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { AlertTriangle, ArrowLeft, ArrowRight, CheckCircle2, FlaskConical, Pencil, Phone, RotateCcw, Save, Send, ShieldCheck, Trash2 } from 'lucide-react'
import { api, ApiError } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { TextAreaField } from '../../components/ui/Controls'
import { EmptyState } from '../../components/ui/EmptyState'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import { formatAge, patientName } from '../patients/patientsApi'
import { FlagBadge, NotifyDialog, ReasonDialog, ResultStatusBadge } from './ResultParts'
import { useRefreshResults } from './resultHooks'
import { OrderTestsBar } from './OrderTestsBar'
import { nextTest, useOrderTests } from './orderTests'
import { parseNumber, previewFlag, rangeText, resultKeys, type AlertDto, type EntryModel, type ParameterEntry, type ResultFlag } from './resultsApi'
import './results.css'

interface Cell { value: string; optionId: number | null }

const pickName = (en: string, ur: string | null, lang: string) => (lang === 'ur' && ur ? ur : en)

/** Initial cells from what the server has saved for this version. */
function cellsFrom(m: EntryModel): Record<number, Cell> {
  const cells: Record<number, Cell> = {}
  for (const v of m.values) cells[v.parameterId] = { value: v.valueNumeric !== null ? String(v.valueNumeric) : v.optionId === null ? v.valueText ?? '' : '', optionId: v.optionId }
  return cells
}

/**
 * One screen for a single test's result, at every stage: entry (draft, submit), review (verify, return, approve) and a released
 * result (read-only, amend). What each person may do comes from the server's `actions`; this page never decides permissions itself.
 */
export function ResultEntryPage() {
  const { t, i18n } = useTranslation()
  const { orderItemId } = useParams()
  const itemId = Number(orderItemId)
  const navigate = useNavigate()
  const qc = useQueryClient()
  const toast = useToast()
  const { can } = useSession()
  const refresh = useRefreshResults()
  const tableRef = useRef<HTMLTableElement>(null)

  const { data: m, isLoading, error } = useQuery({ queryKey: resultKeys.entry(itemId), queryFn: () => api<EntryModel>(`/api/results/items/${itemId}`), enabled: Number.isInteger(itemId), retry: false })

  const [cells, setCells] = useState<Record<number, Cell>>({})
  const [comment, setComment] = useState('')
  const [dirty, setDirty] = useState(false)
  const [dialog, setDialog] = useState<'return' | 'amend' | 'discard' | null>(null)
  const [notifying, setNotifying] = useState<AlertDto | null>(null)
  /** Where the user wanted to go while values were not saved (asks first). */
  const [leaveTo, setLeaveTo] = useState<string | null>(null)
  const orderTests = useOrderTests(m?.orderId)

  // Reset the form to what the server has whenever the saved version or its state changes.
  const stamp = m ? `${m.current?.id}:${m.current?.rowVersion}:${m.current?.status}` : ''
  useEffect(() => {
    if (!m) return
    setCells(cellsFrom(m))
    setComment(m.current?.comment ?? '')
    setDirty(false)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stamp])

  useEffect(() => {
    if (!dirty) return
    const warn = (e: BeforeUnloadEvent) => { e.preventDefault() }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [dirty])

  const params = useMemo(() => m?.parameters ?? [], [m])
  const editable = !!m?.actions.save
  const invalid = useMemo(() => new Set(params.filter((p) => p.resultType === 1 && (cells[p.parameterId]?.value ?? '').trim() !== '' && parseNumber(cells[p.parameterId]!.value) === null).map((p) => p.parameterId)), [params, cells])

  const done = async (e: EntryModel | null, ok: string) => {
    if (e) qc.setQueryData(resultKeys.entry(itemId), e)
    toast.show({ tone: 'success', title: ok })
    await refresh()
  }
  const failed = (e: unknown) => toast.show({ tone: 'danger', title: t('results.actionFailed'), text: errorMessage(t, e) })

  const body = () => ({
    rowVersion: m!.current?.status === 0 ? m!.current.rowVersion : null,
    comment: comment.trim() || null,
    values: params.map((p) => ({ parameterId: p.parameterId, value: p.resultType === 2 ? null : cells[p.parameterId]?.value ?? '', optionId: p.resultType === 2 ? cells[p.parameterId]?.optionId ?? null : null })),
  })
  const saveCall = () => api<EntryModel>(`/api/results/items/${itemId}/draft`, { method: 'PUT', body: body() })

  const save = useMutation({ mutationFn: saveCall, onSuccess: (e) => done(e, t('results.draftSaved')), onError: failed })
  const submit = useMutation({
    mutationFn: async () => {
      const e = dirty || !m!.current ? await saveCall() : m!
      return api<EntryModel>(`/api/results/${e.current!.id}/submit`, { method: 'POST', body: { rowVersion: e.current!.rowVersion } })
    },
    onSuccess: (e) => done(e, t('results.submitted')),
    onError: async (e) => {
      failed(e)
      // A stale version (someone else changed it) is reloaded so the screen matches the server.
      if (e instanceof ApiError && e.status === 409) await qc.invalidateQueries({ queryKey: resultKeys.entry(itemId) })
    },
  })
  const step = (path: 'verify' | 'approve', ok: string) => api<EntryModel>(`/api/results/${m!.current!.id}/${path}`, { method: 'POST', body: { rowVersion: m!.current!.rowVersion } }).then((e) => done(e, ok))
  const verify = useMutation({ mutationFn: () => step('verify', t('results.verified')), onError: async (e) => { failed(e); await qc.invalidateQueries({ queryKey: resultKeys.entry(itemId) }) } })
  const approve = useMutation({ mutationFn: () => step('approve', t('results.approved')), onError: async (e) => { failed(e); await qc.invalidateQueries({ queryKey: resultKeys.entry(itemId) }) } })
  const returnIt = useMutation({
    mutationFn: (reason: string) => api<EntryModel>(`/api/results/${m!.current!.id}/return`, { method: 'POST', body: { reason, rowVersion: m!.current!.rowVersion } }),
    onSuccess: (e) => { setDialog(null); return done(e, t('results.returned')) }, onError: failed,
  })
  const amend = useMutation({
    mutationFn: (reason: string) => api<EntryModel>(`/api/results/${m!.current!.id}/amend`, { method: 'POST', body: { reason, rowVersion: m!.current!.rowVersion } }),
    onSuccess: (e) => { setDialog(null); return done(e, t('results.amendStarted')) }, onError: failed,
  })
  const discard = useMutation({
    mutationFn: () => api(`/api/results/items/${itemId}/draft`, { method: 'DELETE' }),
    onSuccess: async () => { setDialog(null); toast.show({ tone: 'success', title: t('results.discarded') }); await refresh(); navigate('/results/entry') },
    onError: (e) => { setDialog(null); failed(e) },
  })

  /** Move to another test of the order (or its report); unsaved values are never dropped without asking. */
  const go = (path: string) => { if (dirty) setLeaveTo(path); else navigate(path) }

  const setCell = (id: number, patch: Partial<Cell>) => { setCells((c) => ({ ...c, [id]: { ...{ value: '', optionId: null }, ...c[id], ...patch } })); setDirty(true) }

  /** Enter moves to the next field, like a form on paper; after the last one it lands on Save. */
  const onEnter = (e: React.KeyboardEvent) => {
    if (e.key !== 'Enter' || (e.target as HTMLElement).tagName === 'TEXTAREA') return
    e.preventDefault()
    const fields = [...(tableRef.current?.querySelectorAll<HTMLElement>('[data-entry]') ?? [])]
    const i = fields.indexOf(e.target as HTMLElement)
    if (i >= 0 && i < fields.length - 1) fields[i + 1]!.focus()
    else document.getElementById('entry-save')?.focus()
  }

  if (isLoading) return <div className="page"><Skeleton height={120} radius={12} /><div style={{ height: 16 }} /><Skeleton height={320} radius={12} /></div>
  if (!m) {
    return (
      <div className="page"><Card><EmptyState icon={<FlaskConical size={22} />} title={t('common.notFound')} text={error instanceof ApiError && error.status === 404 ? t('errors.not_found') : errorMessage(t, error)}
        action={<Link to="/results/entry" className="btn btn-secondary"><ArrowLeft size={16} className="flip-rtl" /> {t('results.backToQueue')}</Link>} /></Card></div>
    )
  }

  const a = m.actions
  const cur = m.current
  const amending = !!m.released && !!cur && cur.status !== 30
  const next = nextTest(orderTests, itemId)
  const flagOf = (p: ParameterEntry): { flag: ResultFlag; preview: boolean } => {
    const saved = m.values.find((v) => v.parameterId === p.parameterId)
    if (!dirty && saved) return { flag: saved.flag, preview: false }
    const cell = cells[p.parameterId]
    if (p.resultType === 1 && cell?.value.trim()) { const n = parseNumber(cell.value); return { flag: n === null ? 0 : previewFlag(n, p.range), preview: true } }
    if (p.resultType === 2 && cell?.optionId) { const o = p.options.find((x) => x.id === cell.optionId); return { flag: o ? (o.isAbnormal ? 6 : p.options.some((x) => x.isAbnormal) ? 1 : 0) : 0, preview: true } }
    return { flag: 0, preview: false }
  }
  const noRange = params.filter((p) => p.resultType === 1 && !p.range)
  const groups: { name: string | null; items: ParameterEntry[] }[] = []
  for (const p of params) { const last = groups[groups.length - 1]; if (last && last.name === p.groupName) last.items.push(p); else groups.push({ name: p.groupName, items: [p] }) }
  const busy = save.isPending || submit.isPending || verify.isPending || approve.isPending

  const renderInput = (p: ParameterEntry) => {
    const c = cells[p.parameterId] ?? { value: '', optionId: null }
    const label = pickName(p.nameEn, p.nameUr, i18n.language)
    if (!editable) {
      const v = m.values.find((x) => x.parameterId === p.parameterId)
      return <span className="entry-value" dir="auto">{v ? (v.display ?? '—') : <span className="field-hint">{t('results.noValue')}</span>}</span>
    }
    if (p.resultType === 2) {
      return (
        <select className="select entry-input" data-entry aria-label={label} value={c.optionId ?? ''} onKeyDown={onEnter} onChange={(e) => setCell(p.parameterId, { optionId: e.target.value ? Number(e.target.value) : null })}>
          <option value="">—</option>
          {p.options.map((o) => <option key={o.id} value={o.id}>{pickName(o.valueEn, o.valueUr, i18n.language)}</option>)}
        </select>
      )
    }
    if (p.resultType === 4) return <textarea className="input textarea entry-input" data-entry aria-label={label} rows={3} maxLength={4000} value={c.value} onChange={(e) => setCell(p.parameterId, { value: e.target.value })} />
    return (
      <input className="input entry-input" data-entry data-numeric={p.resultType === 1} aria-label={label} aria-invalid={invalid.has(p.parameterId) || undefined} inputMode={p.resultType === 1 ? 'decimal' : 'text'} autoComplete="off"
        maxLength={p.resultType === 3 ? 500 : 24} value={c.value} onKeyDown={onEnter} onChange={(e) => setCell(p.parameterId, { value: e.target.value })} />
    )
  }

  return (
    <div className="page">
      <Link to={cur && cur.status >= 10 ? '/results/verification' : '/results/entry'} className="back-link no-print"><ArrowLeft size={15} className="flip-rtl" aria-hidden /> {t('results.backToQueue')}</Link>

      <section className="card entry-banner">
        <div style={{ flex: 1, minWidth: 260 }}>
          <div className="patient-hero-top">
            <h2 dir="auto">{pickName(m.testNameEn, m.testNameUr, i18n.language)} <span className="num" style={{ color: 'var(--muted)', fontWeight: 500 }}>{m.testCode}</span></h2>
            {cur ? <ResultStatusBadge status={cur.status} /> : <Badge>{t('results.notStarted')}</Badge>}
            {cur && cur.version > 1 && <Badge tone="warning">{t('results.version', { n: cur.version })}</Badge>}
            {m.priority === 1 && <Badge tone="danger">{t('orders.urgent')}</Badge>}
          </div>
          <dl className="test-facts" style={{ gridTemplateColumns: 'repeat(3, minmax(0, auto))' }}>
            <div><dt>{t('orders.patientCol')}</dt><dd><Link to={`/patients/${m.patientId}`} dir="auto">{patientName({ fullName: m.patientName, fullNameUr: m.patientNameUr }, i18n.language)}</Link> <span className="num" style={{ color: 'var(--muted)' }}>{m.patientNo}</span></dd></div>
            <div><dt>{t('patients.ageSex')}</dt><dd>{formatAge(t, m.patientDateOfBirth)} · {t(`sex.short${m.patientSex}`)}</dd></div>
            <div><dt>{t('samples.order')}</dt><dd><Link to={`/orders/${m.orderId}`} className="num">{m.orderNo}</Link></dd></div>
            <div><dt>{t('samples.barcode')}</dt><dd className="num">{m.barcode}</dd></div>
            <div><dt>{t('samples.collectedAt')}</dt><dd>{m.collectedAtUtc ? formatDateTime(m.collectedAtUtc, i18n.language) : '—'}</dd></div>
            <div><dt>{t('samples.received')}</dt><dd>{formatDateTime(m.receivedAtUtc, i18n.language)} · {m.labBranch}</dd></div>
          </dl>
        </div>
      </section>

      <OrderTestsBar orderId={m.orderId} tests={orderTests} currentItemId={itemId} onGo={go} />

      <div className="form-stack" style={{ margin: '16px 0' }}>
        {amending && <div className="notice is-warning" role="status"><Pencil size={16} aria-hidden /><span><strong>{t('results.amendingBanner', { n: cur!.version })}</strong> <span dir="auto">{cur!.amendmentReason}</span><span className="cell-sub">{t('results.releasedStaysValid', { n: m.released!.version })}</span></span></div>}
        {a.isOwnEntry && cur?.status === 10 && !a.verify && <div className="notice is-info" role="status"><ShieldCheck size={16} aria-hidden /><span>{t('results.ownEntryNotice')}</span></div>}
        {m.alerts.some((x) => x.status === 0) && <div className="notice is-danger" role="alert"><AlertTriangle size={16} aria-hidden /><span><strong>{t('results.openAlertNotice')}</strong></span></div>}
        {noRange.length > 0 && cur?.status !== 30 && <div className="notice is-info" role="status"><AlertTriangle size={16} aria-hidden /><span>{t('results.noRangeNotice', { names: noRange.map((p) => p.code).join(', ') })}</span></div>}
        {!cur && !a.save && <div className="notice is-warning" role="status"><AlertTriangle size={16} aria-hidden /><span>{t('results.cannotEnterNotice')}</span></div>}
      </div>

      <Card flush>
        <div className="table-wrap">
          <table className="entry-table" ref={tableRef}>
            <thead><tr><th>{t('results.parameter')}</th><th style={{ width: '28%' }}>{t('results.result')}</th><th>{t('results.unit')}</th><th>{t('results.referenceRange')}</th><th>{t('results.flag')}</th></tr></thead>
            <tbody>
              {groups.map((g, gi) => (
                <Fragment key={gi}>
                  {g.name && <tr className="group-row"><td colSpan={5}>{g.name}</td></tr>}
                  {g.items.map((p) => {
                    const { flag, preview } = flagOf(p)
                    const saved = m.values.find((v) => v.parameterId === p.parameterId)
                    const rt = saved && !dirty ? saved.rangeText ?? rangeText(saved.range, p.decimalPlaces) : rangeText(p.range, p.decimalPlaces)
                    return (
                      <tr key={p.parameterId} className={flag === 4 || flag === 5 ? 'is-critical' : flag === 2 || flag === 3 || flag === 6 ? 'is-abnormal' : undefined}>
                        <td><span className="cell-strong" dir="auto">{pickName(p.nameEn, p.nameUr, i18n.language)}</span><span className="cell-sub num">{p.code}</span></td>
                        <td>{renderInput(p)}{invalid.has(p.parameterId) && <span className="field-error" role="alert">{t('errors.result_value_invalid')}</span>}</td>
                        <td className="num">{saved?.unitText ?? p.unit ?? ''}</td>
                        <td className="num">{rt ?? <span className="field-hint">{p.resultType === 1 ? t('results.noRange') : ''}</span>}</td>
                        <td><FlagBadge flag={flag} preview={preview} /></td>
                      </tr>
                    )
                  })}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
        {m.reportNotes && <p className="field-hint" style={{ padding: '0 var(--space-5) var(--space-3)' }} dir="auto">{m.reportNotes}</p>}
        <div style={{ padding: 'var(--space-4) var(--space-5)' }}>
          {editable ? <TextAreaField label={t('results.comment')} value={comment} maxLength={1000} rows={2} onChange={(e) => { setComment(e.target.value); setDirty(true) }} /> : cur?.comment && <p className="field-hint" dir="auto"><strong>{t('results.comment')}:</strong> {cur.comment}</p>}
        </div>
        <div className="entry-actions no-print">
          <span className="grow">{dirty ? t('results.unsaved') : cur?.status === 30 ? t('results.releasedInfo', { by: cur.approvedByName ?? '—', at: cur.approvedAtUtc ? formatDateTime(cur.approvedAtUtc, i18n.language) : '' }) : ''}</span>
          {a.discard && <Button variant="ghost" icon={<Trash2 size={16} />} onClick={() => setDialog('discard')} disabled={busy}>{t('results.discard')}</Button>}
          {a.return && <Button variant="ghost" icon={<RotateCcw size={16} />} onClick={() => setDialog('return')} disabled={busy}>{t('results.returnToEntry')}</Button>}
          {a.amend && <Button icon={<Pencil size={16} />} onClick={() => setDialog('amend')}>{t('results.amend')}</Button>}
          {a.save && <Button id="entry-save" icon={<Save size={16} />} loading={save.isPending} disabled={!dirty || invalid.size > 0 || busy} onClick={() => save.mutate()}>{t('results.saveDraft')}</Button>}
          {a.save && <Button variant="primary" icon={<Send size={16} />} loading={submit.isPending} disabled={invalid.size > 0 || busy || (!dirty && !a.submit)} onClick={() => submit.mutate()}>{t('results.submit')}</Button>}
          {a.verify && <Button variant="primary" icon={<CheckCircle2 size={16} />} loading={verify.isPending} disabled={busy} onClick={() => verify.mutate()}>{t('results.verify')}</Button>}
          {a.approve && <Button variant="primary" icon={<ShieldCheck size={16} />} loading={approve.isPending} disabled={busy} onClick={() => approve.mutate()}>{t('results.approve')}</Button>}
          {next && <Button variant="ghost" icon={<ArrowRight size={16} className="flip-rtl" />} disabled={busy} onClick={() => go(`/results/item/${next.orderItemId}`)}>{t('results.nextTest', { code: next.testCode })}</Button>}
        </div>
      </Card>

      <div className="grid-2-1 section-gap no-print">
        <Card title={t('results.history')} flush>
          {m.history.length === 0 ? <p className="field-hint" style={{ padding: 16 }}>{t('results.noHistory')}</p> : (
            <ol className="timeline" style={{ padding: 'var(--space-4) var(--space-5)' }}>
              {m.history.map((h, i) => (
                <li key={i} className={h.step === 'Return' ? 'is-bad' : h.step === 'Approve' ? 'is-good' : undefined}>
                  <strong>{t(`resultStep.${h.step}`, { defaultValue: h.step })}</strong>
                  <span className="cell-sub">{formatDateTime(h.atUtc, i18n.language)}{h.userName ? ` · ${h.userName}` : ''}</span>
                  {h.comment && <span className="cell-sub" dir="auto">{h.comment}</span>}
                </li>
              ))}
            </ol>
          )}
        </Card>
        <Card title={t('results.criticalAlerts')} flush>
          {m.alerts.length === 0 ? <p className="field-hint" style={{ padding: 16 }}>{t('results.noAlerts')}</p> : (
            <ul className="ledger" style={{ padding: '0 var(--space-5)' }}>
              {m.alerts.map((al) => (
                <li key={al.id} style={{ gridTemplateColumns: 'minmax(0, 1fr) auto' }}>
                  <span>
                    <strong dir="auto">{pickName(al.parameterNameEn, al.parameterNameUr, i18n.language)}: <span className="num">{al.valueText}</span></strong>
                    <span className="cell-sub">{al.status === 1 ? t('results.alertDone', { to: al.notifiedTo ?? '', by: al.notifiedByName ?? '' }) : t('results.alertOpen')}</span>
                  </span>
                  {al.status === 0 && can('results.critical') ? <Button size="sm" variant="primary" icon={<Phone size={14} />} onClick={() => setNotifying(al)}>{t('results.report')}</Button> : <Badge tone={al.status === 1 ? 'success' : 'danger'}>{t(`alertStatus.${al.status}`)}</Badge>}
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      {dialog === 'return' && <ReasonDialog title={t('results.returnTitle')} text={t('results.returnText')} confirmLabel={t('results.returnToEntry')} busy={returnIt.isPending} onConfirm={(r) => returnIt.mutate(r)} onClose={() => setDialog(null)} />}
      {dialog === 'amend' && <ReasonDialog title={t('results.amendTitle')} text={t('results.amendText')} confirmLabel={t('results.amend')} busy={amend.isPending} onConfirm={(r) => amend.mutate(r)} onClose={() => setDialog(null)} />}
      <ConfirmDialog open={dialog === 'discard'} icon={<Trash2 size={20} />} tone="danger" title={t('results.discardTitle')} text={t('results.discardText')} confirmLabel={t('results.discard')} loading={discard.isPending} onConfirm={() => discard.mutate()} onCancel={() => setDialog(null)} />
      {notifying && <NotifyDialog alert={notifying} onClose={() => setNotifying(null)} />}
      <ConfirmDialog open={leaveTo !== null} icon={<AlertTriangle size={20} />} tone="danger" title={t('results.leaveTitle')} text={t('results.leaveText')} confirmLabel={t('results.leaveConfirm')}
        onConfirm={() => { const to = leaveTo!; setLeaveTo(null); setDirty(false); navigate(to) }} onCancel={() => setLeaveTo(null)} />
    </div>
  )
}
