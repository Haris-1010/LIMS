import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { ShieldCheck } from 'lucide-react'
import { api } from '../../lib/api'
import { formatDateTime } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { SearchInput, Tabs } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { patientName } from '../patients/patientsApi'
import type { Paged } from '../admin/adminApi'
import { resultKeys, type QueueItem } from './resultsApi'
import './results.css'

const PAGE_SIZE = 25
type Stage = 'verification' | 'approval'

/** Results waiting for the second pair of eyes: technical verification, then approval and release. */
export function VerificationPage() {
  const { t, i18n } = useTranslation()
  const { can } = useSession()
  const navigate = useNavigate()
  const stages: Stage[] = [...(can('results.verify') ? (['verification'] as const) : []), ...(can('results.approve') ? (['approval'] as const) : [])]
  const [stage, setStage] = useState<Stage>(stages[0] ?? 'verification')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const params = { stage, search, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: resultKeys.queue(params), queryFn: () => api<Paged<QueueItem>>('/api/results/queue', { query: params }), placeholderData: keepPreviousData, refetchInterval: 30_000 })

  const columns: Column<QueueItem>[] = [
    { key: 'barcode', header: t('samples.barcode'), width: 160, render: (x) => <span className="num cell-strong">{x.barcode}</span> },
    { key: 'patient', header: t('orders.patientCol'), render: (x) => <span><span dir="auto">{patientName({ fullName: x.patientName, fullNameUr: x.patientNameUr }, i18n.language)}</span><span className="cell-sub num">{x.patientNo} · {x.orderNo}</span></span> },
    { key: 'test', header: t('results.test'), render: (x) => <span><span dir="auto">{i18n.language === 'ur' && x.testNameUr ? x.testNameUr : x.testNameEn}</span><span className="cell-sub num">{x.testCode}</span></span> },
    { key: 'flags', header: t('results.flags'), render: (x) => (
      <span className="badge-list">
        {x.criticalCount > 0 && <Badge tone="danger">{t('results.criticalCount', { count: x.criticalCount })}</Badge>}
        {x.abnormalCount > 0 && <Badge tone="warning">{t('results.abnormalCount', { count: x.abnormalCount })}</Badge>}
        {x.hasOpenAlert && <Badge tone="danger">{t('results.alertOpen')}</Badge>}
        {x.isAmendment && <Badge tone="warning">{t('results.version', { n: x.version })}</Badge>}
        {x.priority === 1 && <Badge tone="danger">{t('orders.urgent')}</Badge>}
      </span>) },
    { key: 'by', header: t('results.enteredBy'), render: (x) => <span>{x.enteredByName ?? '—'}{x.isOwnEntry && <> <Badge>{t('results.you')}</Badge></>}{x.submittedAtUtc && <span className="cell-sub">{formatDateTime(x.submittedAtUtc, i18n.language)}</span>}</span> },
    ...(stage === 'approval' ? [{ key: 'verifiedBy', header: t('results.verifiedBy'), render: (x: QueueItem) => x.verifiedByName ?? '—' }] : []),
  ]

  return (
    <div className="page">
      <div className="page-head"><div><h2>{t('results.verificationTitle')}</h2><p>{t('results.verificationSubtitle')}</p></div></div>
      {stages.length > 1 && <div style={{ marginBottom: 16 }}><Tabs value={stage} onChange={(s) => { setStage(s); setPage(1) }} tabs={stages.map((s) => ({ key: s, label: t(`results.stage.${s}`) }))} /></div>}
      <section className="card">
        <div className="card-toolbar"><SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('results.searchPending')} /></div>
        <DataTable columns={columns} rows={data?.items} rowKey={(x) => x.resultId} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage}
          onRowClick={(x) => navigate(`/results/item/${x.orderItemId}`)}
          empty={search ? undefined : <EmptyState icon={<ShieldCheck size={22} />} title={t('results.verificationEmptyTitle')} text={t('results.verificationEmptyText')} />} />
      </section>
    </div>
  )
}
