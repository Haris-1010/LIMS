import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, Eye, FileDown, FilePlus2, Fingerprint, MessageCircle, Printer } from 'lucide-react'
import { api, ApiError } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { CheckList, SelectField, TextAreaField } from '../../components/ui/Controls'
import { Field } from '../../components/ui/Field'
import { EmptyState } from '../../components/ui/EmptyState'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import { PrintReport } from './PrintReport'
import { ReportDocument } from './ReportDocument'
import { defaultLayout, designFor } from './reportDesign'
import { imagesOf, reportKeys, reportTone, type ReportLanguage, type ReportLayoutDto, type ReportVersion, type ReportView, type TemplateDto } from './reportsApi'
import './report.css'

/** One order's report: preview what would be issued, issue it, open earlier versions, print and check integrity. */
export function ReportOrderPage() {
  const { t, i18n } = useTranslation()
  const { orderId: orderParam } = useParams()
  const orderId = Number(orderParam)
  const qc = useQueryClient()
  const toast = useToast()
  const { can } = useSession()
  const [lang, setLang] = useState<ReportLanguage>(0)
  /** Language to show and print an issued report in; null = the language it was issued in. */
  const [copyLang, setCopyLang] = useState<ReportLanguage | null>(null)
  const [selected, setSelected] = useState<number | 'preview' | null>(null)
  const [issuing, setIssuing] = useState(false)
  const [printing, setPrinting] = useState<PrintJob | null>(null)
  const [printOptions, setPrintOptions] = useState(false)
  const [sharing, setSharing] = useState(false)
  /** Chosen design: undefined = the organisation's default, null = built-in classic. */
  const [layoutChoice, setLayoutChoice] = useState<number | null | undefined>(undefined)

  const versions = useQuery({ queryKey: reportKeys.versions(orderId), queryFn: () => api<ReportVersion[]>(`/api/orders/${orderId}/reports`), enabled: Number.isInteger(orderId) })
  const template = useQuery({ queryKey: reportKeys.template, queryFn: () => api<TemplateDto>('/api/report-template'), staleTime: 60_000 })
  const layouts = useQuery({ queryKey: reportKeys.layouts, queryFn: () => api<ReportLayoutDto[]>('/api/report-layouts'), staleTime: 60_000 })
  const layoutId = layoutChoice === undefined ? defaultLayout(layouts.data)?.id ?? null : layoutChoice
  const design = designFor(layouts.data, layoutId)
  const list = versions.data ?? []
  const live = list.find((v) => v.status !== 90)
  const effective: number | 'preview' = selected ?? live?.id ?? 'preview'

  const view = useQuery({
    queryKey: effective === 'preview' ? reportKeys.preview(orderId, lang) : reportKeys.view(effective),
    queryFn: () => (effective === 'preview'
      ? api<ReportView>(`/api/orders/${orderId}/reports/preview`, { method: 'POST', body: { language: lang } })
      : api<ReportView>(`/api/reports/${effective}`)),
    enabled: versions.isSuccess,
    staleTime: effective === 'preview' ? 0 : 30_000,
    retry: false,
  })
  const v = view.data

  const refresh = async () => { await qc.invalidateQueries({ queryKey: reportKeys.all }) }
  const canIssue = can('reports.issue') && (!live || live.isOutdated)

  const verify = useMutation({
    mutationFn: () => api<{ intact: boolean; fingerprint: string }>(`/api/reports/${v!.meta.id}/verify`),
    onSuccess: (r) => toast.show({ tone: r.intact ? 'success' : 'danger', title: r.intact ? t('reports.intact') : t('reports.notIntact'), text: r.fingerprint }),
    onError: (e) => toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: errorMessage(t, e) }),
  })
  const printed = useMutation({
    mutationFn: (job: PrintJob) => api(`/api/reports/${job.view.meta.id}/printed`, { method: 'POST', body: { layoutId: job.layoutId, orderItemIds: job.onlyTests ?? null, language: job.language } }),
    onSettled: refresh,
  })

  if (versions.isLoading) return <div className="page"><Skeleton height={80} radius={12} /><div style={{ height: 16 }} /><Skeleton height={400} radius={12} /></div>
  if (versions.isError) {
    return (
      <div className="page"><Card><EmptyState icon={<Eye size={22} />} title={t('common.notFound')} text={versions.error instanceof ApiError && versions.error.status === 404 ? t('errors.not_found') : errorMessage(t, versions.error)}
        action={<Link to="/reports" className="btn btn-secondary"><ArrowLeft size={16} className="flip-rtl" /> {t('reports.back')}</Link>} /></Card></div>
    )
  }

  return (
    <div className="page">
      <Link to="/reports" className="back-link no-print"><ArrowLeft size={15} className="flip-rtl" aria-hidden /> {t('reports.back')}</Link>
      <div className="page-head">
        <div>
          <h2>{v ? <><span className="num">{v.meta.reportNo}</span> {v.isPreview ? <Badge tone="info">{t('reports.preview')}</Badge> : <Badge tone={reportTone[v.meta.status]} dot>{t(`reportStatus.${v.meta.status}`)}</Badge>}</> : t('reports.title')}</h2>
          {v && <p>{v.snapshot.order.orderNo} · <span dir="auto">{i18n.language === 'ur' && v.snapshot.patient.nameUr ? v.snapshot.patient.nameUr : v.snapshot.patient.nameEn}</span></p>}
        </div>
        <span className="toolbar">
          {effective === 'preview' && (
            <select className="select select-sm" value={lang} onChange={(e) => setLang(Number(e.target.value) as ReportLanguage)} aria-label={t('reports.language')}>
              {[0, 1, 2].map((l) => <option key={l} value={l}>{t(`reportLanguage.${l}`)}</option>)}
            </select>
          )}
          {v && !v.isPreview && (
            <select className="select select-sm" value={copyLang ?? v.snapshot.language} onChange={(e) => setCopyLang(Number(e.target.value) as ReportLanguage)} aria-label={t('reports.copyLanguage')} title={t('reports.copyLanguageHint')}>
              {[0, 1, 2].map((l) => <option key={l} value={l}>{t(`reportLanguage.${l}`)}{l === v.snapshot.language ? ` (${t('reports.asIssued')})` : ''}</option>)}
            </select>
          )}
          {effective !== 'preview' && <Button variant="ghost" icon={<Eye size={16} />} onClick={() => setSelected('preview')}>{t('reports.previewCurrent')}</Button>}
          {canIssue && <Button variant="primary" icon={<FilePlus2 size={16} />} onClick={() => setIssuing(true)} disabled={view.isError}>{live ? t('reports.issueNewVersion') : t('reports.issue')}</Button>}
          {(layouts.data?.length ?? 0) > 0 && (
            <select className="select select-sm" value={layoutId ?? ''} onChange={(e) => setLayoutChoice(e.target.value === '' ? null : Number(e.target.value))} aria-label={t('reports.design')}>
              <option value="">{t('reports.builtIn')}</option>
              {layouts.data!.map((l) => <option key={l.id} value={l.id}>{l.name}{l.isDefault ? ` (${t('reports.default')})` : ''}</option>)}
            </select>
          )}
          {v && !v.isPreview && can('reports.print') && <Button icon={<Printer size={16} />} onClick={() => setPrintOptions(true)}>{t('reports.print')}</Button>}
          {v && !v.isPreview && can('reports.print') && v.meta.status !== 90 && <Button icon={<MessageCircle size={16} />} onClick={() => setSharing(true)}>{t('reports.whatsapp')}</Button>}
          {v && !v.isPreview && <Button variant="ghost" icon={<Fingerprint size={16} />} loading={verify.isPending} onClick={() => verify.mutate()}>{t('reports.verify')}</Button>}
        </span>
      </div>

      <div className="form-stack" style={{ marginBottom: 12 }}>
        {v?.meta.isOutdated && <div className="notice is-warning" role="status"><span>{t('reports.outdatedNotice')}</span></div>}
        {view.isError && <div className="notice is-danger" role="alert"><span>{errorMessage(t, view.error)}</span></div>}
        {v?.isPreview && !view.isError && <div className="notice is-info" role="status"><span>{t('reports.previewNotice')}</span></div>}
      </div>

      <div className="report-layout">
        <div className="report-stage">
          {view.isLoading || !v ? <Skeleton height={600} width={600} radius={4} /> : <ReportDocument view={v} images={imagesOf(template.data)} design={design} language={v.isPreview ? undefined : copyLang ?? undefined} />}
        </div>
        <aside className="no-print">
          <Card title={t('reports.versions')} flush>
            {list.length === 0 ? <p className="field-hint" style={{ padding: 16 }}>{t('reports.noVersions')}</p> : (
              <ul className="ledger" style={{ padding: '0 var(--space-4)' }}>
                {list.map((x) => (
                  <li key={x.id} style={{ gridTemplateColumns: 'minmax(0, 1fr) auto' }}>
                    <button type="button" onClick={() => setSelected(x.id)} className="link-btn" style={{ textAlign: 'start', background: 'none', border: 0, padding: 0, cursor: 'pointer', font: 'inherit', color: 'inherit', fontWeight: effective === x.id ? 700 : 400 }}>
                      <span className="num cell-strong">{x.reportNo} · {t('reports.versionShort', { n: x.version })}</span>
                      <span className="cell-sub">{formatDateTime(x.issuedAtUtc, i18n.language)} · {x.issuedByName}</span>
                      {x.amendmentReason && <span className="cell-sub" dir="auto">{x.amendmentReason}</span>}
                    </button>
                    <span className="badge-list"><Badge tone={reportTone[x.status]}>{t(`reportStatus.${x.status}`)}</Badge>{x.isOutdated && <Badge tone="danger">{t('reports.outdated')}</Badge>}</span>
                  </li>
                ))}
              </ul>
            )}
          </Card>
          {v && !v.isPreview && <p className="field-hint" style={{ marginTop: 8 }}>{t('reports.printInfo', { count: v.meta.printCount })}</p>}
        </aside>
      </div>

      {issuing && <IssueDialog orderId={orderId} hasLive={!!live} preview={view.data?.isPreview ? view.data : null} initialLang={lang}
        onClose={() => setIssuing(false)} onIssued={async (r) => { setIssuing(false); setSelected(r.meta.id); await refresh() }} />}
      {printOptions && v && !v.isPreview && (
        <PrintDialog view={v} layouts={layouts.data ?? []} layoutId={layoutId} onLayout={setLayoutChoice} language={copyLang ?? v.snapshot.language} onLanguage={setCopyLang} onClose={() => setPrintOptions(false)}
          onPrint={(onlyTests, onlyParameters) => { setPrintOptions(false); setPrinting({ view: v, layoutId, onlyTests, onlyParameters, language: copyLang ?? undefined }) }} />
      )}
      {sharing && v && !v.isPreview && (
        <ShareDialog view={v} onClose={() => setSharing(false)} onSavePdf={() => setPrinting({ view: v, layoutId, onlyTests: undefined, onlyParameters: undefined, language: copyLang ?? undefined })} />
      )}
      {printing && <PrintReport view={printing.view} images={imagesOf(template.data)} design={designFor(layouts.data, printing.layoutId)} onlyTests={printing.onlyTests} onlyParameters={printing.onlyParameters} language={printing.language}
        onDone={() => { const job = printing; setPrinting(null); if (job.view.meta.id) printed.mutate(job) }} />}
    </div>
  )
}

