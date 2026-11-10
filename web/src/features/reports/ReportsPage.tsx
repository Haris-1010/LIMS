import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { FileText } from 'lucide-react'
import { api } from '../../lib/api'
import { formatDateTime } from '../../lib/format'
import { Badge } from '../../components/ui/Badge'
import { SearchInput, Tabs } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { patientName } from '../patients/patientsApi'
import type { Paged } from '../admin/adminApi'
import { reportKeys, reportTone, type ReportListItem } from './reportsApi'
import './report.css'

const PAGE_SIZE = 25
type Tab = 'ready' | 'issued'

/** Orders whose results are released: the ones that still need a report, and the reports already issued. */
export function ReportsPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const [tab, setTab] = useState<Tab>('ready')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const params = { tab, search, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: reportKeys.list(params), queryFn: () => api<Paged<ReportListItem>>('/api/reports', { query: params }), placeholderData: keepPreviousData, refetchInterval: 30_000 })

  const columns: Column<ReportListItem>[] = [
    { key: 'order', header: t('reports.order'), width: 170, render: (x) => <span className="num cell-strong">{x.orderNo}</span> },
    { key: 'patient', header: t('orders.patientCol'), render: (x) => <span><span dir="auto">{patientName({ fullName: x.patientName, fullNameUr: x.patientNameUr }, i18n.language)}</span><span className="cell-sub num">{x.patientNo} · {x.branchName}</span></span> },
    { key: 'tests', header: t('reports.released'), render: (x) => <span className="num">{x.releasedCount}/{x.activeTestCount}</span> },
    { key: 'report', header: t('reports.report'), render: (x) => (x.reportNo
      ? <span><span className="num cell-strong">{x.reportNo}</span> <Badge tone="neutral">{t('reports.versionShort', { n: x.version })}</Badge><span className="cell-sub">{x.issuedAtUtc ? formatDateTime(x.issuedAtUtc, i18n.language) : ''}</span></span>
      : <span className="field-hint">{t('reports.notIssued')}</span>) },
    { key: 'state', header: t('orders.status'), render: (x) => (
      <span className="badge-list">
        {x.status !== null && <Badge tone={reportTone[x.status]} dot>{t(`reportStatus.${x.status}`)}</Badge>}
        {x.isOutdated && <Badge tone="danger">{t('reports.outdated')}</Badge>}
        {x.status === null && (x.releasedCount < x.activeTestCount ? <Badge tone="warning">{t('reportStatus.0')}</Badge> : <Badge tone="success">{t('reports.readyFinal')}</Badge>)}
        {x.printCount > 0 && <Badge>{t('reports.printed', { count: x.printCount })}</Badge>}
      </span>) },
  ]

  return (
    <div className="page">
      <div className="page-head"><div><h2>{t('reports.title')}</h2><p>{t('reports.subtitle')}</p></div></div>
      <div style={{ marginBottom: 16 }}>
        <Tabs value={tab} onChange={(k) => { setTab(k); setPage(1) }} tabs={[{ key: 'ready', label: t('reports.tabReady') }, { key: 'issued', label: t('reports.tabIssued') }]} />
      </div>
      <section className="card">
        <div className="card-toolbar"><SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('reports.search')} /></div>
        <DataTable columns={columns} rows={data?.items} rowKey={(x) => x.orderId} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage}
          onRowClick={(x) => navigate(`/reports/order/${x.orderId}`)}
          empty={search ? undefined : <EmptyState icon={<FileText size={22} />} title={tab === 'ready' ? t('reports.noReadyTitle') : t('reports.noIssuedTitle')} text={tab === 'ready' ? t('reports.noReadyText') : t('reports.noIssuedText')} />} />
      </section>
    </div>
  )
}
