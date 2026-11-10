import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { TestTubes } from 'lucide-react'
import { api } from '../../lib/api'
import { formatDateTime } from '../../lib/format'
import { Badge } from '../../components/ui/Badge'
import { SearchInput } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { patientName } from '../patients/patientsApi'
import type { Paged } from '../admin/adminApi'
import { SampleDetailModal, SampleStatusBadge } from './SampleParts'
import { SAMPLE_STATUSES, pick, sampleKeys, type SampleListItem } from './samplesApi'
import './samples.css'

const PAGE_SIZE = 25

/** Search and follow any sample the user may see: by barcode, patient, order number, status or date. */
export function TrackingPage() {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const [viewing, setViewing] = useState<number | null>(null)
  const params = { search, status: status || undefined, from: from || undefined, to: to || undefined, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: sampleKeys.list(params), queryFn: () => api<Paged<SampleListItem>>('/api/samples', { query: params }), placeholderData: keepPreviousData })

  const columns: Column<SampleListItem>[] = [
    { key: 'barcode', header: t('samples.barcode'), width: 170, render: (s) => <span className="num cell-strong">{s.barcode}</span> },
    { key: 'patient', header: t('orders.patientCol'), render: (s) => <span><span dir="auto">{patientName({ fullName: s.patientName, fullNameUr: s.patientNameUr }, i18n.language)}</span><span className="cell-sub num">{s.patientNo} · {s.orderNo}</span></span> },
    { key: 'type', header: t('samples.sampleType'), render: (s) => <span>{pick(s.sampleTypeNameEn, s.sampleTypeNameUr, i18n.language)}<span className="cell-sub num">{s.testCodes.join(', ')}</span></span> },
    { key: 'status', header: t('orders.status'), render: (s) => <span><SampleStatusBadge status={s.status} />{s.priority === 1 && <> <Badge tone="danger">{t('orders.urgent')}</Badge></>}{s.isRecollection && <> <Badge tone="warning">{t('samples.recollectionBadge')}</Badge></>}</span> },
    { key: 'where', header: t('samples.currentBranch'), render: (s) => <span>{s.currentBranch}{s.destinationBranch && <span className="cell-sub">→ {s.destinationBranch}</span>}</span> },
    { key: 'at', header: t('samples.collectedAt'), render: (s) => (s.collectedAtUtc ? formatDateTime(s.collectedAtUtc, i18n.language) : '—') },
  ]
  const filtered = !!(search || status || from || to)

  return (
    <div className="page">
      <div className="page-head"><div><h2>{t('samples.trackingTitle')}</h2><p>{t('samples.trackingSubtitle')}</p></div></div>
      <section className="card">
        <div className="card-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('samples.trackingSearch')} />
          <select className="select select-sm" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }} aria-label={t('orders.status')}>
            <option value="">{t('samples.anyStatus')}</option>
            {SAMPLE_STATUSES.map((s) => <option key={s} value={s}>{t(`sampleStatus.${s}`)}</option>)}
          </select>
          <input type="date" className="input select-sm" value={from} max={to || undefined} onChange={(e) => { setFrom(e.target.value); setPage(1) }} aria-label={t('samples.from')} />
          <input type="date" className="input select-sm" value={to} min={from || undefined} onChange={(e) => { setTo(e.target.value); setPage(1) }} aria-label={t('samples.to')} />
        </div>
        <DataTable columns={columns} rows={data?.items} rowKey={(s) => s.id} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage} onRowClick={(s) => setViewing(s.id)}
          empty={filtered ? undefined : <EmptyState icon={<TestTubes size={22} />} title={t('samples.trackingEmptyTitle')} text={t('samples.trackingEmptyText')} />} />
      </section>
      {viewing !== null && <SampleDetailModal id={viewing} onClose={() => setViewing(null)} />}
    </div>
  )
}
