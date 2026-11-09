import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, CheckCircle2, Pencil, Plus, Repeat, ShieldAlert, Trash2, Archive } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import { catalogKeys, formatAgeBand, formatLimits, localName, type ParameterDto, type RangeDto } from './catalogApi'
import { RangeFormModal } from './RangeFormModal'

const statusTone = { 0: 'info', 1: 'success', 2: 'neutral' } as const

/** All ranges of one parameter with the draft → approved → retired workflow. */
export function RangesModal({ parameter, testId, onClose }: { parameter: ParameterDto; testId: number; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const { can } = useSession()
  const canDraft = can('catalog.manage') || can('catalog.clinical')
  const canApprove = can('catalog.clinical')
  const [form, setForm] = useState<{ range: RangeDto | null; replaces: RangeDto | null } | null>(null)
  const [confirm, setConfirm] = useState<{ kind: 'approve' | 'retire' | 'delete'; range: RangeDto } | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const { data, isLoading } = useQuery({
    queryKey: catalogKeys.ranges(parameter.id),
    queryFn: () => api<RangeDto[]>(`/api/catalog/parameters/${parameter.id}/ranges`),
  })

  const act = async () => {
    if (!confirm) return
    setBusy(true)
    setFailure(null)
    const { kind, range } = confirm
    try {
      if (kind === 'approve') await api(`/api/catalog/ranges/${range.id}/approve`, { method: 'POST' })
      if (kind === 'retire') await api(`/api/catalog/ranges/${range.id}/retire`, { method: 'POST', body: { reason: null } })
      if (kind === 'delete') await api(`/api/catalog/ranges/${range.id}`, { method: 'DELETE' })
      toast.show({ tone: 'success', title: kind === 'approve' ? t('catalog.approved') : kind === 'retire' ? t('catalog.retired') : t('common.saved') })
      await qc.invalidateQueries({ queryKey: ['catalog'] })
    } catch (e) {
      setFailure(errorMessage(t, e))
    } finally {
      setBusy(false)
      setConfirm(null)
    }
  }

  const unit = parameter.resultType === 1 && parameter.unitCode ? ` ${parameter.unitCode}` : ''
  const ordered = [...(data ?? [])].sort((a, b) => (a.status === 2 ? 1 : 0) - (b.status === 2 ? 1 : 0))

  return (
    <>
      <Modal
        open
        size="xl"
        onClose={onClose}
        title={t('catalog.rangesTitle', { name: localName(parameter, i18n.language) })}
        subtitle={t('catalog.rangesIntro')}
        footer={
          <>
            <span className="spacer" />
            <Button onClick={onClose}>{t('common.close')}</Button>
            {canDraft && <Button variant="primary" icon={<Plus size={15} />} onClick={() => setForm({ range: null, replaces: null })}>{t('catalog.addRange')}</Button>}
          </>
        }
      >
        <div className="banner tone-warning" style={{ marginBottom: 16 }}>
          <ShieldAlert size={16} aria-hidden style={{ flexShrink: 0 }} />
          <span>{t('catalog.clinicalWarning')}</span>
        </div>
        {failure && (
          <div className="banner tone-danger" role="alert" style={{ marginBottom: 16 }}>
            <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
            <span>{failure}</span>
          </div>
        )}

        {isLoading ? (
          <Skeleton height={160} />
        ) : ordered.length === 0 ? (
          <p className="field-hint">{t('catalog.noRanges')}</p>
        ) : (
          <div className="table-wrap range-table">
            <table className="table">
              <thead>
                <tr>
                  <th>{t('catalog.sex')}</th>
                  <th>{t('catalog.ageBand')}</th>
                  <th>{t('catalog.limits')}</th>
                  <th>{t('catalog.critical')}</th>
                  <th>{t('catalog.status')}</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {ordered.map((r) => (
                  <tr key={r.id} className={r.status === 2 ? 'is-retired' : undefined}>
                    <td>{t(`rangeSex.${r.sex}`)}</td>
                    <td className="nowrap">{formatAgeBand(t, r.ageFromDays, r.ageToDays)}</td>
                    <td><span className="num cell-strong">{formatLimits(r)}{r.displayText ? '' : unit}</span>{r.source && <span className="cell-sub">{r.source}</span>}</td>
                    <td className="num">{r.criticalLow ?? '—'} / {r.criticalHigh ?? '—'}</td>
                    <td>
                      <Badge tone={statusTone[r.status]} dot>{t(`rangeStatus.${r.status}`)}</Badge>
                      <span className="cell-sub">
                        {r.status === 1 && r.approvedAtUtc
                          ? t('catalog.approvedBy', { name: r.approvedByName ?? '—', date: formatDateTime(r.approvedAtUtc, i18n.language) })
                          : r.status === 0 ? t('catalog.draftedBy', { name: r.createdByName ?? '—' }) : r.retiredAtUtc && formatDateTime(r.retiredAtUtc, i18n.language)}
                      </span>
                      {r.replacesRangeId && <span className="cell-sub">{t('catalog.replacesRange', { id: r.replacesRangeId })}</span>}
                    </td>
                    <td>
                      <span className="row-actions">
                        {r.status === 0 && canDraft && <button type="button" className="icon-btn" aria-label={t('common.edit')} onClick={() => setForm({ range: r, replaces: null })}><Pencil size={15} /></button>}
                        {r.status === 0 && canDraft && <button type="button" className="icon-btn" aria-label={t('catalog.deleteDraft')} onClick={() => setConfirm({ kind: 'delete', range: r })}><Trash2 size={15} /></button>}
                        {r.status === 0 && canApprove && (
                          <Button size="sm" variant="primary" icon={<CheckCircle2 size={14} />} disabled={!r.source} title={!r.source ? t('errors.range_source_required') : undefined} onClick={() => setConfirm({ kind: 'approve', range: r })}>
                            {t('catalog.approve')}
                          </Button>
                        )}
                        {r.status === 1 && canDraft && <Button size="sm" variant="ghost" icon={<Repeat size={14} />} onClick={() => setForm({ range: null, replaces: r })}>{t('catalog.replace')}</Button>}
                        {r.status === 1 && canApprove && <Button size="sm" variant="ghost" icon={<Archive size={14} />} onClick={() => setConfirm({ kind: 'retire', range: r })}>{t('catalog.retire')}</Button>}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Modal>

      {form && (
        <RangeFormModal
          parameter={parameter}
          range={form.range}
          replaces={form.replaces}
          onClose={() => setForm(null)}
          onSaved={() => void qc.invalidateQueries({ queryKey: catalogKeys.test(testId) })}
        />
      )}
      <ConfirmDialog
        open={!!confirm}
        tone={confirm?.kind === 'approve' ? 'primary' : 'danger'}
        icon={confirm?.kind === 'approve' ? <CheckCircle2 size={20} /> : confirm?.kind === 'retire' ? <Archive size={20} /> : <Trash2 size={20} />}
        title={confirm?.kind === 'approve' ? t('catalog.approveTitle') : confirm?.kind === 'retire' ? t('catalog.retireTitle') : t('catalog.deleteDraftTitle')}
        text={confirm?.kind === 'approve' ? t('catalog.approveText') : confirm?.kind === 'retire' ? t('catalog.retireText') : t('common.unsavedText')}
        confirmLabel={confirm?.kind === 'approve' ? t('catalog.approve') : confirm?.kind === 'retire' ? t('catalog.retire') : t('common.delete')}
        loading={busy}
        onCancel={() => setConfirm(null)}
        onConfirm={() => void act()}
      />
    </>
  )
}
