import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { FlaskConical } from 'lucide-react'
import { api } from '../../lib/api'
import { formatDateTime } from '../../lib/format'
import { Badge } from '../../components/ui/Badge'
import { SearchInput } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { useDepartmentOptions } from '../common/useDepartmentOptions'
import { patientName } from '../patients/patientsApi'
import type { Paged } from '../admin/adminApi'
import { ResultStatusBadge } from './ResultParts'
import { resultKeys, type PendingItem } from './resultsApi'
import './results.css'

const PAGE_SIZE = 25

/** Tests whose sample has been received and that still need a result entered. Scan a barcode to jump to a sample. */
export function ResultQueuePage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [dept, setDept] = useState('')
  const [urgent, setUrgent] = useState(false)
  const [overdue, setOverdue] = useState(false)
  const [page, setPage] = useState(1)
  const departments = useDepartmentOptions()
  const params = { search, departmentId: dept || undefined, urgentOnly: urgent || undefined, overdueOnly: overdue || undefined, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: resultKeys.pending(params), queryFn: () => api<Paged<PendingItem>>('/api/results/pending', { query: params }), placeholderData: keepPreviousData, refetchInterval: 30_000 })

  const columns: Column<PendingItem>[] = [
    { key: 'barcode', header: t('samples.barcode'), width: 160, render: (x) => <span className="num cell-strong">{x.barcode}</span> },
    { key: 'patient', header: t('orders.patientCol'), render: (x) => <span><span dir="auto">{patientName({ fullName: x.patientName, fullNameUr: x.patientNameUr }, i18n.language)}</span><span className="cell-sub num">{x.patientNo} · {x.orderNo}</span></span> },
    { key: 'test', header: t('results.test'), render: (x) => <span><span dir="auto">{i18n.language === 'ur' && x.testNameUr ? x.testNameUr : x.testNameEn}</span><span className="cell-sub num">{x.testCode} · {i18n.language === 'ur' && x.departmentNameUr ? x.departmentNameUr : x.departmentNameEn}</span></span> },
    { key: 'state', header: t('orders.status'), render: (x) => <span>{x.resultStatus === null ? <Badge>{t('results.notStarted')}</Badge> : <ResultStatusBadge status={x.resultStatus} />}{x.priority === 1 && <> <Badge tone="danger">{t('orders.urgent')}</Badge></>}{x.onWorklist && <span className="cell-sub num">{x.onWorklist}</span>}</span> },
    { key: 'due', header: t('results.due'), render: (x) => (x.dueAtUtc ? <span className={x.isOverdue ? 'overdue' : undefined}>{formatDateTime(x.dueAtUtc, i18n.language)}{x.isOverdue && <> · {t('results.overdue')}</>}</span> : '—') },
  ]
  const filtered = !!(search || dept || urgent || overdue)

  return (
    <div className="page">
      <div className="page-head"><div><h2>{t('results.entryTitle')}</h2><p>{t('results.entrySubtitle')}</p></div></div>
      <section className="card">
        <div className="card-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('results.searchPending')} autoFocus />
          <select className="select select-sm" value={dept} onChange={(e) => { setDept(e.target.value); setPage(1) }} aria-label={t('results.department')}>
            <option value="">{t('results.anyDepartment')}</option>
            {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
          <label className="checkbox"><input type="checkbox" checked={urgent} onChange={(e) => { setUrgent(e.target.checked); setPage(1) }} />{t('results.urgentOnly')}</label>
          <label className="checkbox"><input type="checkbox" checked={overdue} onChange={(e) => { setOverdue(e.target.checked); setPage(1) }} />{t('results.overdueOnly')}</label>
        </div>
        <DataTable columns={columns} rows={data?.items} rowKey={(x) => x.orderItemId} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage}
          onRowClick={(x) => navigate(`/results/item/${x.orderItemId}`)}
          empty={filtered ? undefined : <EmptyState icon={<FlaskConical size={22} />} title={t('results.queueEmptyTitle')} text={t('results.queueEmptyText')} />} />
      </section>
    </div>
  )
}
