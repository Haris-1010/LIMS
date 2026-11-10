import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, Eye, FilePlus2, Fingerprint, Printer } from 'lucide-react'
import { api, ApiError } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { SelectField, TextAreaField } from '../../components/ui/Controls'
import { EmptyState } from '../../components/ui/EmptyState'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import { PrintReport } from './PrintReport'
import { ReportDocument } from './ReportDocument'
import { reportKeys, reportTone, type ReportLanguage, type ReportVersion, type ReportView, type TemplateDto } from './reportsApi'
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
  const [selected, setSelected] = useState<number | 'preview' | null>(null)
  const [issuing, setIssuing] = useState(false)
  const [printing, setPrinting] = useState<ReportView | null>(null)

  const versions = useQuery({ queryKey: reportKeys.versions(orderId), queryFn: () => api<ReportVersion[]>(`/api/orders/${orderId}/reports`), enabled: Number.isInteger(orderId) })
  const template = useQuery({ queryKey: reportKeys.template, queryFn: () => api<TemplateDto>('/api/report-template'), staleTime: 60_000 })
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
    mutationFn: (id: number) => api(`/api/reports/${id}/printed`, { method: 'POST' }),
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
          {effective !== 'preview' && <Button variant="ghost" icon={<Eye size={16} />} onClick={() => setSelected('preview')}>{t('reports.previewCurrent')}</Button>}
          {canIssue && <Button variant="primary" icon={<FilePlus2 size={16} />} onClick={() => setIssuing(true)} disabled={view.isError}>{live ? t('reports.issueNewVersion') : t('reports.issue')}</Button>}
          {v && !v.isPreview && can('reports.print') && <Button icon={<Printer size={16} />} onClick={() => setPrinting(v)}>{t('reports.print')}</Button>}
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
          {view.isLoading || !v ? <Skeleton height={600} width={600} radius={4} /> : <ReportDocument view={v} hasLogo={!!template.data?.hasLogo} />}
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
      {printing && <PrintReport view={printing} hasLogo={!!template.data?.hasLogo} onDone={() => { const id = printing.meta.id; setPrinting(null); if (id) printed.mutate(id) }} />}
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
