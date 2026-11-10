import { useRef, useState } from 'react'
import { keepPreviousData, useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Printer, Send, Truck } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { branchName, useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { SelectField, TextAreaField, Tabs } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import type { Paged } from '../admin/adminApi'
import { SampleStatusBadge } from './SampleParts'
import { useRefreshSamples } from './sampleHooks'
import { pick, sampleKeys, transferTone, type SampleDetail, type SampleListItem, type TransferDetail, type TransferListItem } from './samplesApi'
import './samples.css'

const PAGE_SIZE = 25

/** Prints just the open manifest on A4: the print stylesheet hides the app and the dialog chrome. */
function printManifest() {
  const style = document.createElement('style')
  style.textContent = '@page { size: A4; margin: 12mm; }'
  document.head.appendChild(style)
  document.body.classList.add('printing-manifest')
  const done = () => { style.remove(); document.body.classList.remove('printing-manifest') }
  window.addEventListener('afterprint', done, { once: true })
  window.print()
}
type Direction = 'incoming' | 'outgoing' | ''

/** Manifests between branches: build one from collected samples, print it, and close it at the destination. */
export function TransfersPage() {
  const { t, i18n } = useTranslation()
  const { can } = useSession()
  const [direction, setDirection] = useState<Direction>('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [viewing, setViewing] = useState<number | null>(null)
  const [creating, setCreating] = useState(false)
  const params = { direction: direction || undefined, status: status || undefined, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: sampleKeys.transfers(params), queryFn: () => api<Paged<TransferListItem>>('/api/sample-transfers', { query: params }), placeholderData: keepPreviousData })

  const columns: Column<TransferListItem>[] = [
    { key: 'no', header: t('samples.manifestNo'), width: 150, render: (x) => <span className="num cell-strong">{x.transferNo}</span> },
    { key: 'route', header: t('samples.route'), render: (x) => <span>{x.fromBranch} → {x.toBranch}</span> },
    { key: 'count', header: t('samples.sampleCount'), render: (x) => <span className="num">{x.receivedCount}/{x.sampleCount}{x.missingCount > 0 && <Badge tone="danger">{t('samples.missingCount', { count: x.missingCount })}</Badge>}</span> },
    { key: 'status', header: t('orders.status'), render: (x) => <Badge tone={transferTone[x.status]} dot>{t(`transferStatus.${x.status}`)}</Badge> },
    { key: 'at', header: t('samples.dispatchedAt'), render: (x) => <span>{formatDateTime(x.dispatchedAtUtc, i18n.language)}{x.dispatchedByName && <span className="cell-sub">{x.dispatchedByName}</span>}</span> },
  ]

  return (
    <div className="page">
      <div className="page-head">
        <div><h2>{t('samples.transfersTitle')}</h2><p>{t('samples.transfersSubtitle')}</p></div>
        {can('samples.transfer') && <Button variant="primary" icon={<Send size={16} />} onClick={() => setCreating(true)}>{t('samples.newManifest')}</Button>}
      </div>
      <div style={{ marginBottom: 16 }}>
        <Tabs value={direction || 'all'} onChange={(k) => { setDirection(k === 'all' ? '' : (k as Direction)); setPage(1) }}
          tabs={[{ key: 'all', label: t('common.all') }, { key: 'outgoing', label: t('samples.outgoing') }, { key: 'incoming', label: t('samples.incoming') }]} />
      </div>
      <section className="card">
        <div className="card-toolbar">
          <select className="select select-sm" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }} aria-label={t('orders.status')}>
            <option value="">{t('samples.anyStatus')}</option>
            {[0, 1, 2].map((s) => <option key={s} value={s}>{t(`transferStatus.${s}`)}</option>)}
          </select>
        </div>
        <DataTable columns={columns} rows={data?.items} rowKey={(x) => x.id} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage} onRowClick={(x) => setViewing(x.id)}
          empty={direction || status ? undefined : <EmptyState icon={<Truck size={22} />} title={t('samples.noTransfersTitle')} text={t('samples.noTransfersText')} />} />
      </section>
      {creating && <NewManifestModal onClose={() => setCreating(false)} onCreated={(id) => { setCreating(false); setViewing(id) }} />}
      {viewing !== null && <ManifestModal id={viewing} onClose={() => setViewing(null)} />}
    </div>
  )
}