function IssueDialog({ orderId, hasLive, preview, initialLang, onClose, onIssued }: {
  orderId: number; hasLive: boolean; preview: ReportView | null; initialLang: ReportLanguage; onClose: () => void; onIssued: (r: ReportView) => void
}) {
  const { t } = useTranslation()
  const toast = useToast()
  const [lang, setLang] = useState<ReportLanguage>(initialLang)
  const [reason, setReason] = useState('')
  const issue = useMutation({
    mutationFn: () => api<ReportView>(`/api/orders/${orderId}/reports`, { method: 'POST', body: { language: lang, reason: reason.trim() || null } }),
    onSuccess: (r) => { toast.show({ tone: 'success', title: t('reports.issued'), text: `${r.meta.reportNo} · ${t('reports.versionShort', { n: r.meta.version })}` }); onIssued(r) },
    onError: (e) => toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: errorMessage(t, e) }),
  })
  const interim = preview ? preview.snapshot.status === 0 : false
  return (
    <Modal open title={hasLive ? t('reports.issueNewVersion') : t('reports.issue')} subtitle={t('reports.issueHint')} onClose={onClose} size="md"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" icon={<FilePlus2 size={16} />} loading={issue.isPending} disabled={hasLive && !reason.trim()} onClick={() => issue.mutate()}>{t('reports.issue')}</Button></>}>
      <div className="form-stack">
        {interim && <div className="notice is-warning" role="status"><span>{t('reports.interimNotice', { count: preview?.snapshot.pending.length ?? 0 })}</span></div>}
        <SelectField label={t('reports.language')} value={lang} onChange={(e) => setLang(Number(e.target.value) as ReportLanguage)}>
          {[0, 1, 2].map((l) => <option key={l} value={l}>{t(`reportLanguage.${l}`)}</option>)}
        </SelectField>
        {hasLive && <TextAreaField label={`${t('reports.reason')} *`} value={reason} maxLength={500} rows={3} onChange={(e) => setReason(e.target.value)} />}
        {hasLive && <p className="field-hint">{t('reports.reasonHint')}</p>}
        <p className="field-hint">{t('reports.frozenHint')}</p>
      </div>
    </Modal>
  )
}

