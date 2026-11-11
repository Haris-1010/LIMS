import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ImagePlus, Pencil, Plus, Signature, Trash2 } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/Toast'
import { reportKeys, type ReportSignatoryDto } from './reportsApi'

const MAX = 4
const MAX_SIGNATURE = 200 * 1024
type Draft = { nameEn: string; nameUr: string; qualification: string; designation: string; registrationNo: string }
const emptyDraft: Draft = { nameEn: '', nameUr: '', qualification: '', designation: '', registrationNo: '' }

/**
 * People printed on every new report (e.g. the consultant pathologist), beside whoever approved the results. They are copied
 * into a report when it is issued, so changing or removing one never alters an issued report.
 */
export function ReportSignatoriesCard() {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const { data = [] } = useQuery({ queryKey: reportKeys.signatories, queryFn: () => api<ReportSignatoryDto[]>('/api/report-template/signatories') })
  const [editing, setEditing] = useState<ReportSignatoryDto | 'new' | null>(null)
  const [removing, setRemoving] = useState<ReportSignatoryDto | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)
  const [uploadFor, setUploadFor] = useState<number | null>(null)

  const refresh = () => qc.invalidateQueries({ queryKey: reportKeys.signatories })
  const failed = (e: unknown) => toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: errorMessage(t, e) })
  const remove = useMutation({
    mutationFn: (id: number) => api(`/api/report-template/signatories/${id}`, { method: 'DELETE' }),
    onSuccess: async () => { setRemoving(null); toast.show({ tone: 'success', title: t('reports.signatoryRemoved') }); await refresh() },
    onError: failed,
  })
  const upload = useMutation({
    mutationFn: ({ id, file }: { id: number; file: File }) => { const f = new FormData(); f.append('file', file); return api(`/api/report-template/signatories/${id}/signature`, { method: 'PUT', body: f }) },
    onSuccess: async () => { toast.show({ tone: 'success', title: t('reports.imageSaved') }); await refresh() },
    onError: failed,
  })
  const pick = (f: File | undefined) => {
    if (fileRef.current) fileRef.current.value = ''
    if (!f || uploadFor === null) return
    if (f.size > MAX_SIGNATURE) { toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: t('errors.image_too_large') }); return }
    upload.mutate({ id: uploadFor, file: f })
  }

  return (
    <Card
      title={<span className="tpl-card-title"><Signature size={17} aria-hidden /> {t('reports.signatoriesTitle')}</span>}
      subtitle={t('reports.signatoriesSubtitle')}
      actions={data.length < MAX ? <Button size="sm" icon={<Plus size={14} />} onClick={() => setEditing('new')}>{t('reports.addSignatory')}</Button> : undefined}
    >
      <input ref={fileRef} type="file" accept="image/png,image/jpeg" hidden onChange={(e) => pick(e.target.files?.[0])} />
      {data.length === 0 ? <p className="field-hint" style={{ margin: 0 }}>{t('reports.noSignatories')}</p> : (
        <ul className="tpl-signers">
          {data.map((s) => (
            <li key={s.id}>
              <button type="button" className="tpl-signer-sig" onClick={() => { setUploadFor(s.id); fileRef.current?.click() }} title={s.hasSignature ? t('reports.replaceImage') : t('reports.uploadSignature')}>
                {s.hasSignature ? <img src={`/api/report-template/signatories/${s.id}/signature?v=${encodeURIComponent(s.rowVersion)}`} alt="" /> : <><ImagePlus size={16} aria-hidden /><span>{t('reports.uploadSignature')}</span></>}
              </button>
              <span className="tpl-signer-text">
                <strong dir="auto">{s.nameEn}</strong>{s.nameUr && <span lang="ur" dir="rtl"> · {s.nameUr}</span>}
                <span className="field-hint" dir="auto">{[s.qualification, s.designation, s.registrationNo].filter(Boolean).join(' · ')}</span>
              </span>
              <span className="toolbar">
                <Button size="sm" variant="ghost" iconOnly icon={<Pencil size={14} />} aria-label={`${t('common.edit')}: ${s.nameEn}`} onClick={() => setEditing(s)} />
                <Button size="sm" variant="ghost" iconOnly icon={<Trash2 size={14} />} aria-label={`${t('reports.removeSignatory')}: ${s.nameEn}`} onClick={() => setRemoving(s)} />
              </span>
            </li>
          ))}
        </ul>
      )}
      {editing && <SignatoryDialog signatory={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={async () => { setEditing(null); await refresh() }} />}
      <ConfirmDialog open={removing !== null} tone="danger" icon={<Trash2 size={20} />} title={t('reports.removeSignatoryTitle')} text={t('reports.removeSignatoryText')}
        confirmLabel={t('reports.removeSignatory')} loading={remove.isPending} onCancel={() => setRemoving(null)} onConfirm={() => removing && remove.mutate(removing.id)} />
    </Card>
  )
}

function SignatoryDialog({ signatory, onClose, onSaved }: { signatory: ReportSignatoryDto | null; onClose: () => void; onSaved: () => Promise<void> }) {
  const { t } = useTranslation()
  const toast = useToast()
  const [d, setD] = useState<Draft>(signatory
    ? { nameEn: signatory.nameEn, nameUr: signatory.nameUr ?? '', qualification: signatory.qualification ?? '', designation: signatory.designation ?? '', registrationNo: signatory.registrationNo ?? '' }
    : emptyDraft)
  const save = useMutation({
    mutationFn: () => signatory
      ? api(`/api/report-template/signatories/${signatory.id}`, { method: 'PUT', body: { ...d, rowVersion: signatory.rowVersion } })
      : api('/api/report-template/signatories', { method: 'POST', body: d }),
    onSuccess: async () => { toast.show({ tone: 'success', title: t('common.saved') }); await onSaved() },
    onError: (e) => toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: errorMessage(t, e) }),
  })
  const set = (k: keyof Draft) => (e: React.ChangeEvent<HTMLInputElement>) => setD({ ...d, [k]: e.target.value })
  return (
    <Modal open size="md" onClose={onClose} title={signatory ? t('reports.editSignatory') : t('reports.addSignatory')} subtitle={t('reports.signatoryHint')}
      footer={<><Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={save.isPending} disabled={!d.nameEn.trim()} onClick={() => save.mutate()}>{t('common.save')}</Button></>}>
      <form className="form-stack" onSubmit={(e) => { e.preventDefault(); if (d.nameEn.trim()) save.mutate() }}>
        <Field label={t('reports.signatoryName')} required autoFocus maxLength={150} value={d.nameEn} onChange={set('nameEn')} placeholder="Dr. Ahmed Khan" />
        <Field label={t('reports.signatoryNameUr')} maxLength={150} dir="rtl" lang="ur" value={d.nameUr} onChange={set('nameUr')} />
        <Field label={t('reports.qualification')} maxLength={200} value={d.qualification} onChange={set('qualification')} placeholder="MBBS, FCPS (Haematology)" />
        <Field label={t('reports.designationLabel')} maxLength={150} value={d.designation} onChange={set('designation')} placeholder="Consultant Pathologist" />
        <Field label={t('reports.registrationNo')} maxLength={50} dir="ltr" value={d.registrationNo} onChange={set('registrationNo')} placeholder="PMDC 12345-P" />
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}
