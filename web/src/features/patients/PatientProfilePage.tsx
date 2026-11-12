import { useState } from 'react'
import { useQueries, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, ClipboardList, FileText, Pencil, Printer, UserX } from 'lucide-react'
import { api, ApiError } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime, formatLongDate } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Tabs } from '../../components/ui/Controls'
import { EmptyState } from '../../components/ui/EmptyState'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import { formatAge, formatPhone, patientKeys, patientName, type PatientDetail } from './patientsApi'
import { PatientFormModal } from './PatientFormModal'
import { PatientHistoryGraph } from './PatientHistoryGraph'
import type { OrderListItem } from '../orders/ordersApi'
import { reportTone, type ReportVersion } from '../reports/reportsApi'
import './patients.css'

export function PatientProfilePage() {
  const { t, i18n } = useTranslation()
  const { id } = useParams()
  const { can, user } = useSession()
  const patientId = Number(id)
  const [editing, setEditing] = useState(false)
  const [slipPreview, setSlipPreview] = useState(false)
  const [tab, setTab] = useState<'overview' | 'orders' | 'history'>('overview')

  const { data: p, isLoading, error } = useQuery({
    queryKey: patientKeys.detail(patientId),
    queryFn: () => api<PatientDetail>(`/api/patients/${patientId}`),
    enabled: Number.isInteger(patientId),
  })

  if (isLoading) {
    return (
      <div className="page">
        <Skeleton height={120} radius={12} />
        <div style={{ height: 16 }} />
        <Skeleton height={260} radius={12} />
      </div>
    )
  }
  if (!p) {
    return (
      <div className="page">
        <Card>
          <EmptyState
            icon={<UserX size={22} />}
            title={t('common.notFound')}
            text={error instanceof ApiError && error.status === 404 ? t('errors.not_found') : t('errors.server_error')}
            action={<Link to="/patients" className="btn btn-secondary"><ArrowLeft size={16} className="flip-rtl" /> {t('patients.back')}</Link>}
          />
        </Card>
      </div>
    )
  }

  const name = patientName(p, i18n.language)
  const dob = p.dateOfBirth ? new Date(`${p.dateOfBirth}T00:00:00`) : null
  const printSlip = () => {
    const pageStyle = document.createElement('style')
    pageStyle.textContent = '@page { size: A5 portrait; margin: 12mm; }'
    document.head.appendChild(pageStyle)
    const cleanup = () => {
      document.body.classList.remove('printing-patient-slip')
      pageStyle.remove()
      setSlipPreview(false)
    }
    document.body.classList.add('printing-patient-slip')
    window.addEventListener('afterprint', cleanup, { once: true })
    window.print()
  }

  return (
    <div className="page">
      <Link to="/patients" className="back-link no-print">
        <ArrowLeft size={15} className="flip-rtl" aria-hidden /> {t('patients.back')}
      </Link>

      <section className="card patient-hero no-print">
        <div className="patient-avatar" aria-hidden>{p.fullName.charAt(0).toUpperCase()}</div>
        <div className="patient-hero-main">
          <div className="patient-hero-top">
            <h2 dir="auto">{name}</h2>
            <span className="patient-no num">{p.patientNo}</span>
          </div>
          {i18n.language !== 'ur' && p.fullNameUr && <p className="patient-name-ur" lang="ur" dir="rtl">{p.fullNameUr}</p>}
          <div className="patient-facts">
            <span>{t(`sex.${p.sex}`)}</span>
            <span>
              {formatAge(t, p.dateOfBirth)}
              {p.isDateOfBirthEstimated && <Badge tone="warning">{t('age.estimated')}</Badge>}
            </span>
            {p.guardianName && <span dir="auto">{t('patients.guardian')}: {p.guardianName}</span>}
            <span className="num">{formatPhone(p.mobile)}</span>
          </div>
        </div>
        <div className="patient-hero-actions">
          <Button icon={<Printer size={16} />} onClick={() => setSlipPreview(true)}>{t('patients.printSlip')}</Button>
          {can('patients.edit') && <Button icon={<Pencil size={16} />} onClick={() => setEditing(true)}>{t('common.edit')}</Button>}
          {can('orders.create') && (
            <Link to={`/orders/new?patient=${p.id}`} className="btn btn-primary"><ClipboardList size={16} /> {t('patients.newOrder')}</Link>
          )}
        </div>
      </section>

      <div className="no-print" style={{ margin: '16px 0' }}>
        <Tabs value={tab} onChange={setTab} tabs={[
          { key: 'overview', label: t('patients.tabOverview') },
          { key: 'orders', label: t('patients.tabOrders') },
          { key: 'history', label: t('patients.tabHistory') },
        ]} />
      </div>

      {tab === 'overview' ? (
        <div className="grid-2-1 no-print">
          <Card title={t('patients.details')}>
            <dl className="details-grid">
              <Detail label={t('patients.patientNo')} value={<span className="num">{p.patientNo}</span>} />
              <Detail label={t('patients.fullName')} value={<span dir="auto">{p.title ? `${p.title} ${p.fullName}` : p.fullName}</span>} />
              <Detail label={t('patients.fullNameUr')} value={p.fullNameUr && <span lang="ur" dir="rtl" className="urdu-text">{p.fullNameUr}</span>} />
              <Detail label={t('patients.guardian')} value={p.guardianName} />
              <Detail label={t('patients.sexLabel')} value={t(`sex.${p.sex}`)} />
              {/* An estimate (from a stated age) is shown as a year only, so it isn't mistaken for a real date. */}
              <Detail
                label={t('patients.dob')}
                value={dob && (p.isDateOfBirthEstimated
                  ? <><span className="num">≈ {dob.getFullYear()}</span> <Badge tone="warning">{t('age.estimated')}</Badge></>
                  : formatLongDate(dob, i18n.language))}
              />
              <Detail label={t('patients.cnic')} value={p.cnic && <span className="num">{p.cnic}</span>} />
              <Detail label={t('patients.notes')} value={p.notes} wide />
            </dl>
          </Card>
          <Card title={t('patients.contact')}>
            <dl className="details-grid one-col">
              <Detail label={t('patients.mobile')} value={<span className="num">{formatPhone(p.mobile)}</span>} />
              <Detail label={t('patients.altPhone')} value={p.altPhone && <span className="num">{formatPhone(p.altPhone)}</span>} />
              <Detail label={t('common.email')} value={p.email && <span className="num">{p.email}</span>} />
              <Detail label={t('patients.address')} value={p.address} />
              <Detail label={t('patients.city')} value={p.city} />
              <Detail label={t('patients.registeredAt')} value={formatDateTime(p.registeredAtUtc, i18n.language)} />
              <Detail label={t('patients.registeredBy')} value={p.registeredByName} />
              <Detail label={t('patients.branch')} value={p.registeredBranchName} />
            </dl>
            {!p.sensitiveVisible && <p className="field-hint" style={{ marginTop: 12 }}>{t('patients.hiddenSensitive')}</p>}
          </Card>
        </div>
      ) : tab === 'orders' ? (
        <PatientOrdersPanel patientId={p.id} canViewOrders={can('orders.view')} canViewReports={can('reports.view')} />
      ) : (
        <div className="patient-history-print-container"><PatientHistoryGraph patientId={p.id} patientName={name} patientNo={p.patientNo} /></div>
      )}

      {editing && <PatientFormModal patient={p} onClose={() => setEditing(false)} />}
      <Modal open={slipPreview} title={t('patients.slipPreview')} subtitle={t('patients.slipPreviewHint')} onClose={() => setSlipPreview(false)} size="md"
        footer={<><Button variant="ghost" onClick={() => setSlipPreview(false)}>{t('common.cancel')}</Button>
          <Button variant="primary" icon={<Printer size={16} />} onClick={printSlip}>{t('patients.printSlip')}</Button></>}>
        <PatientSlip patient={p} organizationName={i18n.language === 'ur' && user?.organizationNameUr ? user.organizationNameUr : user?.organizationNameEn ?? ''} />
      </Modal>
    </div>
  )
}

