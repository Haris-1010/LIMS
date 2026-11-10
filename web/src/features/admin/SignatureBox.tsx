import { useRef, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ImagePlus, Trash2 } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Button } from '../../components/ui/Button'
import { useToast } from '../../components/ui/Toast'
import { keys } from './adminApi'

const MAX_SIGNATURE = 200 * 1024

/** The signature image printed under this person's name on reports they approve (PNG or JPEG, up to 200 KB). */
export function SignatureBox({ userId, has }: { userId: number; has: boolean }) {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const fileRef = useRef<HTMLInputElement>(null)
  const [stamp, setStamp] = useState(0)
  const done = async (msg: string) => { toast.show({ tone: 'success', title: msg }); setStamp(Date.now()); await qc.invalidateQueries({ queryKey: keys.user(userId) }) }
  const failed = (e: unknown) => toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: errorMessage(t, e) })

  const upload = useMutation({
    mutationFn: (file: File) => { const f = new FormData(); f.append('file', file); return api(`/api/users/${userId}/signature`, { method: 'PUT', body: f }) },
    onSuccess: () => done(t('users.signatureSaved')), onError: failed,
  })
  const remove = useMutation({ mutationFn: () => api(`/api/users/${userId}/signature`, { method: 'DELETE' }), onSuccess: () => done(t('users.signatureRemoved')), onError: failed })

  const pick = (f: File | undefined) => {
    if (!f) return
    if (f.size > MAX_SIGNATURE) { toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: t('errors.image_too_large') }); return }
    upload.mutate(f)
    if (fileRef.current) fileRef.current.value = ''
  }

  return (
    <div style={{ display: 'flex', gap: 'var(--space-4)', alignItems: 'center' }}>
      <div className="label-preview" style={{ width: 160, height: 70, padding: 6 }}>
        {has ? <img src={`/api/reports/signatures/${userId}?v=${stamp}`} alt={t('users.signature')} style={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain' }} /> : <span className="field-hint">{t('users.noSignature')}</span>}
      </div>
      <span className="toolbar">
        <input ref={fileRef} type="file" accept="image/png,image/jpeg" hidden onChange={(e) => pick(e.target.files?.[0])} />
        <Button icon={<ImagePlus size={16} />} loading={upload.isPending} onClick={() => fileRef.current?.click()}>{t('users.uploadSignature')}</Button>
        {has && <Button variant="ghost" icon={<Trash2 size={16} />} loading={remove.isPending} onClick={() => remove.mutate()}>{t('users.removeSignature')}</Button>}
      </span>
    </div>
  )
}