interface Destination { id: number; code: string; nameEn: string; nameUr: string | null }

function NewManifestModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: number) => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshSamples()
  const [to, setTo] = useState('')
  const [notes, setNotes] = useState('')
  const [picked, setPicked] = useState<Set<number>>(new Set())
  const [scan, setScan] = useState('')
  const scanRef = useRef<HTMLInputElement>(null)

  const destinations = useQuery({ queryKey: ['samples', 'destinations'], queryFn: () => api<Destination[]>('/api/sample-transfers/destinations') })
  // Collected samples held at this branch and not already on a manifest.
  const ready = useQuery({ queryKey: sampleKeys.list({ ready: true }), queryFn: () => api<Paged<SampleListItem>>('/api/samples', { query: { status: 10, heldHere: true, pageSize: 100 } }) })
  const items = (ready.data?.items ?? []).filter((s) => s.status === 10)

  const toggle = (id: number) => setPicked((p) => { const n = new Set(p); if (n.has(id)) n.delete(id); else n.add(id); return n })

  const addScanned = async () => {
    const value = scan.trim()
    if (!value) return
    try {
      const d = await api<SampleDetail>(`/api/samples/by-barcode/${encodeURIComponent(value)}`)
      if (d.sample.status !== 10) throw new Error('state')
      setPicked((p) => new Set(p).add(d.sample.id))
      setScan('')
    } catch (e) {
      toast.show({ tone: 'danger', title: t('samples.cannotAdd'), text: e instanceof Error && e.message === 'state' ? t('errors.sample_invalid_state') : errorMessage(t, e) })
    }
    scanRef.current?.focus()
  }

  const create = useMutation({
    mutationFn: () => api<TransferDetail>('/api/sample-transfers', { method: 'POST', body: { toBranchId: Number(to), sampleIds: [...picked], notes: notes.trim() || null } }),
    onSuccess: async (d) => { toast.show({ tone: 'success', title: t('samples.manifestCreated'), text: d.transfer.transferNo }); await refresh(); onCreated(d.transfer.id) },
    onError: (e) => toast.show({ tone: 'danger', title: t('samples.actionFailed'), text: errorMessage(t, e) }),
  })

  return (
    <Modal open title={t('samples.newManifest')} subtitle={t('samples.newManifestHint')} onClose={onClose} size="lg"
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" icon={<Send size={16} />} loading={create.isPending} disabled={!to || picked.size === 0} onClick={() => create.mutate()}>{t('samples.dispatch', { count: picked.size })}</Button></>}>
      <div className="form-stack">
        <SelectField label={t('samples.destination')} required value={to} onChange={(e) => setTo(e.target.value)}>
          <option value="">{t('samples.chooseBranch')}</option>
          {destinations.data?.map((b) => <option key={b.id} value={b.id}>{branchName(b, i18n.language)}</option>)}
        </SelectField>
        <Field ref={scanRef} label={t('samples.scanToAdd')} value={scan} placeholder={t('samples.scanPlaceholder')} autoComplete="off" onChange={(e) => setScan(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); void addScanned() } }} />
        {ready.isLoading ? <Skeleton height={120} radius={12} /> : items.length === 0 ? (
          <p className="field-hint">{t('samples.nothingToSend')}</p>
        ) : (
          <div className="manifest-pick" role="group" aria-label={t('samples.readyToSend')}>
            {items.map((s) => (
              <label key={s.id}>
                <input type="checkbox" checked={picked.has(s.id)} onChange={() => toggle(s.id)} />
                <span className="num cell-strong">{s.barcode}</span>
                <span dir="auto" style={{ flex: 1 }}>{s.patientName} · {pick(s.sampleTypeNameEn, s.sampleTypeNameUr, i18n.language)}</span>
                {s.priority === 1 && <Badge tone="danger">{t('orders.urgent')}</Badge>}
              </label>
            ))}
          </div>
        )}
        <TextAreaField label={t('samples.manifestNotes')} value={notes} maxLength={500} rows={2} onChange={(e) => setNotes(e.target.value)} />
      </div>
    </Modal>
  )
}