/** One print (or "Save as PDF"): the report, the design and, for a partial copy, the chosen tests. */
interface PrintJob {
  view: ReportView
  layoutId: number | null
  onlyTests: number[] | undefined
  onlyParameters: Record<number, string[]> | undefined
  /** undefined = the language the report was issued in. */
  language: ReportLanguage | undefined
}

/** Choose the design and which tests go on this copy. Choosing every test is a normal full print. */
function PrintDialog({ view, layouts, layoutId, onLayout, language, onLanguage, onClose, onPrint }: {
  view: ReportView; layouts: ReportLayoutDto[]; layoutId: number | null; onLayout: (id: number | null) => void
  language: ReportLanguage; onLanguage: (l: ReportLanguage) => void; onClose: () => void; onPrint: (onlyTests: number[] | undefined, onlyParameters: Record<number, string[]> | undefined) => void
}) {
  const { t, i18n } = useTranslation()
  const tests = view.snapshot.sections.flatMap((sec) => sec.tests)
  const [chosen, setChosen] = useState<(number | string)[]>(tests.map((x) => x.orderItemId))
  const [chosenParameters, setChosenParameters] = useState<Record<number, string[]>>(
    () => Object.fromEntries(tests.map((x) => [x.orderItemId, x.parameters.map((p) => p.code)])),
  )
  const all = chosen.length === tests.length
  const parametersComplete = tests.every((test) => chosen.includes(test.orderItemId) ? (chosenParameters[test.orderItemId]?.length ?? 0) > 0 : true)
  const ur = i18n.language === 'ur'
  return (
    <Modal open title={t('reports.printOptions')} subtitle={t('reports.printOptionsHint')} onClose={onClose} size="md"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
        <Button variant="primary" icon={<Printer size={16} />} disabled={chosen.length === 0 || !parametersComplete} onClick={() => onPrint(all ? undefined : chosen.map(Number), parametersComplete && chosen.every((id) => (chosenParameters[Number(id)]?.length ?? 0) === (tests.find((x) => x.orderItemId === Number(id))?.parameters.length ?? 0)) ? undefined : chosenParameters)}>{t('reports.printNow')}</Button></>}>
      <div className="form-stack">
        <SelectField label={t('reports.design')} value={layoutId ?? ''} onChange={(e) => onLayout(e.target.value === '' ? null : Number(e.target.value))}>
          <option value="">{t('reports.builtIn')}</option>
          {layouts.map((l) => <option key={l.id} value={l.id}>{l.name}{l.isDefault ? ` (${t('reports.default')})` : ''}</option>)}
        </SelectField>
        <SelectField label={t('reports.copyLanguage')} hint={t('reports.copyLanguageHint')} value={language} onChange={(e) => onLanguage(Number(e.target.value) as ReportLanguage)}>
          {[0, 1, 2].map((l) => <option key={l} value={l}>{t(`reportLanguage.${l}`)}{l === view.snapshot.language ? ` (${t('reports.asIssued')})` : ''}</option>)}
        </SelectField>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8, marginBottom: 8 }}>
            <strong>{t('reports.testsToPrint')}</strong>
            <span className="toolbar">
              <Button size="sm" variant="ghost" onClick={() => setChosen(tests.map((x) => x.orderItemId))}>{t('reports.selectAll')}</Button>
              <Button size="sm" variant="ghost" onClick={() => setChosen([])}>{t('reports.selectNone')}</Button>
            </span>
          </div>
          <div className="print-tests">
            <CheckList columns={1} selected={chosen} onChange={setChosen}
              items={tests.map((x) => ({ id: x.orderItemId, label: ur && x.nameUr ? x.nameUr : x.nameEn, sub: x.code }))} />
          </div>
          <div className="print-parameters">
            <strong>{t('reports.parametersToPrint')}</strong>
            {tests.filter((test) => chosen.includes(test.orderItemId)).map((test) => (
              <div key={test.orderItemId} className="print-parameter-group">
                <span className="cell-strong">{ur && test.nameUr ? test.nameUr : test.nameEn}</span>
                <CheckList columns={1} selected={chosenParameters[test.orderItemId] ?? []} onChange={(next) => setChosenParameters((current) => ({ ...current, [test.orderItemId]: next.map(String) }))}
                  items={test.parameters.map((p) => ({ id: p.code, label: ur && p.nameUr ? p.nameUr : p.nameEn, sub: p.unit ?? undefined }))} />
              </div>
            ))}
          </div>
          <p className="field-hint" style={{ marginTop: 8 }}>{t('reports.testsChosen', { count: chosen.length, total: tests.length })}</p>
          {chosen.length === 0 && <p className="field-error">{t('reports.chooseTests')}</p>}
          {chosen.length > 0 && !all && <div className="notice is-info" role="status" style={{ marginTop: 8 }}><span>{t('reports.partialNote')}</span></div>}
        </div>
      </div>
    </Modal>
  )
}

