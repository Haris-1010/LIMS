import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { AlertCircle, ArrowRight, CheckCheck, CheckCircle2, ShieldAlert, ShieldCheck } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { useSession } from '../../app/session'
import { Button } from '../../components/ui/Button'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { useToast } from '../../components/ui/Toast'
import { catalogKeys, formatAgeBand, formatLimits, type BulkApproveFailure, type BulkApproveResult, type PendingRange } from './catalogApi'
import './catalog.css'

/** The pathologist's queue: every draft range in the organisation, oldest first. */
export function RangeApprovalPage() {
  const { t, i18n } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const { can } = useSession()
  const canApprove = can('catalog.clinical')
  const [approving, setApproving] = useState<PendingRange | null>(null)
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const [confirmAll, setConfirmAll] = useState(false)
  const [bulkFailures, setBulkFailures] = useState<{ name: string; message: string }[]>([])

  const { data, isLoading } = useQuery({ queryKey: catalogKeys.pending, queryFn: () => api<PendingRange[]>('/api/catalog/ranges/pending') })

  const approve = async () => {
    if (!approving) return
    setBusy(true)
    setFailure(null)
    try {
      await api(`/api/catalog/ranges/${approving.range.id}/approve`, { method: 'POST' })
      toast.show({ tone: 'success', title: t('catalog.approved') })
      await qc.invalidateQueries({ queryKey: ['catalog'] })
    } catch (e) {
      setFailure(errorMessage(t, e))
    } finally {
      setBusy(false)
      setApproving(null)
    }
  }

  // Only drafts with a source can be approved; the rest stay in the queue.
  const approvable = (data ?? []).filter((p) => !!p.range.source)

  const approveAll = async () => {
    const rows = approvable
    setBusy(true)
    setFailure(null)
    setBulkFailures([])
    try {
      let approved = 0
      const failed: BulkApproveFailure[] = []
      // The API takes at most 500 ids per call.
      for (let i = 0; i < rows.length; i += 500) {
        const res = await api<BulkApproveResult>('/api/catalog/ranges/approve-many', {
          method: 'POST',
          body: { ids: rows.slice(i, i + 500).map((p) => p.range.id) },
        })
        approved += res.approved
        failed.push(...res.failed)
      }
      if (approved > 0) toast.show({ tone: 'success', title: t('catalog.approvedMany', { count: approved }) })
      const byId = new Map(rows.map((p) => [p.range.id, p]))
      setBulkFailures(failed.map((f) => {
        const p = byId.get(f.id)
        return {
          name: p ? `${p.testCode} · ${p.parameterName} (${t(`rangeSex.${p.range.sex}`)})` : `#${f.id}`,
          message: t(`errors.${f.code}`, { defaultValue: f.code }),
        }
      }))
      await qc.invalidateQueries({ queryKey: ['catalog'] })
    } catch (e) {
      setFailure(errorMessage(t, e))
    } finally {
      setBusy(false)
      setConfirmAll(false)
    }
  }

  const unit = (p: PendingRange) => (p.resultType === 1 && p.unitCode && !p.range.displayText ? ` ${p.unitCode}` : '')

  const columns: Column<PendingRange>[] = [
    {
      key: 'test',
      header: t('catalog.test'),
      render: (p) => (
        <span>
          <span className="cell-strong" dir="auto">{p.parameterName}</span>
          <span className="cell-sub"><span className="num">{p.testCode}</span> · {p.testName}</span>
        </span>
      ),
    },
    { key: 'who', header: `${t('catalog.sex')} / ${t('catalog.ageBand')}`, render: (p) => <span>{t(`rangeSex.${p.range.sex}`)}<span className="cell-sub">{formatAgeBand(t, p.range.ageFromDays, p.range.ageToDays)}</span></span> },
    {
      key: 'limits',
      header: t('catalog.limits'),
      render: (p) => (
        <span>
          <span className="num cell-strong">{formatLimits(p.range)}{unit(p)}</span>
          {(p.range.criticalLow !== null || p.range.criticalHigh !== null) && (
            <span className="cell-sub num">{t('catalog.criticalLow')}: {p.range.criticalLow ?? '—'} · {t('catalog.criticalHigh')}: {p.range.criticalHigh ?? '—'}</span>
          )}
        </span>
      ),
    },
    {
      key: 'replaces',
      header: t('catalog.replaces'),
      render: (p) => (p.replaces ? <span className="num replaced-limits">{formatLimits(p.replaces)}{unit(p)}</span> : '—'),
    },
    {
      key: 'source',
      header: t('catalog.source'),
      render: (p) => (p.range.source ? <span>{p.range.source}</span> : <span className="text-warning"><ShieldAlert size={13} aria-hidden /> {t('errors.range_source_required')}</span>),
    },
    { key: 'drafted', header: t('catalog.status'), render: (p) => <span>{p.range.createdByName ?? '—'}<span className="cell-sub">{formatDateTime(p.range.createdAtUtc, i18n.language)}</span></span> },
    {
      key: 'actions',
      header: '',
      align: 'end',
      render: (p) => (
        <span className="row-actions">
          <Link to={`/catalog/tests/${p.testId}`} className="btn btn-ghost btn-sm">{t('catalog.openTest')} <ArrowRight size={14} className="flip-rtl" /></Link>
          {canApprove && <Button size="sm" variant="primary" icon={<CheckCircle2 size={14} />} disabled={!p.range.source} onClick={() => setApproving(p)}>{t('catalog.approve')}</Button>}
        </span>
      ),
    },
  ]

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <h2>{t('catalog.queueTitle')}</h2>
          <p>{t('catalog.queueSubtitle')}</p>
        </div>
        {canApprove && approvable.length > 0 && (
          <Button variant="primary" icon={<CheckCheck size={16} />} onClick={() => setConfirmAll(true)}>
            {t('catalog.approveAll', { count: approvable.length })}
          </Button>
        )}
      </div>
      {failure && (
        <div className="banner tone-danger" role="alert" style={{ marginBottom: 16 }}>
          <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
          <span>{failure}</span>
        </div>
      )}
      {bulkFailures.length > 0 && (
        <div className="banner tone-warning" role="alert" style={{ marginBottom: 16, alignItems: 'flex-start' }}>
          <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
          <div>
            <strong>{t('catalog.approveManyFailed', { count: bulkFailures.length })}</strong>
            <ul style={{ margin: '4px 0 0', paddingInlineStart: 18 }}>
              {bulkFailures.map((f, i) => <li key={i}><span dir="auto">{f.name}</span>: {f.message}</li>)}
            </ul>
          </div>
        </div>
      )}
      <section className="card">
        <DataTable
          columns={columns}
          rows={data}
          rowKey={(p) => p.range.id}
          loading={isLoading}
          empty={<EmptyState icon={<ShieldCheck size={22} />} title={t('catalog.queueEmpty')} text={t('catalog.queueEmptyText')} />}
        />
      </section>
      <ConfirmDialog
        open={!!approving}
        icon={<CheckCircle2 size={20} />}
        title={t('catalog.approveTitle')}
        text={t('catalog.approveText')}
        confirmLabel={t('catalog.approve')}
        loading={busy}
        onCancel={() => setApproving(null)}
        onConfirm={() => void approve()}
      />
      <ConfirmDialog
        open={confirmAll}
        icon={<CheckCheck size={20} />}
        title={t('catalog.approveAllTitle', { count: approvable.length })}
        text={t('catalog.approveAllText', { count: approvable.length })}
        confirmLabel={t('catalog.approveAll', { count: approvable.length })}
        loading={busy}
        onCancel={() => setConfirmAll(false)}
        onConfirm={() => void approveAll()}
      />
    </div>
  )
}
