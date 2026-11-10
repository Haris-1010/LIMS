import { useState } from 'react'
import { keepPreviousData, useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { ClipboardList, Plus, Printer } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { SelectField, TextAreaField } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import { useDepartmentOptions } from '../common/useDepartmentOptions'
import { formatAge, patientName } from '../patients/patientsApi'
import type { Paged } from '../admin/adminApi'
import { ResultStatusBadge } from './ResultParts'
import { useRefreshResults } from './resultHooks'
import { printOpenDialog, resultKeys, type PendingItem, type WorklistDetail, type WorklistListItem } from './resultsApi'
import './results.css'

const PAGE_SIZE = 25

/** Bench batches: pick received tests of one department, print the sheet, close it when the run is done. */
export function WorklistsPage() {
  const { t, i18n } = useTranslation()
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const [viewing, setViewing] = useState<number | null>(null)
  const params = { status: status || undefined, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: resultKeys.worklists(params), queryFn: () => api<Paged<WorklistListItem>>('/api/worklists', { query: params }), placeholderData: keepPreviousData })

  const columns: Column<WorklistListItem>[] = [
    { key: 'no', header: t('results.worklistNo'), width: 150, render: (w) => <span className="num cell-strong">{w.worklistNo}</span> },
    { key: 'dept', header: t('results.department'), render: (w) => (i18n.language === 'ur' && w.departmentNameUr ? w.departmentNameUr : w.departmentNameEn) },
    { key: 'items', header: t('results.tests'), render: (w) => <span className="num">{w.doneCount}/{w.itemCount}</span> },
    { key: 'status', header: t('orders.status'), render: (w) => <Badge tone={w.status === 0 ? 'primary' : 'neutral'} dot>{t(`worklistStatus.${w.status}`)}</Badge> },
    { key: 'at', header: t('results.created'), render: (w) => <span>{formatDateTime(w.createdAtUtc, i18n.language)}{w.createdByName && <span className="cell-sub">{w.createdByName}</span>}</span> },
  ]

  return (
    <div className="page">
      <div className="page-head">
        <div><h2>{t('results.worklistsTitle')}</h2><p>{t('results.worklistsSubtitle')}</p></div>
        <Button variant="primary" icon={<Plus size={16} />} onClick={() => setCreating(true)}>{t('results.newWorklist')}</Button>
      </div>
      <section className="card">
        <div className="card-toolbar">
          <select className="select select-sm" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }} aria-label={t('orders.status')}>
            <option value="">{t('samples.anyStatus')}</option>
            <option value="0">{t('worklistStatus.0')}</option>
            <option value="1">{t('worklistStatus.1')}</option>
          </select>
        </div>
        <DataTable columns={columns} rows={data?.items} rowKey={(w) => w.id} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage} onRowClick={(w) => setViewing(w.id)}
          empty={status ? undefined : <EmptyState icon={<ClipboardList size={22} />} title={t('results.noWorklistsTitle')} text={t('results.noWorklistsText')} />} />
      </section>
      {creating && <NewWorklistModal onClose={() => setCreating(false)} onCreated={(id) => { setCreating(false); setViewing(id) }} />}
      {viewing !== null && <WorklistModal id={viewing} onClose={() => setViewing(null)} />}
    </div>
  )
}

function NewWorklistModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: number) => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshResults()
  const departments = useDepartmentOptions()
  const [dept, setDept] = useState('')
  const [notes, setNotes] = useState('')
  const [picked, setPicked] = useState<Set<number>>(new Set())

  const pending = useQuery({
    queryKey: resultKeys.pending({ worklistPicker: dept }),
    queryFn: () => api<Paged<PendingItem>>('/api/results/pending', { query: { departmentId: dept, pageSize: 100 } }),
    enabled: !!dept,
  })
  const free = (pending.data?.items ?? []).filter((x) => !x.onWorklist)
  const toggle = (id: number) => setPicked((p) => { const n = new Set(p); if (n.has(id)) n.delete(id); else n.add(id); return n })

  const create = useMutation({
    mutationFn: () => api<WorklistDetail>('/api/worklists', { method: 'POST', body: { departmentId: Number(dept), orderItemIds: [...picked], notes: notes.trim() || null } }),
    onSuccess: async (d) => { toast.show({ tone: 'success', title: t('results.worklistCreated'), text: d.worklist.worklistNo }); await refresh(); onCreated(d.worklist.id) },
    onError: (e) => toast.show({ tone: 'danger', title: t('results.actionFailed'), text: errorMessage(t, e) }),
  })

  return (
    <Modal open title={t('results.newWorklist')} subtitle={t('results.newWorklistHint')} onClose={onClose} size="lg"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={create.isPending} disabled={!dept || picked.size === 0} onClick={() => create.mutate()}>{t('results.createWorklist', { count: picked.size })}</Button></>}>
      <div className="form-stack">
        <SelectField label={t('results.department')} required value={dept} onChange={(e) => { setDept(e.target.value); setPicked(new Set()) }}>
          <option value="">{t('results.chooseDepartment')}</option>
          {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
        </SelectField>
        {dept && (pending.isLoading ? <Skeleton height={120} radius={12} /> : free.length === 0 ? <p className="field-hint">{t('results.nothingForWorklist')}</p> : (
          <div className="manifest-pick" role="group" aria-label={t('results.tests')}>
            {free.map((x) => (
              <label key={x.orderItemId}>
                <input type="checkbox" checked={picked.has(x.orderItemId)} onChange={() => toggle(x.orderItemId)} />
                <span className="num cell-strong">{x.barcode}</span>
                <span dir="auto" style={{ flex: 1 }}>{patientName({ fullName: x.patientName, fullNameUr: x.patientNameUr }, i18n.language)} · {x.testCode}</span>
                {x.priority === 1 && <Badge tone="danger">{t('orders.urgent')}</Badge>}
              </label>
            ))}
          </div>
        ))}
        <TextAreaField label={t('samples.manifestNotes')} value={notes} maxLength={500} rows={2} onChange={(e) => setNotes(e.target.value)} />
      </div>
    </Modal>
  )
}

