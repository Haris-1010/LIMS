import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ScrollText } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { SearchInput, SelectField } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import type { Paged } from './adminApi'

interface AuditItem {
  id: number; atUtc: string; action: string; entityType: string; entityId: string; userId: number | null; userName: string | null
  branchId: number | null; branchName: string | null; reason: string | null; ipAddress: string | null; changedFields: number
}
interface AuditChange { field: string; old: string | null; new: string | null }
interface AuditDetail extends Omit<AuditItem, 'changedFields'> { correlationId: string | null; changes: AuditChange[]; rawChanges: string | null }
interface AuditFilters { entityTypes: string[]; actions: string[] }

const PAGE_SIZE = 25
const ROW_ACTIONS = ['Insert', 'Update', 'Delete']

const tone = (action: string) => (action === 'Insert' ? 'success' : action === 'Delete' ? 'danger' : action === 'Update' ? 'primary' : 'accent')

/** Read-only audit log of the organisation: who did what, when, with the old and new values. Masked fields stay masked. */
export function AuditPage() {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const [kind, setKind] = useState('')
  const [entityType, setEntityType] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const [openId, setOpenId] = useState<number | null>(null)
  const params = { search, kind: kind || undefined, entityType: entityType || undefined, from: from || undefined, to: to || undefined, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: ['audit', params], queryFn: () => api<Paged<AuditItem>>('/api/audit', { query: params }), placeholderData: keepPreviousData })
  const filters = useQuery({ queryKey: ['audit', 'filters'], queryFn: () => api<AuditFilters>('/api/audit/filters'), staleTime: 60_000 })
  const reset = <V,>(set: (v: V) => void) => (v: V) => { set(v); setPage(1) }

  const columns: Column<AuditItem>[] = [
    { key: 'at', header: t('audit.when'), width: 175, render: (a) => <span className="num">{formatDateTime(a.atUtc, i18n.language)}</span> },
    { key: 'user', header: t('audit.who'), render: (a) => <span>{a.userName ?? t('audit.system')}{a.branchName && <span className="cell-sub">{a.branchName}</span>}</span> },
    { key: 'action', header: t('audit.action'), render: (a) => <Badge tone={tone(a.action)}>{ROW_ACTIONS.includes(a.action) ? t(`audit.kind.${a.action}`) : a.action}</Badge> },
    { key: 'entity', header: t('audit.entity'), render: (a) => <span><span className="cell-strong">{a.entityType}</span><span className="cell-sub num" dir="ltr">{a.entityId}</span></span> },
    { key: 'detail', header: t('audit.details'), render: (a) => <span>{a.reason ? <span dir="auto">{a.reason}</span> : null}{a.changedFields > 0 && <span className="cell-sub">{t('audit.fieldsChanged', { count: a.changedFields })}</span>}</span> },
  ]

  return (
    <div className="page">
      <div className="page-head"><div><h2>{t('audit.title')}</h2><p>{t('audit.subtitle')}</p></div></div>
      <section className="card">
        <div className="card-toolbar" style={{ flexWrap: 'wrap', gap: 8 }}>
          <SearchInput value={search} onChange={reset(setSearch)} placeholder={t('audit.search')} />
          <SelectField label={t('audit.kindLabel')} value={kind} onChange={(e) => reset(setKind)(e.target.value)}>
            <option value="">{t('common.all')}</option>
            <option value="Business">{t('audit.kind.Business')}</option>
            {ROW_ACTIONS.map((k) => <option key={k} value={k}>{t(`audit.kind.${k}`)}</option>)}
          </SelectField>
          <SelectField label={t('audit.entity')} value={entityType} onChange={(e) => reset(setEntityType)(e.target.value)}>
            <option value="">{t('common.all')}</option>
            {filters.data?.entityTypes.map((x) => <option key={x} value={x}>{x}</option>)}
          </SelectField>
          <label className="field-inline"><span className="field-hint">{t('common.from')}</span><input className="input" type="date" dir="ltr" value={from} max={to || undefined} onChange={(e) => reset(setFrom)(e.target.value)} /></label>
          <label className="field-inline"><span className="field-hint">{t('common.to')}</span><input className="input" type="date" dir="ltr" value={to} min={from || undefined} onChange={(e) => reset(setTo)(e.target.value)} /></label>
        </div>
        <DataTable columns={columns} rows={data?.items} rowKey={(a) => a.id} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage} onRowClick={(a) => setOpenId(a.id)}
          empty={<EmptyState icon={<ScrollText size={22} />} title={t('audit.emptyTitle')} text={t('audit.emptyText')} />} />
      </section>
      {openId !== null && <AuditDetailModal id={openId} onClose={() => setOpenId(null)} />}
    </div>
  )
}

function AuditDetailModal({ id, onClose }: { id: number; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const { data, isLoading, error } = useQuery({ queryKey: ['audit', 'detail', id], queryFn: () => api<AuditDetail>(`/api/audit/${id}`) })
  return (
    <Modal open title={data ? `${data.entityType} · ${data.entityId}` : t('audit.title')} subtitle={data ? formatDateTime(data.atUtc, i18n.language) : undefined} onClose={onClose} size="lg"
      footer={<Button variant="ghost" onClick={onClose}>{t('common.close')}</Button>}>
      {isLoading ? <Skeleton height={160} /> : error || !data ? <p className="field-hint">{errorMessage(t, error)}</p> : (
        <div className="form-stack">
          <dl className="kv">
            <div><dt>{t('audit.who')}</dt><dd>{data.userName ?? t('audit.system')}{data.branchName ? ` · ${data.branchName}` : ''}</dd></div>
            <div><dt>{t('audit.action')}</dt><dd>{data.action}</dd></div>
            {data.reason && <div><dt>{t('audit.reason')}</dt><dd dir="auto">{data.reason}</dd></div>}
            {data.ipAddress && <div><dt>{t('audit.ip')}</dt><dd className="num" dir="ltr">{data.ipAddress}</dd></div>}
            {data.correlationId && <div><dt>{t('audit.correlation')}</dt><dd className="num" dir="ltr">{data.correlationId}</dd></div>}
          </dl>
          {data.changes.length === 0 ? <p className="field-hint">{t('audit.noChanges')}</p> : (
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>{t('audit.field')}</th><th>{t('audit.oldValue')}</th><th>{t('audit.newValue')}</th></tr></thead>
                <tbody>
                  {data.changes.map((c) => (
                    <tr key={c.field}>
                      <td className="cell-strong">{c.field}</td>
                      <td dir="auto">{c.old ?? <span className="field-hint">—</span>}</td>
                      <td dir="auto">{c.new ?? <span className="field-hint">—</span>}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <p className="field-hint">{t('audit.maskedNote')}</p>
        </div>
      )}
    </Modal>
  )
}
