import { useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Pencil, Plus, XCircle } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { SearchInput, Switch, Tabs } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/Toast'
import { patientName } from '../patients/patientsApi'
import type { Paged } from '../admin/adminApi'
import { SampleDetailModal } from './SampleParts'
import { useRejectionReasons } from './sampleHooks'
import { pick, sampleKeys, type RejectionReason, type SampleListItem } from './samplesApi'
import './samples.css'

const PAGE_SIZE = 25

/** Rejected samples and recollection, plus (for catalogue managers) the editable list of rejection reasons. */
export function RejectionsPage() {
  const { t } = useTranslation()
  const { can } = useSession()
  const [tab, setTab] = useState<'rejected' | 'reasons'>('rejected')
  const tabs = [{ key: 'rejected' as const, label: t('samples.rejectedTab') }, ...(can('catalog.manage') ? [{ key: 'reasons' as const, label: t('samples.reasonsTab') }] : [])]
  return (
    <div className="page">
      <div className="page-head"><div><h2>{t('samples.rejectionsTitle')}</h2><p>{t('samples.rejectionsSubtitle')}</p></div></div>
      {tabs.length > 1 && <div style={{ marginBottom: 16 }}><Tabs tabs={tabs} value={tab} onChange={setTab} /></div>}
      {tab === 'rejected' ? <RejectedList /> : <ReasonsList />}
    </div>
  )
}

function RejectedList() {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [viewing, setViewing] = useState<number | null>(null)
  const params = { search, status: 80, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: sampleKeys.list(params), queryFn: () => api<Paged<SampleListItem>>('/api/samples', { query: params }), placeholderData: keepPreviousData })
  const columns: Column<SampleListItem>[] = [
    { key: 'barcode', header: t('samples.barcode'), render: (s) => <span className="num cell-strong">{s.barcode}</span> },
    { key: 'patient', header: t('orders.patientCol'), render: (s) => <span><span dir="auto">{patientName({ fullName: s.patientName, fullNameUr: s.patientNameUr }, i18n.language)}</span><span className="cell-sub num">{s.patientNo} · {s.orderNo}</span></span> },
    { key: 'type', header: t('samples.sampleType'), render: (s) => pick(s.sampleTypeNameEn, s.sampleTypeNameUr, i18n.language) },
    { key: 'branch', header: t('samples.currentBranch'), render: (s) => s.currentBranch },
    { key: 'at', header: t('samples.collectedAt'), render: (s) => (s.collectedAtUtc ? formatDateTime(s.collectedAtUtc, i18n.language) : '—') },
    { key: 'flag', header: '', render: (s) => (s.isRecollection ? <Badge tone="warning">{t('samples.recollectionBadge')}</Badge> : null) },
  ]
  return (
    <section className="card">
      <div className="card-toolbar"><SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('samples.trackingSearch')} /></div>
      <DataTable columns={columns} rows={data?.items} rowKey={(s) => s.id} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage} onRowClick={(s) => setViewing(s.id)}
        empty={search ? undefined : <EmptyState icon={<XCircle size={22} />} title={t('samples.noRejectionsTitle')} text={t('samples.noRejectionsText')} />} />
      {viewing !== null && <SampleDetailModal id={viewing} onClose={() => setViewing(null)} />}
    </section>
  )
}

function ReasonsList() {
  const { t } = useTranslation()
  const { data, isLoading } = useRejectionReasons(true)
  const [editing, setEditing] = useState<RejectionReason | 'new' | null>(null)
  const columns: Column<RejectionReason>[] = [
    { key: 'code', header: t('common.code'), width: 180, render: (r) => <span className="num cell-strong">{r.code}</span> },
    { key: 'name', header: t('samples.reason'), render: (r) => <span><span>{r.nameEn}</span><span className="cell-sub" dir="rtl">{r.nameUr}</span></span> },
    { key: 'note', header: t('samples.noteRequired'), render: (r) => (r.requiresNote ? t('common.yes') : t('common.no')) },
    { key: 'active', header: t('common.status'), render: (r) => <Badge tone={r.isActive ? 'success' : 'neutral'} dot>{r.isActive ? t('common.active') : t('common.inactive')}</Badge> },
    { key: 'edit', header: '', align: 'end', render: (r) => <Button size="sm" variant="ghost" iconOnly aria-label={t('common.edit')} icon={<Pencil size={15} />} onClick={() => setEditing(r)} /> },
  ]
  return (
    <section className="card">
      <div className="card-toolbar"><span /><Button variant="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>{t('samples.addReason')}</Button></div>
      <DataTable columns={columns} rows={data} rowKey={(r) => r.id} loading={isLoading} />
      {editing && <ReasonModal reason={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </section>
  )
}

function ReasonModal({ reason, onClose }: { reason: RejectionReason | null; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const [f, setF] = useState({ code: reason?.code ?? '', nameEn: reason?.nameEn ?? '', nameUr: reason?.nameUr ?? '', requiresNote: reason?.requiresNote ?? false, sortOrder: reason?.sortOrder ?? 100, isActive: reason?.isActive ?? true })
  const save = useMutation({
    mutationFn: () => {
      const body = { code: f.code.trim(), nameEn: f.nameEn.trim(), nameUr: f.nameUr.trim() || null, requiresNote: f.requiresNote, sortOrder: f.sortOrder }
      return reason
        ? api(`/api/samples/rejection-reasons/${reason.id}`, { method: 'PUT', body: { ...body, isActive: f.isActive, rowVersion: reason.rowVersion } })
        : api('/api/samples/rejection-reasons', { method: 'POST', body })
    },
    onSuccess: async () => { toast.show({ tone: 'success', title: t('common.saved') }); await qc.invalidateQueries({ queryKey: ['samples', 'reasons'] }); onClose() },
    onError: (e) => toast.show({ tone: 'danger', title: t('samples.actionFailed'), text: errorMessage(t, e) }),
  })
  return (
    <Modal open title={reason ? t('samples.editReason') : t('samples.addReason')} onClose={onClose} size="md"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={save.isPending} disabled={!f.code.trim() || !f.nameEn.trim()} onClick={() => save.mutate()}>{t('common.save')}</Button></>}>
      <div className="form-stack">
        <Field label={t('common.code')} value={f.code} disabled={!!reason} maxLength={30} onChange={(e) => setF({ ...f, code: e.target.value.toUpperCase() })} hint={t('common.codeHint')} />
        <Field label={t('common.nameEn')} value={f.nameEn} maxLength={200} onChange={(e) => setF({ ...f, nameEn: e.target.value })} />
        <Field label={t('common.nameUr')} dir="rtl" value={f.nameUr} maxLength={200} onChange={(e) => setF({ ...f, nameUr: e.target.value })} />
        <Field label={t('common.sortOrder')} type="number" min={0} max={10000} value={f.sortOrder} onChange={(e) => setF({ ...f, sortOrder: Number(e.target.value) })} />
        <label className="checkbox"><input type="checkbox" checked={f.requiresNote} onChange={(e) => setF({ ...f, requiresNote: e.target.checked })} />{t('samples.noteRequired')}</label>
        {reason && <Switch checked={f.isActive} onChange={(v) => setF({ ...f, isActive: v })} label={t('common.active')} />}
      </div>
    </Modal>
  )
}