function PatientOrdersPanel({ patientId, canViewOrders, canViewReports }: { patientId: number; canViewOrders: boolean; canViewReports: boolean }) {
  const { t, i18n } = useTranslation()
  const [page, setPage] = useState(1)
  const pageSize = 20
  const orders = useQuery({
    queryKey: ['patients', 'orders', patientId, page],
    queryFn: () => api<{ items: OrderListItem[]; total: number }>(`/api/orders`, { query: { patientId, page, pageSize } }),
    enabled: canViewOrders,
  })
  const rows = orders.data?.items ?? []
  const reports = useQueries({
    queries: rows.map((order) => ({
      queryKey: ['reports', 'versions', order.id],
      queryFn: () => api<ReportVersion[]>(`/api/orders/${order.id}/reports`),
      enabled: canViewReports,
      staleTime: 30_000,
    })),
  })

  if (!canViewOrders) return <Card><EmptyState icon={<FileText size={22} />} title={t('patients.ordersUnavailable')} text={t('patients.ordersPermissionHint')} /></Card>
  if (orders.isLoading) return <Card title={t('patients.ordersAndReports')}><p className="field-hint">{t('common.loading')}</p></Card>
  if (orders.isError) return <Card title={t('patients.ordersAndReports')}><p className="field-error">{errorMessage(t, orders.error)}</p></Card>
  if (rows.length === 0) return <Card><EmptyState icon={<FileText size={22} />} title={t('patients.ordersEmpty')} text={t('patients.ordersEmptyText')} /></Card>

  return (
    <Card title={t('patients.ordersAndReports')} subtitle={t('patients.ordersAndReportsSubtitle')}>
      <div className="patient-order-list">
        {rows.map((order, index) => {
          const versions = reports[index]?.data ?? []
          const latest = versions.find((version) => version.status !== 90) ?? versions[0]
          return (
            <article className="patient-order-row" key={order.id}>
              <div className="patient-order-main">
                <Link to={`/orders/${order.id}`} className="patient-order-number">{order.orderNo}</Link>
                <span className="patient-order-date">{formatDateTime(order.createdAtUtc, i18n.language)}</span>
                <span>{t('patients.orderTests', { count: order.testCount })}</span>
                <Badge tone={order.status === 1 ? 'neutral' : 'success'}>{t(`patients.orderStatus.${order.status}`)}</Badge>
              </div>
              <div className="patient-order-actions">
                <span className="patient-order-total">{t('patients.orderTotal')}: <b className="num">{order.net.toLocaleString(i18n.language === 'ur' ? 'ur-PK' : 'en-PK')}</b></span>
                {canViewReports && (reports[index]?.isLoading ? <span className="field-hint">{t('common.loading')}</span> : latest ? (
                  <Link to={`/reports/order/${order.id}`} className="btn btn-secondary btn-sm">
                    <FileText size={14} /> {t('reports.title')} · {latest.reportNo} <Badge tone={reportTone[latest.status]}>{t(`reportStatus.${latest.status}`)}</Badge>
                  </Link>
                ) : <span className="field-hint">{t('patients.noReportYet')}</span>)}
              </div>
              {reports[index]?.isError && canViewReports && <p className="field-error">{errorMessage(t, reports[index]!.error)}</p>}
            </article>
          )
        })}
      </div>
      {(orders.data?.total ?? rows.length) > pageSize && (
        <div className="patient-order-pagination">
          <span className="field-hint">{t('patients.orderPage', { page, pages: Math.ceil((orders.data?.total ?? 0) / pageSize) })}</span>
          <div className="toolbar">
            <Button size="sm" variant="secondary" icon={<ArrowLeft size={15} className="flip-rtl" />} onClick={() => setPage((current) => Math.max(1, current - 1))} disabled={page === 1}>{t('table.previous')}</Button>
            <Button size="sm" variant="secondary" onClick={() => setPage((current) => current + 1)}
              disabled={page >= Math.ceil((orders.data?.total ?? 0) / pageSize)}>{t('table.next')}</Button>
          </div>
        </div>
      )}
    </Card>
  )
}