function WorklistModal({ id, onClose }: { id: number; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshResults()
  const { data: d, isLoading } = useQuery({ queryKey: resultKeys.worklist(id), queryFn: () => api<WorklistDetail>(`/api/worklists/${id}`) })
  const close = useMutation({
    mutationFn: () => api<WorklistDetail>(`/api/worklists/${id}/close`, { method: 'POST' }),
    onSuccess: async () => { await refresh() },
    onError: (e) => toast.show({ tone: 'danger', title: t('results.actionFailed'), text: errorMessage(t, e) }),
  })
  const w = d?.worklist
  return (
    <Modal open title={w ? <span className="num">{w.worklistNo}</span> : t('results.worklist')} subtitle={w && <Badge tone={w.status === 0 ? 'primary' : 'neutral'} dot>{t(`worklistStatus.${w.status}`)}</Badge>} onClose={onClose} size="xl"
      footer={d && (<>
        <Button variant="ghost" icon={<Printer size={16} />} onClick={printOpenDialog}>{t('results.printSheet')}</Button>
        {w!.status === 0 && <Button variant="primary" loading={close.isPending} onClick={() => close.mutate()}>{t('results.closeWorklist')}</Button>}
      </>)}>
      {isLoading && <Skeleton height={200} radius={12} />}
      {d && w && (
        <div className="form-stack">
          <dl className="test-facts" style={{ gridTemplateColumns: 'repeat(3, minmax(0, 1fr))' }}>
            <div><dt>{t('results.department')}</dt><dd>{i18n.language === 'ur' && w.departmentNameUr ? w.departmentNameUr : w.departmentNameEn}</dd></div>
            <div><dt>{t('orders.branch')}</dt><dd>{d.branchName}</dd></div>
            <div><dt>{t('results.created')}</dt><dd>{formatDateTime(w.createdAtUtc, i18n.language)}{w.createdByName ? ` · ${w.createdByName}` : ''}</dd></div>
          </dl>
          {w.notes && <p className="field-hint" dir="auto">{w.notes}</p>}
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>#</th><th>{t('samples.barcode')}</th><th>{t('orders.patientCol')}</th><th>{t('results.test')}</th><th>{t('results.parameters')}</th><th className="no-print">{t('orders.status')}</th></tr></thead>
              <tbody>
                {d.items.map((i, n) => (
                  <tr key={i.orderItemId}>
                    <td className="num">{n + 1}</td>
                    <td className="num cell-strong">{i.barcode}{i.priority === 1 && <> <Badge tone="danger">{t('orders.urgent')}</Badge></>}</td>
                    <td><span dir="auto">{patientName({ fullName: i.patientName, fullNameUr: i.patientNameUr }, i18n.language)}</span><span className="cell-sub num">{i.patientNo} · {formatAge(t, i.patientDateOfBirth)} · {t(`sex.short${i.patientSex}`)}</span></td>
                    <td><span dir="auto">{i18n.language === 'ur' && i.testNameUr ? i.testNameUr : i.testNameEn}</span><span className="cell-sub num">{i.testCode} · {i18n.language === 'ur' && i.sampleTypeNameUr ? i.sampleTypeNameUr : i.sampleTypeNameEn}</span></td>
                    <td className="num">{i.parameterCodes.join(', ')}</td>
                    <td className="no-print">{i.resultStatus === null ? <Link to={`/results/item/${i.orderItemId}`}>{t('results.enterResult')}</Link> : <Link to={`/results/item/${i.orderItemId}`}><ResultStatusBadge status={i.resultStatus} /></Link>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </Modal>
  )
}
