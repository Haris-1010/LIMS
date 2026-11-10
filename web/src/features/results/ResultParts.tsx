import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Phone } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { SelectField, TextAreaField } from '../../components/ui/Controls'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/Toast'
import { patientName } from '../patients/patientsApi'
import { useRefreshResults } from './resultHooks'
import { flagTone, resultTone, type AlertDto, type ResultFlag, type ResultStatus } from './resultsApi'

export function ResultStatusBadge({ status }: { status: ResultStatus }) {
  const { t } = useTranslation()
  return <Badge tone={resultTone[status]} dot>{t(`resultStatus.${status}`)}</Badge>
}

/** Flag next to a value. Unflagged values (no approved range) show nothing rather than a misleading "normal". */
export function FlagBadge({ flag, preview }: { flag: ResultFlag; preview?: boolean }) {
  const { t } = useTranslation()
  if (flag === 0) return null
  return <Badge tone={flagTone[flag]}>{t(`resultFlag.${flag}`)}{preview ? ' *' : ''}</Badge>
}

/** Records who was told about a critical value, how, and that it was read back. */
export function NotifyDialog({ alert, onClose, onDone }: { alert: AlertDto; onClose: () => void; onDone?: () => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshResults()
  const [to, setTo] = useState('')
  const [method, setMethod] = useState('1')
  const [readBack, setReadBack] = useState(false)
  const [comment, setComment] = useState('')

  const notify = useMutation({
    mutationFn: () => api(`/api/critical-alerts/${alert.id}/notify`, { method: 'POST', body: { notifiedTo: to.trim(), method: Number(method), readBackConfirmed: readBack, comment: comment.trim() || null } }),
    onSuccess: async () => { toast.show({ tone: 'success', title: t('results.alertReported') }); await refresh(); onDone?.(); onClose() },
    onError: (e) => toast.show({ tone: 'danger', title: t('results.actionFailed'), text: errorMessage(t, e) }),
  })
  const param = i18n.language === 'ur' && alert.parameterNameUr ? alert.parameterNameUr : alert.parameterNameEn

  return (
    <Modal open title={t('results.reportCritical')} subtitle={<span dir="auto">{patientName({ fullName: alert.patientName, fullNameUr: alert.patientNameUr }, i18n.language)}</span>} onClose={onClose} size="md"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" icon={<Phone size={16} />} loading={notify.isPending} disabled={!to.trim() || !readBack} onClick={() => notify.mutate()}>{t('results.recordNotification')}</Button></>}>
      <div className="form-stack">
        <div className="change-due is-warning" style={{ alignItems: 'flex-start' }}>
          <span>
            <strong dir="auto">{param}: <span className="num">{alert.valueText}</span> {alert.unit}</strong>
            <span className="cell-sub num">{alert.isHigh ? t('results.aboveLimit', { limit: alert.limit ?? '—' }) : t('results.belowLimit', { limit: alert.limit ?? '—' })}</span>
            {alert.patientMobile && <span className="cell-sub num" dir="ltr">{alert.patientMobile}</span>}
          </span>
        </div>
        <Field label={t('results.notifiedTo')} required value={to} maxLength={200} onChange={(e) => setTo(e.target.value)} hint={t('results.notifiedToHint')} />
        <SelectField label={t('results.notifyMethod')} value={method} onChange={(e) => setMethod(e.target.value)}>
          {[1, 2, 3].map((m) => <option key={m} value={m}>{t(`notifyMethod.${m}`)}</option>)}
        </SelectField>
        <label className="checkbox"><input type="checkbox" checked={readBack} onChange={(e) => setReadBack(e.target.checked)} />{t('results.readBackConfirm')}</label>
        <TextAreaField label={t('results.alertComment')} value={comment} maxLength={500} rows={2} onChange={(e) => setComment(e.target.value)} />
      </div>
    </Modal>
  )
}

/** Asks for a reason (return to entry, amend a released result). */
export function ReasonDialog({ title, text, confirmLabel, tone = 'primary', busy, onConfirm, onClose }: {
  title: string; text: string; confirmLabel: string; tone?: 'primary' | 'danger'; busy?: boolean; onConfirm: (reason: string) => void; onClose: () => void
}) {
  const { t } = useTranslation()
  const [reason, setReason] = useState('')
  return (
    <Modal open title={title} subtitle={text} onClose={onClose} size="sm"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant={tone === 'danger' ? 'danger' : 'primary'} loading={busy} disabled={!reason.trim()} onClick={() => onConfirm(reason.trim())}>{confirmLabel}</Button></>}>
      <TextAreaField label={t('results.reason')} value={reason} maxLength={500} rows={3} autoFocus onChange={(e) => setReason(e.target.value)} />
    </Modal>
  )
}