function ManifestModal({ id, onClose }: { id: number; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const refresh = useRefreshSamples()
  const [confirm, setConfirm] = useState<'complete' | 'cancel' | null>(null)
  const { data: d, isLoading } = useQuery({ queryKey: sampleKeys.transfer(id), queryFn: () => api<TransferDetail>(`/api/sample-transfers/${id}`) })

  const act = useMutation({
    mutationFn: (path: 'complete' | 'cancel') => api<TransferDetail>(`/api/sample-transfers/${id}/${path}`, { method: 'POST' }),
    onSuccess: async () => { setConfirm(null); await refresh() },
    onError: (e) => { setConfirm(null); toast.show({ tone: 'danger', title: t('samples.actionFailed'), text: errorMessage(t, e) }) },
  })

  const tr = d?.transfer
  return (
    <>
      <Modal open title={tr ? <span className="num">{tr.transferNo}</span> : t('samples.manifest')} subtitle={tr && <Badge tone={transferTone[tr.status]} dot>{t(`transferStatus.${tr.status}`)}</Badge>} onClose={onClose} size="lg"
        footer={d && (<>
          <Button variant="ghost" icon={<Printer size={16} />} onClick={printManifest}>{t('samples.printManifest')}</Button>
          {d.canCancel && <Button variant="ghost" onClick={() => setConfirm('cancel')}>{t('samples.cancelManifest')}</Button>}
          {d.canComplete && <Button variant="primary" onClick={() => setConfirm('complete')}>{t('samples.closeManifest')}</Button>}
        </>)}>
        {isLoading && <Skeleton height={200} radius={12} />}
        {d && tr && (
          <div className="form-stack manifest-print">
            <dl className="test-facts" style={{ gridTemplateColumns: 'repeat(3, minmax(0, 1fr))' }}>
              <div><dt>{t('samples.route')}</dt><dd>{tr.fromBranch} → {tr.toBranch}</dd></div>
              <div><dt>{t('samples.dispatchedAt')}</dt><dd>{formatDateTime(tr.dispatchedAtUtc, i18n.language)}</dd></div>
              <div><dt>{t('samples.dispatchedBy')}</dt><dd>{tr.dispatchedByName ?? '—'}</dd></div>
            </dl>
            {d.notes && <p className="field-hint" dir="auto">{d.notes}</p>}
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>{t('samples.barcode')}</th><th>{t('orders.patientCol')}</th><th>{t('samples.sampleType')}</th><th>{t('orders.status')}</th></tr></thead>
                <tbody>
                  {d.items.map((i) => (
                    <tr key={i.sampleId}>
                      <td className="num cell-strong">{i.barcode}</td>
                      <td dir="auto">{i.patientName}</td>
                      <td>{pick(i.sampleTypeNameEn, i.sampleTypeNameUr, i18n.language)}</td>
                      <td>{i.markedMissing ? <Badge tone="danger">{t('sampleEvent.MissingAtDestination')}</Badge> : i.receivedAtUtc ? <Badge tone="success" dot>{t('samples.received')}</Badge> : <SampleStatusBadge status={i.sampleStatus} />}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {d.canReceive && <p className="field-hint">{t('samples.receiveHint')}</p>}
          </div>
        )}
      </Modal>
      <ConfirmDialog open={confirm !== null} icon={<Truck size={20} />} tone={confirm === 'cancel' ? 'danger' : 'primary'} loading={act.isPending}
        title={confirm === 'cancel' ? t('samples.cancelManifestTitle') : t('samples.closeManifestTitle')}
        text={confirm === 'cancel' ? t('samples.cancelManifestText') : t('samples.closeManifestText', { count: d ? d.items.filter((i) => !i.receivedAtUtc).length : 0 })}
        confirmLabel={confirm === 'cancel' ? t('samples.cancelManifest') : t('samples.closeManifest')}
        onConfirm={() => confirm && act.mutate(confirm)} onCancel={() => setConfirm(null)} />
    </>
  )
}