function PatientSlip({ patient: p, organizationName }: { patient: PatientDetail; organizationName: string }) {
  const { t, i18n } = useTranslation()
  return (
    <section className="patient-slip-sheet">
      <header><strong>{organizationName}</strong><span>{t('patients.slipTitle')}</span></header>
      <div className="slip-no num">{p.patientNo}</div>
      <table><tbody>
        <tr><th>{t('patients.fullName')}</th><td dir="auto">{p.title ? `${p.title} ${p.fullName}` : p.fullName}</td></tr>
        {p.fullNameUr && <tr><th>{t('patients.fullNameUr')}</th><td lang="ur" dir="rtl" className="urdu-text">{p.fullNameUr}</td></tr>}
        {p.guardianName && <tr><th>{t('patients.guardian')}</th><td dir="auto">{p.guardianName}</td></tr>}
        <tr><th>{t('patients.ageSex')}</th><td>{formatAge(t, p.dateOfBirth)} / {t(`sex.${p.sex}`)}</td></tr>
        <tr><th>{t('patients.mobile')}</th><td className="num">{formatPhone(p.mobile)}</td></tr>
        <tr><th>{t('patients.registeredAt')}</th><td>{formatDateTime(p.registeredAtUtc, i18n.language)}</td></tr>
      </tbody></table>
      <footer>{t('patients.slipNote')}</footer>
    </section>
  )
}

function Detail({ label, value, wide }: { label: string; value: React.ReactNode; wide?: boolean }) {
  return (
    <div className={wide ? 'detail wide' : 'detail'}>
      <dt>{label}</dt>
      <dd>{value || <span className="detail-empty">—</span>}</dd>
    </div>
  )
}
