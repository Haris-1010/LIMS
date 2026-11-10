import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { AlertTriangle, CheckCircle2, Printer, RefreshCw, TestTube2, XCircle } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { SelectField, TextAreaField } from '../../components/ui/Controls'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import { formatAge, formatPhone, patientName } from '../patients/patientsApi'
import { printLabels } from './labelBus'
import { useRefreshSamples, useRejectionReasons } from './sampleHooks'
import { pick, sampleKeys, sampleTone, type SampleDetail, type SampleListItem } from './samplesApi'

export function SampleStatusBadge({ status }: { status: SampleListItem['status'] }) {
  const { t } = useTranslation()
  return <Badge tone={sampleTone[status]} dot>{t(`sampleStatus.${status}`)}</Badge>
}

/** Name and identifiers a collector reads out and compares with the patient in front of them. */
export function PatientIdentity({ sample }: { sample: SampleListItem }) {
  const { t, i18n } = useTranslation()
  return (
    <div className="picked-patient">
      <div style={{ flex: 1, minWidth: 0 }}>
        <strong dir="auto" style={{ fontSize: 'var(--text-lg)' }}>{patientName({ fullName: sample.patientName, fullNameUr: sample.patientNameUr }, i18n.language)}</strong>
        <div className="cell-sub num">{sample.patientNo} · {formatAge(t, sample.patientDateOfBirth)} · {t(`sex.short${sample.patientSex}`)}{sample.patientDateOfBirth ? ` · ${sample.patientDateOfBirth}` : ''}</div>
      </div>
    </div>
  )
}

