import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { CheckCircle2, PackageCheck, XCircle } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Field } from '../../components/ui/Field'
import { patientName } from '../patients/patientsApi'
import type { Paged } from '../admin/adminApi'
import { SampleDetailModal, SampleStatusBadge } from './SampleParts'
import { useRefreshSamples } from './sampleHooks'
import { pick, sampleKeys, type SampleDetail, type SampleListItem } from './samplesApi'
import './samples.css'

interface Outcome { key: number; ok: boolean; barcode: string; text: string }

/** Lab bench: scan a barcode to receive it. Every scan shows a clear result so a mistake is never silent. */
export function ReceivingPage() {
  const { t, i18n } = useTranslation()
  const { user } = useSession()
  const refresh = useRefreshSamples()
  const inputRef = useRef<HTMLInputElement>(null)
  const [code, setCode] = useState('')
  const [outcomes, setOutcomes] = useState<Outcome[]>([])
  const [viewing, setViewing] = useState<number | null>(null)
  const counter = useRef(0)

  useEffect(() => { inputRef.current?.focus() }, [])
  const branchId = user?.activeBranchId ?? undefined

  // Samples that should reach this bench: collected here, or in transit to this branch.
  const waitingQuery = useQuery({
    queryKey: sampleKeys.list({ awaiting: true, branchId }),
    queryFn: () => api<Paged<SampleListItem>>('/api/samples', { query: { awaitingReceipt: true, pageSize: 100 } }),
    enabled: !!branchId,
  })
  const waiting = waitingQuery.data?.items ?? []

  const receive = useMutation({
    mutationFn: async (value: string) => {
      const found = await api<SampleDetail>(`/api/samples/by-barcode/${encodeURIComponent(value)}`)
      return api<SampleDetail>(`/api/samples/${found.sample.id}/receive`, { method: 'POST' })
    },
    onSuccess: async (d) => {
      setOutcomes((o) => [{ key: ++counter.current, ok: true, barcode: d.sample.barcode, text: patientName({ fullName: d.sample.patientName, fullNameUr: d.sample.patientNameUr }, i18n.language) }, ...o].slice(0, 12))
      await refresh()
    },
    onError: (e, value) => setOutcomes((o) => [{ key: ++counter.current, ok: false, barcode: value.toUpperCase(), text: errorMessage(t, e) }, ...o].slice(0, 12)),
    onSettled: () => { setCode(''); inputRef.current?.focus() },
  })

  const receiveRow = useMutation({
    mutationFn: (s: SampleListItem) => api<SampleDetail>(`/api/samples/${s.id}/receive`, { method: 'POST' }),
    onSuccess: async (d) => { setOutcomes((o) => [{ key: ++counter.current, ok: true, barcode: d.sample.barcode, text: patientName({ fullName: d.sample.patientName, fullNameUr: d.sample.patientNameUr }, i18n.language) }, ...o].slice(0, 12)); await refresh() },
    onError: (e, s) => setOutcomes((o) => [{ key: ++counter.current, ok: false, barcode: s.barcode, text: errorMessage(t, e) }, ...o].slice(0, 12)),
  })

  const columns: Column<SampleListItem>[] = [
    { key: 'barcode', header: t('samples.barcode'), render: (s) => <span className="num cell-strong">{s.barcode}</span> },
    { key: 'patient', header: t('orders.patientCol'), render: (s) => <span><span dir="auto">{patientName({ fullName: s.patientName, fullNameUr: s.patientNameUr }, i18n.language)}</span><span className="cell-sub num">{s.patientNo}</span></span> },
    { key: 'type', header: t('samples.sampleType'), render: (s) => <span>{pick(s.sampleTypeNameEn, s.sampleTypeNameUr, i18n.language)}<span className="cell-sub num">{s.testCodes.join(', ')}</span></span> },
    { key: 'from', header: t('samples.originBranch'), render: (s) => s.originBranch },
    { key: 'status', header: t('orders.status'), render: (s) => <span><SampleStatusBadge status={s.status} />{s.priority === 1 && <> <Badge tone="danger">{t('orders.urgent')}</Badge></>}</span> },
    { key: 'at', header: t('samples.collectedAt'), render: (s) => (s.collectedAtUtc ? formatDateTime(s.collectedAtUtc, i18n.language) : '—') },
    { key: 'act', header: '', align: 'end', render: (s) => <Button size="sm" variant="primary" loading={receiveRow.isPending && receiveRow.variables?.id === s.id} onClick={(e) => { e.stopPropagation(); receiveRow.mutate(s) }}>{t('samples.receiveAction')}</Button> },
  ]

  return (
    <div className="page">
      <div className="page-head"><div><h2>{t('samples.receivingTitle')}</h2><p>{t('samples.receivingSubtitle')}</p></div></div>

      <section className="card" style={{ padding: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
        <div className="scan-box">
          <Field ref={inputRef} className="scan-input" label={t('samples.scanToReceive')} value={code} placeholder={t('samples.scanPlaceholder')} autoComplete="off" disabled={receive.isPending}
            onChange={(e) => setCode(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter' && code.trim()) { e.preventDefault(); receive.mutate(code.trim()) } }} />
        </div>
        <ul className="ledger" aria-live="polite" style={{ marginTop: 'var(--space-3)' }}>
          {outcomes.map((o) => (
            <li key={o.key} style={{ gridTemplateColumns: '24px minmax(0,1fr)' }}>
              {o.ok ? <CheckCircle2 size={18} color="var(--success)" aria-label={t('samples.received')} /> : <XCircle size={18} color="var(--danger)" aria-label={t('samples.actionFailed')} />}
              <span><strong className="num">{o.barcode}</strong> <span dir="auto" className={o.ok ? undefined : 'text-warning'}>{o.text}</span></span>
            </li>
          ))}
        </ul>
      </section>

      <section className="card">
        <div className="card-header"><h3>{t('samples.awaitingHere')}</h3><Badge tone="primary">{waitingQuery.data?.total ?? 0}</Badge></div>
        <DataTable columns={columns} rows={waiting} rowKey={(s) => s.id} loading={waitingQuery.isLoading} onRowClick={(s) => setViewing(s.id)}
          empty={<EmptyState icon={<PackageCheck size={22} />} title={t('samples.awaitingEmptyTitle')} text={t('samples.awaitingEmptyText')} />} />
      </section>
      {viewing !== null && <SampleDetailModal id={viewing} onClose={() => setViewing(null)} />}
    </div>
  )
}
