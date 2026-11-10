import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ImagePlus, Save, Trash2 } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { TextAreaField } from '../../components/ui/Controls'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import { reportKeys, type TemplateDto, type TemplateText } from './reportsApi'
import './report.css'

const MAX_LOGO = 300 * 1024
const empty: TemplateText = { headerNote: '', footerNote: '', disclaimer: '' }

/** Header, footer and disclaimer text (English and Urdu) and the logo printed on every report. Organisation and branch names come from Administration. */
export function ReportTemplatePage() {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const fileRef = useRef<HTMLInputElement>(null)
  const { data, isLoading } = useQuery({ queryKey: reportKeys.template, queryFn: () => api<TemplateDto>('/api/report-template') })
  const [en, setEn] = useState<TemplateText>(empty)
  const [ur, setUr] = useState<TemplateText>(empty)
  const [logoStamp, setLogoStamp] = useState(0)

  useEffect(() => {
    if (!data) return
    setEn({ ...empty, ...data.en })
    setUr({ ...empty, ...data.ur })
  }, [data])

  const refresh = () => qc.invalidateQueries({ queryKey: reportKeys.template })
  const save = useMutation({
    mutationFn: () => api('/api/report-template', { method: 'PUT', body: { rowVersion: data?.rowVersion ?? null, en, ur } }),
    onSuccess: async () => { toast.show({ tone: 'success', title: t('common.saved') }); await refresh() },
    onError: (e) => toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: errorMessage(t, e) }),
  })
  const upload = useMutation({
    mutationFn: (file: File) => { const f = new FormData(); f.append('file', file); return api('/api/report-template/logo', { method: 'PUT', body: f }) },
    onSuccess: async () => { toast.show({ tone: 'success', title: t('reports.logoSaved') }); setLogoStamp(Date.now()); await refresh() },
    onError: (e) => toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: errorMessage(t, e) }),
  })
  const remove = useMutation({
    mutationFn: () => api('/api/report-template/logo', { method: 'DELETE' }),
    onSuccess: async () => { toast.show({ tone: 'success', title: t('reports.logoRemoved') }); await refresh() },
    onError: (e) => toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: errorMessage(t, e) }),
  })

  const pickFile = (f: File | undefined) => {
    if (!f) return
    if (f.size > MAX_LOGO) { toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: t('errors.image_too_large') }); return }
    upload.mutate(f)
    if (fileRef.current) fileRef.current.value = ''
  }

  if (isLoading || !data) return <div className="page"><Skeleton height={300} radius={12} /></div>
  const field = (lang: 'en' | 'ur', text: TemplateText, set: (v: TemplateText) => void) => (
    <Card title={lang === 'en' ? t('reports.templateEnglish') : t('reports.templateUrdu')}>
      <div className="form-stack" dir={lang === 'ur' ? 'rtl' : 'ltr'}>
        <TextAreaField label={t('reports.headerNote')} value={text.headerNote ?? ''} rows={2} maxLength={500} onChange={(e) => set({ ...text, headerNote: e.target.value })} />
        <TextAreaField label={t('reports.footerNote')} value={text.footerNote ?? ''} rows={2} maxLength={500} onChange={(e) => set({ ...text, footerNote: e.target.value })} />
        <TextAreaField label={t('reports.disclaimer')} value={text.disclaimer ?? ''} rows={3} maxLength={1000} onChange={(e) => set({ ...text, disclaimer: e.target.value })} />
      </div>
    </Card>
  )

  return (
    <div className="page">
      <div className="page-head"><div><h2>{t('reports.templateTitle')}</h2><p>{t('reports.templateSubtitle')}</p></div>
        <Button variant="primary" icon={<Save size={16} />} loading={save.isPending} onClick={() => save.mutate()}>{t('common.save')}</Button></div>
      <Card title={t('reports.logo')} subtitle={t('reports.logoHint')}>
        <div style={{ display: 'flex', gap: 'var(--space-4)', alignItems: 'center' }}>
          <div className="label-preview" style={{ width: 160, height: 90, padding: 8 }}>
            {data.hasLogo ? <img src={`/api/report-template/logo?v=${logoStamp}`} alt={t('reports.logo')} style={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain' }} /> : <span className="field-hint">{t('reports.noLogo')}</span>}
          </div>
          <span className="toolbar">
            <input ref={fileRef} type="file" accept="image/png,image/jpeg" hidden onChange={(e) => pickFile(e.target.files?.[0])} />
            <Button icon={<ImagePlus size={16} />} loading={upload.isPending} onClick={() => fileRef.current?.click()}>{t('reports.uploadLogo')}</Button>
            {data.hasLogo && <Button variant="ghost" icon={<Trash2 size={16} />} loading={remove.isPending} onClick={() => remove.mutate()}>{t('reports.removeLogo')}</Button>}
          </span>
        </div>
      </Card>
      <div className="report-twocol section-gap">{field('en', en, setEn)}{field('ur', ur, setUr)}</div>
    </div>
  )
}