/**
 * WhatsApp without internet on the server: the number is checked (and the share recorded) by the API, then the browser opens
 * wa.me with the number and message in a new tab. A file cannot be attached through a link, so staff save the PDF first and
 * attach it in WhatsApp.
 */
function ShareDialog({ view, onClose, onSavePdf }: { view: ReportView; onClose: () => void; onSavePdf: () => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const s = view.snapshot
  const name = i18n.language === 'ur' && s.patient.nameUr ? s.patient.nameUr : s.patient.nameEn
  const lab = i18n.language === 'ur' && s.letterhead.organizationNameUr ? s.letterhead.organizationNameUr : s.letterhead.organizationNameEn
  const [phone, setPhone] = useState(s.patient.mobile ?? '')
  const [message, setMessage] = useState(t('reports.whatsappDefault', { name, reportNo: view.meta.reportNo, lab }))
  const [error, setError] = useState<string | null>(null)
  const share = useMutation({
    mutationFn: () => api<{ phone: string }>(`/api/reports/${view.meta.id}/share`, { method: 'POST', body: { channel: 'whatsapp', phone } }),
    onSuccess: (r) => {
      setError(null)
      window.open(`https://wa.me/${r.phone}?text=${encodeURIComponent(message)}`, '_blank', 'noopener,noreferrer')
      toast.show({ tone: 'success', title: t('reports.whatsappOpened') })
      onClose()
    },
    onError: (e) => setError(errorMessage(t, e)),
  })
  return (
    <Modal open title={t('reports.whatsappTitle')} subtitle={t('reports.whatsappHint')} onClose={onClose} size="md"
      footer={<Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>}>
      <div className="form-stack">
        <div>
          <strong>{t('reports.whatsappStep1')}</strong>
          <p className="field-hint">{t('reports.whatsappStep1Hint')}</p>
          <Button icon={<FileDown size={16} />} onClick={onSavePdf}>{t('reports.savePdf')}</Button>
        </div>
        <strong>{t('reports.whatsappStep2')}</strong>
        <Field label={t('reports.whatsappNumber')} hint={t('reports.whatsappNumberHint')} value={phone} inputMode="tel" dir="ltr" maxLength={30}
          error={error ?? undefined} onChange={(e) => setPhone(e.target.value)} />
        <TextAreaField label={t('reports.whatsappMessage')} value={message} rows={4} maxLength={1000} onChange={(e) => setMessage(e.target.value)} />
        <div><Button variant="primary" icon={<MessageCircle size={16} />} loading={share.isPending} disabled={!phone.trim()} onClick={() => share.mutate()}>{t('reports.openWhatsapp')}</Button></div>
      </div>
    </Modal>
  )
}