/** Collection: the collector must confirm identity before the sample can be collected. */
export function CollectDialog({ sample, onClose, onCollected }: { sample: SampleListItem; onClose: () => void; onCollected?: (s: SampleDetail) => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshSamples()
  const [confirmed, setConfirmed] = useState(false)
  const [notes, setNotes] = useState('')
  const [print, setPrint] = useState(true)

  const collect = useMutation({
    mutationFn: () => api<SampleDetail>(`/api/samples/${sample.id}/collect`, { method: 'POST', body: { identityConfirmed: confirmed, notes: notes.trim() || null } }),
    onSuccess: async (detail) => {
      toast.show({ tone: 'success', title: t('samples.collected'), text: sample.barcode })
      await refresh()
      onCollected?.(detail)
      if (print) printLabels([detail.sample])
      onClose()
    },
    onError: (e) => toast.show({ tone: 'danger', title: t('samples.collectFailed'), text: errorMessage(t, e) }),
  })

  return (
    <>
      <Modal open title={t('samples.collectTitle')} subtitle={<span className="num">{sample.barcode}</span>} onClose={onClose} size="md"
        footer={<>
          <Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" icon={<TestTube2 size={16} />} loading={collect.isPending} disabled={!confirmed} onClick={() => collect.mutate()}>{t('samples.collectAction')}</Button>
        </>}>
        <div className="form-stack">
          <PatientIdentity sample={sample} />
          <p className="field-hint">
            {pick(sample.sampleTypeNameEn, sample.sampleTypeNameUr, i18n.language)}{sample.capColour ? ` · ${sample.capColour}` : ''}{sample.container ? ` · ${sample.container}` : ''}
            {' — '}<span className="num">{sample.testCodes.join(', ')}</span>
          </p>
          <label className="checkbox">
            <input type="checkbox" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />
            <span>{t('samples.identityConfirm')}</span>
          </label>
          <TextAreaField label={t('samples.collectionNotes')} value={notes} maxLength={500} rows={2} onChange={(e) => setNotes(e.target.value)} />
          <label className="checkbox">
            <input type="checkbox" checked={print} onChange={(e) => setPrint(e.target.checked)} />
            <span>{t('samples.printAfterCollect')}</span>
          </label>
        </div>
      </Modal>
    </>
  )
}

export function RejectDialog({ sample, onClose }: { sample: SampleListItem; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshSamples()
  const { data: reasons } = useRejectionReasons()
  const [reasonId, setReasonId] = useState('')
  const [note, setNote] = useState('')
  const [recollect, setRecollect] = useState(true)
  const reason = reasons?.find((r) => String(r.id) === reasonId)
  const noteMissing = !!reason?.requiresNote && !note.trim()

  const reject = useMutation({
    mutationFn: () => api(`/api/samples/${sample.id}/reject`, { method: 'POST', body: { reasonId: Number(reasonId), note: note.trim() || null, requestRecollection: recollect } }),
    onSuccess: async () => {
      toast.show({ tone: 'success', title: t('samples.rejected'), text: sample.barcode })
      await refresh()
      onClose()
    },
    onError: (e) => toast.show({ tone: 'danger', title: t('samples.rejectFailed'), text: errorMessage(t, e) }),
  })

  return (
    <Modal open title={t('samples.rejectTitle')} subtitle={<span className="num">{sample.barcode}</span>} onClose={onClose} size="md"
      footer={<>
        <Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
        <Button variant="danger" icon={<XCircle size={16} />} loading={reject.isPending} disabled={!reasonId || noteMissing} onClick={() => reject.mutate()}>{t('samples.rejectAction')}</Button>
      </>}>
      <div className="form-stack">
        <PatientIdentity sample={sample} />
        <SelectField label={t('samples.reason')} required value={reasonId} onChange={(e) => setReasonId(e.target.value)}>
          <option value="">{t('samples.chooseReason')}</option>
          {reasons?.map((r) => <option key={r.id} value={r.id}>{pick(r.nameEn, r.nameUr, i18n.language)}</option>)}
        </SelectField>
        <TextAreaField label={reason?.requiresNote ? `${t('samples.rejectNote')} *` : t('samples.rejectNote')} value={note} maxLength={500} rows={3} onChange={(e) => setNote(e.target.value)}
          error={noteMissing ? t('errors.rejection_note_required') : undefined} />
        <label className="checkbox">
          <input type="checkbox" checked={recollect} onChange={(e) => setRecollect(e.target.checked)} />
          <span>{t('samples.requestRecollection')}</span>
        </label>
        <p className="field-hint">{t('samples.rejectHint')}</p>
      </div>
    </Modal>
  )
}

/** A one-click POST on a sample (receive, recollect) with the standard toast feedback. */
function useSampleAction(id: number, path: string, okTitle: string, barcode?: string) {
  const { t } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshSamples()
  return useMutation({
    mutationFn: () => api<SampleDetail>(`/api/samples/${id}/${path}`, { method: 'POST' }),
    onSuccess: async () => { toast.show({ tone: 'success', title: okTitle, text: barcode }); await refresh() },
    onError: (e) => toast.show({ tone: 'danger', title: t('samples.actionFailed'), text: errorMessage(t, e) }),
  })
}

const EVENT_TONE: Record<string, string> = { Rejected: 'is-bad', MissingAtDestination: 'is-bad', Received: 'is-good', Collected: 'is-good' }

/** Full sample record: who/what/where, its tests, the timeline, and the actions the user may take now. */
export function SampleDetailModal({ id, onClose }: { id: number; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const { can } = useSession()
  const [collecting, setCollecting] = useState(false)
  const [rejecting, setRejecting] = useState(false)

  const { data: d, isLoading, error } = useQuery({ queryKey: sampleKeys.detail(id), queryFn: () => api<SampleDetail>(`/api/samples/${id}`) })

  const receive = useSampleAction(id, 'receive', t('samples.received'), d?.sample.barcode)
  const recollect = useSampleAction(id, 'recollect', t('samples.recollectionPlanned'), d?.sample.barcode)

  const s = d?.sample
  return (
    <>
      <Modal open title={s ? <span className="num">{s.barcode}</span> : t('samples.title')} subtitle={s && <SampleStatusBadge status={s.status} />} onClose={onClose} size="lg"
        footer={d && (
          <>
            {d.actions.printLabel && can('samples.collect') && <Button variant="ghost" icon={<Printer size={16} />} onClick={() => printLabels([d.sample])}>{t('samples.printLabel')}</Button>}
            {d.actions.reject && can('samples.reject') && <Button variant="ghost" icon={<XCircle size={16} />} onClick={() => setRejecting(true)}>{t('samples.rejectAction')}</Button>}
            {d.actions.recollect && can('samples.collect') && <Button icon={<RefreshCw size={16} />} loading={recollect.isPending} onClick={() => recollect.mutate()}>{t('samples.recollect')}</Button>}
            {d.actions.collect && can('samples.collect') && <Button variant="primary" icon={<TestTube2 size={16} />} onClick={() => setCollecting(true)}>{t('samples.collectAction')}</Button>}
            {d.actions.receive && can('samples.receive') && <Button variant="primary" icon={<CheckCircle2 size={16} />} loading={receive.isPending} onClick={() => receive.mutate()}>{t('samples.receiveAction')}</Button>}
          </>
        )}>
        {isLoading && <Skeleton height={220} radius={12} />}
        {error && <p className="text-warning">{errorMessage(t, error)}</p>}
        {d && s && (
          <div className="form-stack">
            <PatientIdentity sample={s} />
            <dl className="test-facts" style={{ gridTemplateColumns: 'repeat(3, minmax(0, 1fr))' }}>
              <div><dt>{t('samples.order')}</dt><dd><Link to={`/orders/${s.orderId}`} className="num">{s.orderNo}</Link></dd></div>
              <div><dt>{t('samples.sampleType')}</dt><dd>{pick(s.sampleTypeNameEn, s.sampleTypeNameUr, i18n.language)}{s.capColour ? ` · ${s.capColour}` : ''}</dd></div>
              <div><dt>{t('orders.priority')}</dt><dd>{s.priority === 1 ? <Badge tone="danger">{t('orders.urgent')}</Badge> : t('orders.routine')}</dd></div>
              <div><dt>{t('samples.originBranch')}</dt><dd>{s.originBranch}</dd></div>
              <div><dt>{t('samples.currentBranch')}</dt><dd>{s.currentBranch}{s.destinationBranch && ` → ${s.destinationBranch}`}</dd></div>
              <div><dt>{t('samples.contact')}</dt><dd className="num">{formatPhone(d.patientMobile)}</dd></div>
              {d.doctorName && <div><dt>{t('orders.doctorCol')}</dt><dd dir="auto">{d.doctorName}</dd></div>}
              {d.collectedByName && <div><dt>{t('samples.collectedBy')}</dt><dd>{d.collectedByName}</dd></div>}
              {d.receivedByName && <div><dt>{t('samples.receivedBy')}</dt><dd>{d.receivedByName}</dd></div>}
            </dl>

            <RejectedNotice d={d} />

            <div>
              <h4 className="form-section-title">{t('samples.tests')}</h4>
              <div className="badge-list">
                {d.tests.map((x) => <Badge key={x.orderItemId} tone={x.status === 90 ? 'neutral' : 'primary'}><span className="num">{x.code}</span> {pick(x.nameEn, x.nameUr, i18n.language)}</Badge>)}
              </div>
            </div>

            {(d.replacesSampleId || d.replacedBySampleId) && (
              <p className="field-hint">
                {d.replacesBarcode && <>{t('samples.replaces')} <span className="num">{d.replacesBarcode}</span>. </>}
                {d.replacedByBarcode && <>{t('samples.replacedBy')} <span className="num">{d.replacedByBarcode}</span>.</>}
              </p>
            )}

            <div>
              <h4 className="form-section-title">{t('samples.timeline')}</h4>
              <ol className="timeline">
                {d.events.map((e, i) => (
                  <li key={i} className={EVENT_TONE[e.type]}>
                    <strong>{t(`sampleEvent.${e.type}`, { defaultValue: e.type })}</strong>
                    <span className="cell-sub">{formatDateTime(e.atUtc, i18n.language)}{e.userName ? ` · ${e.userName}` : ''}{e.branchName ? ` · ${e.branchName}` : ''}</span>
                    {e.notes && <span className="cell-sub" dir="auto">{e.notes}</span>}
                  </li>
                ))}
              </ol>
            </div>
          </div>
        )}
      </Modal>
      {collecting && s && <CollectDialog sample={s} onClose={() => setCollecting(false)} />}
      {rejecting && s && <RejectDialog sample={s} onClose={() => setRejecting(false)} />}
    </>
  )
}

/** Shows why a rejected sample was rejected. */
function RejectedNotice({ d }: { d: SampleDetail }) {
  const { t, i18n } = useTranslation()
  if (d.sample.status !== 80) return null
  return (
    <div className="change-due is-warning" role="status" style={{ alignItems: 'flex-start', gap: 8 }}>
      <AlertTriangle size={16} aria-hidden />
      <span style={{ flex: 1 }}>
        <strong>{t('samples.rejectedBecause', { reason: pick(d.rejectionReason ?? '', d.rejectionReasonUr, i18n.language) })}</strong>
        {d.rejectionNote && <span className="cell-sub" dir="auto">{d.rejectionNote}</span>}
        {d.rejectedByName && <span className="cell-sub">{d.rejectedByName}{d.rejectedAtUtc ? ` · ${formatDateTime(d.rejectedAtUtc, i18n.language)}` : ''}</span>}
      </span>
    </div>
  )
}

