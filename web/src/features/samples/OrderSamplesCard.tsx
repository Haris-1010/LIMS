import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Printer, TestTube2 } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { useSession } from '../../app/session'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Modal } from '../../components/ui/Modal'
import { TextAreaField } from '../../components/ui/Controls'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import type { Paged } from '../admin/adminApi'
import { PatientIdentity, SampleDetailModal, SampleStatusBadge } from './SampleParts'
import { useRefreshSamples } from './sampleHooks'
import { printLabels } from './labelBus'
import { pick, sampleKeys, type OrderSampleDto, type SampleListItem } from './samplesApi'
import './samples.css'

/** The samples planned for an order, with "collect all" and "print labels" for the front desk / phlebotomy. */
export function OrderSamplesCard({ orderId, cancelled }: { orderId: number; cancelled: boolean }) {
  const { t, i18n } = useTranslation()
  const { can } = useSession()
  const [viewing, setViewing] = useState<number | null>(null)
  // Captured when the dialog opens: the live list empties as soon as the samples are collected.
  const [collecting, setCollecting] = useState<{ sample: SampleListItem; count: number } | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: sampleKeys.ofOrder(orderId),
    queryFn: () => api<Paged<SampleListItem>>('/api/samples', { query: { orderId, pageSize: 50 } }),
  })
  const samples = data?.items ?? []
  const live = samples.filter((s) => s.status !== 80 && s.status !== 90)
  const waiting = samples.filter((s) => s.status === 0)

  return (
    <>
      <Card
        title={t('samples.orderCardTitle')}
        subtitle={t('samples.orderCardSubtitle')}
        actions={
          <span className="toolbar">
            {can('samples.collect') && live.some((s) => s.status >= 10) && <Button size="sm" variant="ghost" icon={<Printer size={14} />} onClick={() => printLabels(live.filter((s) => s.status >= 10))}>{t('samples.printLabels')}</Button>}
            {!cancelled && can('samples.collect') && waiting.length > 0 && <Button size="sm" variant="primary" icon={<TestTube2 size={14} />} onClick={() => setCollecting({ sample: waiting[0]!, count: waiting.length })}>{t('samples.collectAll', { count: waiting.length })}</Button>}
          </span>
        }
        flush
      >
        {isLoading ? <div style={{ padding: 16 }}><Skeleton height={60} radius={8} /></div> : samples.length === 0 ? (
          <p className="field-hint" style={{ padding: 16 }}>{t('samples.orderNoSamples')}</p>
        ) : (
          <ul className="ledger" style={{ padding: '0 var(--space-5)' }}>
            {samples.map((s) => (
              <li key={s.id} style={{ gridTemplateColumns: 'minmax(0, 1fr) auto' }}>
                <button type="button" className="link-btn" onClick={() => setViewing(s.id)} style={{ textAlign: 'start', background: 'none', border: 0, padding: 0, cursor: 'pointer', font: 'inherit', color: 'inherit' }}>
                  <span className="cell-strong num">{s.barcode}</span>
                  <span className="cell-sub">
                    <span className="cap-dot" style={{ ['--cap' as string]: s.capColour ?? undefined }} aria-hidden />
                    {pick(s.sampleTypeNameEn, s.sampleTypeNameUr, i18n.language)} · <span className="num">{s.testCodes.join(', ')}</span>
                  </span>
                </button>
                <SampleStatusBadge status={s.status} />
              </li>
            ))}
          </ul>
        )}
      </Card>
      {collecting && <CollectAllDialog orderId={orderId} sample={collecting.sample} count={collecting.count} onClose={() => setCollecting(null)} onPrint={printLabels} />}
      {viewing !== null && <SampleDetailModal id={viewing} onClose={() => setViewing(null)} />}
    </>
  )
}

function CollectAllDialog({ orderId, sample, count, onClose, onPrint }: { orderId: number; sample: SampleListItem; count: number; onClose: () => void; onPrint: (s: SampleListItem[]) => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshSamples()
  const [confirmed, setConfirmed] = useState(false)
  const [notes, setNotes] = useState('')
  const collect = useMutation({
    mutationFn: () => api<OrderSampleDto[]>(`/api/orders/${orderId}/samples/collect`, { method: 'POST', body: { identityConfirmed: confirmed, notes: notes.trim() || null } }),
    onSuccess: async (done) => {
      toast.show({ tone: 'success', title: t('samples.collectedMany', { count: done.length }) })
      await refresh()
      // Labels need the patient details, so print from the refreshed list.
      const list = await api<Paged<SampleListItem>>('/api/samples', { query: { orderId, pageSize: 50 } })
      onPrint(list.items.filter((s) => done.some((d) => d.id === s.id)))
      onClose()
    },
    onError: (e) => toast.show({ tone: 'danger', title: t('samples.collectFailed'), text: errorMessage(t, e) }),
  })
  return (
    <Modal open title={t('samples.collectAllTitle', { count })} onClose={onClose} size="md"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" icon={<TestTube2 size={16} />} loading={collect.isPending} disabled={!confirmed} onClick={() => collect.mutate()}>{t('samples.collectAction')}</Button></>}>
      <div className="form-stack">
        <PatientIdentity sample={sample} />
        <label className="checkbox"><input type="checkbox" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />{t('samples.identityConfirm')}</label>
        <TextAreaField label={t('samples.collectionNotes')} value={notes} maxLength={500} rows={2} onChange={(e) => setNotes(e.target.value)} />
      </div>
    </Modal>
  )
}
