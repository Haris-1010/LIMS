import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, Lock, LockOpen } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime, formatMoney } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { TextAreaField } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/Toast'
import { orderKeys, type DrawerDto } from './ordersApi'
import './orders.css'

export function CashDrawerPage() {
  const { t, i18n } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const { user } = useSession()
  const [openingFloat, setOpeningFloat] = useState('0')
  const [closing, setClosing] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const hasBranch = user?.activeBranchId != null

  const current = useQuery({ queryKey: orderKeys.drawer, queryFn: () => api<{ drawer: DrawerDto | null }>('/api/billing/drawer'), enabled: hasBranch })
  const history = useQuery({ queryKey: orderKeys.drawers, queryFn: () => api<DrawerDto[]>('/api/billing/drawers') })
  const drawer = current.data?.drawer

  const refresh = () => Promise.all([qc.invalidateQueries({ queryKey: orderKeys.drawer }), qc.invalidateQueries({ queryKey: orderKeys.drawers })])

  const open = async () => {
    setBusy(true)
    setFailure(null)
    try {
      await api('/api/billing/drawer/open', { method: 'POST', body: { openingFloat: Number(openingFloat) || 0 } })
      await refresh()
    } catch (e) {
      setFailure(errorMessage(t, e))
    } finally {
      setBusy(false)
    }
  }

  const columns: Column<DrawerDto>[] = [
    { key: 'cashier', header: t('drawer.cashier'), render: (d) => d.userName ?? '—' },
    { key: 'opened', header: t('drawer.opened'), render: (d) => formatDateTime(d.openedAtUtc, i18n.language) },
    { key: 'closed', header: t('drawer.closedAt'), render: (d) => (d.closedAtUtc ? formatDateTime(d.closedAtUtc, i18n.language) : <Badge tone="success" dot>{t('drawer.openNow')}</Badge>) },
    { key: 'expected', header: t('drawer.expected'), align: 'end', render: (d) => <span className="num">{formatMoney(d.expectedCash)}</span> },
    { key: 'counted', header: t('drawer.counted'), align: 'end', render: (d) => <span className="num">{d.countedCash === null ? '—' : formatMoney(d.countedCash)}</span> },
    { key: 'variance', header: t('drawer.variance'), align: 'end', render: (d) => (d.variance === null ? '—' : <span className={`num cell-strong ${d.variance === 0 ? '' : 'text-warning'}`}>{d.variance > 0 ? '+' : ''}{formatMoney(d.variance)}</span>) },
  ]

  return (
    <div className="page">
      <div className="page-head"><div><h2>{t('drawer.title')}</h2><p>{t('drawer.subtitle')}</p></div></div>
      {failure && <div className="banner tone-danger" role="alert" style={{ marginBottom: 16 }}><AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} /><span>{failure}</span></div>}

      {!hasBranch ? (
        <Card><EmptyState icon={<Lock size={22} />} title={t('drawer.closedTitle')} text={t('drawer.noBranch')} /></Card>
      ) : drawer ? (
        <Card title={t('drawer.openTitle')} subtitle={t('drawer.openedAt', { date: formatDateTime(drawer.openedAtUtc, i18n.language) })}
          actions={<Button variant="primary" icon={<Lock size={15} />} onClick={() => setClosing(true)}>{t('drawer.close')}</Button>}>
          <dl className="drawer-figures">
            <div><dt>{t('drawer.float')}</dt><dd className="num">{formatMoney(drawer.openingFloat)}</dd></div>
            <div><dt>{t('drawer.cashIn')}</dt><dd className="num">{formatMoney(drawer.cashIn)}</dd></div>
            <div><dt>{t('drawer.cashOut')}</dt><dd className="num">{formatMoney(drawer.cashOut)}</dd></div>
            <div className="is-total"><dt>{t('drawer.expected')}</dt><dd className="num">{formatMoney(drawer.expectedCash)}</dd></div>
          </dl>
        </Card>
      ) : (
        <Card>
          <EmptyState icon={<LockOpen size={22} />} title={t('drawer.closedTitle')} text={t('drawer.closedText')} />
          <form className="drawer-open-form" onSubmit={(e) => { e.preventDefault(); void open() }}>
            <Field label={t('drawer.openingFloat')} type="number" min={0} step="0.01" dir="ltr" value={openingFloat} onChange={(e) => setOpeningFloat(e.target.value)} />
            <Button type="submit" variant="primary" loading={busy}>{t('drawer.open')}</Button>
          </form>
        </Card>
      )}

      <section className="card section-gap">
        <header className="card-header"><h3 className="card-title">{t('drawer.history')}</h3></header>
        <DataTable columns={columns} rows={history.data} rowKey={(d) => d.id} loading={history.isLoading} />
      </section>

      {closing && drawer && <CloseModal drawer={drawer} onClose={() => setClosing(false)} onDone={async () => { setClosing(false); toast.show({ tone: 'success', title: t('drawer.closed') }); await refresh() }} />}
    </div>
  )
}

function CloseModal({ drawer, onClose, onDone }: { drawer: DrawerDto; onClose: () => void; onDone: () => void }) {
  const { t } = useTranslation()
  const [counted, setCounted] = useState('')
  const [notes, setNotes] = useState('')
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const variance = counted === '' ? null : Number(counted) - drawer.expectedCash

  const submit = async () => {
    if (counted === '' || !(Number(counted) >= 0)) return setFailure(t('common.required'))
    if (variance !== 0 && !notes.trim()) return setFailure(t('errors.drawer_variance_note_required'))
    setSaving(true)
    setFailure(null)
    try {
      await api('/api/billing/drawer/close', { method: 'POST', body: { countedCash: Number(counted), notes: notes.trim() || null } })
      onDone()
    } catch (e) {
      setFailure(errorMessage(t, e))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal open size="sm" onClose={onClose} title={t('drawer.closeTitle')} subtitle={t('drawer.closeText')}
      footer={<><Button onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={saving} onClick={() => void submit()}>{t('drawer.close')}</Button></>}>
      {failure && <div className="banner tone-danger" role="alert" style={{ marginBottom: 14 }}><AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} /><span>{failure}</span></div>}
      <form className="form-stack" onSubmit={(e) => { e.preventDefault(); void submit() }}>
        <Field label={t('drawer.counted')} type="number" min={0} step="0.01" dir="ltr" autoFocus value={counted} onChange={(e) => setCounted(e.target.value)} />
        {variance !== null && (
          <div className={`change-due${variance !== 0 ? ' is-warning' : ''}`}>
            <span>{t('drawer.variance')}</span><strong className="num">{variance > 0 ? '+' : ''}{formatMoney(variance)}</strong>
          </div>
        )}
        <TextAreaField label={t('drawer.notes')} rows={2} value={notes} maxLength={500} onChange={(e) => setNotes(e.target.value)} />
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}
