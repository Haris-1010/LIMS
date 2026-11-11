import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { FileText, ImagePlus, Images, LayoutTemplate, Palette, PanelBottom, PanelTop, RefreshCw, Save, Trash2, Type } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Tabs, TextAreaField } from '../../components/ui/Controls'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import { ReportDesignsPanel } from './ReportDesignsPanel'
import { ReportDocument } from './ReportDocument'
import { ReportSignatoriesCard } from './ReportSignatoriesCard'
import { defaultLayout, designFor, sampleView } from './reportDesign'
import { imagesOf, reportKeys, type ReportLayoutDto, type ReportSignatoryDto, type TemplateDto, type TemplateText } from './reportsApi'
import './report.css'

const empty: TemplateText = { headerNote: '', footerNote: '', disclaimer: '' }
type ImageKind = 'logo' | 'header' | 'footer'
const MAX_BYTES: Record<ImageKind, number> = { logo: 300 * 1024, header: 600 * 1024, footer: 600 * 1024 }
const imageUrl = (kind: ImageKind) => (kind === 'logo' ? '/api/report-template/logo' : `/api/report-template/images/${kind}`)

/**
 * The report template: letterhead text (English and Urdu), the logo and the optional full-width header and footer images, with
 * a live preview on a sample report; and the named report designs. Organisation and branch names come from Administration.
 */
export function ReportTemplatePage() {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const { data, isLoading } = useQuery({ queryKey: reportKeys.template, queryFn: () => api<TemplateDto>('/api/report-template') })
  const layouts = useQuery({ queryKey: reportKeys.layouts, queryFn: () => api<ReportLayoutDto[]>('/api/report-layouts') })
  const signers = useQuery({ queryKey: reportKeys.signatories, queryFn: () => api<ReportSignatoryDto[]>('/api/report-template/signatories') })
  const [en, setEn] = useState<TemplateText>(empty)
  const [ur, setUr] = useState<TemplateText>(empty)
  const [textLang, setTextLang] = useState<'en' | 'ur'>('en')
  const [tab, setTab] = useState<'letterhead' | 'designs'>('letterhead')
  /** Design used for the preview on this tab: undefined = organisation default, null = built-in. */
  const [previewLayout, setPreviewLayout] = useState<number | null | undefined>(undefined)

  useEffect(() => {
    if (!data) return
    setEn({ ...empty, ...data.en })
    setUr({ ...empty, ...data.ur })
  }, [data])

  const same = (a: TemplateText, b: TemplateText | undefined) =>
    (a.headerNote ?? '') === (b?.headerNote ?? '') && (a.footerNote ?? '') === (b?.footerNote ?? '') && (a.disclaimer ?? '') === (b?.disclaimer ?? '')
  const dirty = !!data && (!same(en, data.en) || !same(ur, data.ur))

  const save = useMutation({
    mutationFn: () => api('/api/report-template', { method: 'PUT', body: { rowVersion: data?.rowVersion ?? null, en, ur } }),
    onSuccess: async () => { toast.show({ tone: 'success', title: t('common.saved') }); await qc.invalidateQueries({ queryKey: reportKeys.template }) },
    onError: (e) => toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: errorMessage(t, e) }),
  })

  // The sample report shows the text being edited, before it is saved.
  const sample = useMemo(() => {
    const v = sampleView()
    const n = (x: string | null | undefined) => (x && x.trim() ? x.trim() : null)
    v.snapshot.letterhead = {
      ...v.snapshot.letterhead,
      headerNoteEn: n(en.headerNote), headerNoteUr: n(ur.headerNote),
      footerNoteEn: n(en.footerNote), footerNoteUr: n(ur.footerNote),
      disclaimerEn: n(en.disclaimer), disclaimerUr: n(ur.disclaimer),
    }
    v.snapshot.fixedSignatories = (signers.data ?? []).map((x) => ({ id: x.id, nameEn: x.nameEn, nameUr: x.nameUr, qualification: x.qualification, designation: x.designation, registrationNo: x.registrationNo, hasSignature: x.hasSignature }))
    return v
  }, [en, ur, signers.data])

  if (isLoading || !data) return <div className="page"><Skeleton height={80} radius={12} /><div style={{ height: 16 }} /><Skeleton height={420} radius={12} /></div>

  const images = imagesOf(data)
  const list = layouts.data ?? []
  const layoutId = previewLayout === undefined ? defaultLayout(list)?.id ?? null : previewLayout
  const design = designFor(list, layoutId)
  const text = textLang === 'en' ? en : ur
  const setText = textLang === 'en' ? setEn : setUr

  return (
    <div className="page">
      <section className="tpl-hero">
        <span className="tpl-hero-icon" aria-hidden><LayoutTemplate size={22} /></span>
        <div className="tpl-hero-text">
          <h2>{t('reports.templateTitle')}</h2>
          <p>{t('reports.templateSubtitle')}</p>
        </div>
        {tab === 'letterhead' && (
          <span className="toolbar">
            {dirty && <Badge tone="warning" dot>{t('reports.unsaved')}</Badge>}
            <Button variant="primary" icon={<Save size={16} />} loading={save.isPending} disabled={!dirty} onClick={() => save.mutate()}>{t('common.save')}</Button>
          </span>
        )}
      </section>

      <div className="tpl-tabs">
        <Tabs value={tab} onChange={setTab} tabs={[{ key: 'letterhead', label: t('reports.tabLetterhead') }, { key: 'designs', label: t('reports.tabDesigns'), count: list.length }]} />
      </div>

      {tab === 'designs' ? <ReportDesignsPanel images={images} /> : (
        <div className="tpl-layout">
          <div className="tpl-editor">
            <Card title={<span className="tpl-card-title"><Images size={17} aria-hidden /> {t('reports.imagesTitle')}</span>} subtitle={t('reports.imagesSubtitle')}>
              <div className="tpl-images">
                <ImageTile kind="logo" has={images.logo} version={images.version} icon={<ImagePlus size={20} />} />
                <ImageTile kind="header" has={images.header} version={images.version} icon={<PanelTop size={20} />} wide />
                <ImageTile kind="footer" has={images.footer} version={images.version} icon={<PanelBottom size={20} />} wide />
              </div>
              <p className="field-hint tpl-images-note">{t('reports.imagesUseHint')}</p>
            </Card>

            <Card
              title={<span className="tpl-card-title"><Type size={17} aria-hidden /> {t('reports.textTitle')}</span>}
              subtitle={t('reports.textSubtitle')}
              actions={
                <div className="segmented" role="radiogroup" aria-label={t('reports.language')}>
                  {(['en', 'ur'] as const).map((l) => (
                    <button key={l} type="button" role="radio" aria-checked={textLang === l} aria-pressed={textLang === l} onClick={() => setTextLang(l)}>
                      {l === 'en' ? 'English' : <span lang="ur">اردو</span>}
                    </button>
                  ))}
                </div>
              }
            >
              <div className="form-stack" dir={textLang === 'ur' ? 'rtl' : 'ltr'} lang={textLang} key={textLang}>
                <TextAreaField label={t('reports.headerNote')} value={text.headerNote ?? ''} rows={2} maxLength={500} placeholder={t('reports.headerNotePlaceholder')} onChange={(e) => setText({ ...text, headerNote: e.target.value })} />
                <TextAreaField label={t('reports.footerNote')} value={text.footerNote ?? ''} rows={2} maxLength={500} placeholder={t('reports.footerNotePlaceholder')} onChange={(e) => setText({ ...text, footerNote: e.target.value })} />
                <TextAreaField label={t('reports.disclaimer')} value={text.disclaimer ?? ''} rows={3} maxLength={1000} placeholder={t('reports.disclaimerPlaceholder')} onChange={(e) => setText({ ...text, disclaimer: e.target.value })} />
              </div>
            </Card>

            <ReportSignatoriesCard />
          </div>

          <div className="tpl-preview">
            <Card
              title={<span className="tpl-card-title"><FileText size={17} aria-hidden /> {t('reports.livePreview')}</span>}
              actions={
                <select className="select select-sm" value={layoutId ?? ''} onChange={(e) => setPreviewLayout(e.target.value === '' ? null : Number(e.target.value))} aria-label={t('reports.design')}>
                  <option value="">{t('reports.builtIn')}</option>
                  {list.map((l) => <option key={l.id} value={l.id}>{l.name}{l.isDefault ? ` (${t('reports.default')})` : ''}</option>)}
                </select>
              }
              flush
            >
              <div className="report-stage tpl-stage">
                <div className="report-stage-scale"><ReportDocument view={sample} images={images} design={design} /></div>
              </div>
              {(images.header && !design.useHeaderImage) || (images.footer && !design.showFooterImage) ? (
                <p className="field-hint tpl-preview-note">
                  <Palette size={14} aria-hidden /> {t('reports.previewImagesOff')}{' '}
                  <button type="button" className="link-btn" onClick={() => setTab('designs')}>{t('reports.openDesigns')}</button>
                </p>
              ) : null}
            </Card>
          </div>
        </div>
      )}
    </div>
  )
}

/** One template image: preview, upload or replace, remove. Bytes are checked by the server (PNG or JPEG only). */
function ImageTile({ kind, has, version, icon, wide }: { kind: ImageKind; has: boolean; version: string; icon: ReactNode; wide?: boolean }) {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const fileRef = useRef<HTMLInputElement>(null)
  const done = async (title: string) => { toast.show({ tone: 'success', title }); await qc.invalidateQueries({ queryKey: reportKeys.template }) }
  const failed = (e: unknown) => toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: errorMessage(t, e) })
  const upload = useMutation({
    mutationFn: (file: File) => { const f = new FormData(); f.append('file', file); return api(imageUrl(kind), { method: 'PUT', body: f }) },
    onSuccess: () => done(t('reports.imageSaved')),
    onError: failed,
  })
  const remove = useMutation({
    mutationFn: () => api(imageUrl(kind), { method: 'DELETE' }),
    onSuccess: () => done(t('reports.imageRemoved')),
    onError: failed,
  })
  const pick = (f: File | undefined) => {
    if (fileRef.current) fileRef.current.value = ''
    if (!f) return
    if (f.size > MAX_BYTES[kind]) { toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: t('errors.image_too_large') }); return }
    upload.mutate(f)
  }

  return (
    <div className={`tpl-image${wide ? ' is-wide' : ''}${has ? ' has-image' : ''}`}>
      <button type="button" className="tpl-image-drop" onClick={() => fileRef.current?.click()} aria-label={`${t(`reports.image.${kind}`)}: ${has ? t('reports.replaceImage') : t('reports.uploadImage')}`}
        onDragOver={(e) => e.preventDefault()} onDrop={(e) => { e.preventDefault(); pick(e.dataTransfer.files?.[0]) }}>
        {has ? <img src={`${imageUrl(kind)}?v=${encodeURIComponent(version)}`} alt="" /> : (
          <span className="tpl-image-empty">{icon}<span>{t('reports.dropImage')}</span></span>
        )}
      </button>
      <div className="tpl-image-meta">
        <strong>{t(`reports.image.${kind}`)}</strong>
        <span className="field-hint">{t(`reports.imageHint.${kind}`)}</span>
      </div>
      <span className="toolbar">
        <input ref={fileRef} type="file" accept="image/png,image/jpeg" hidden onChange={(e) => pick(e.target.files?.[0])} />
        <Button size="sm" icon={has ? <RefreshCw size={14} /> : <ImagePlus size={14} />} loading={upload.isPending} onClick={() => fileRef.current?.click()}>{has ? t('reports.replaceImage') : t('reports.uploadImage')}</Button>
        {has && <Button size="sm" variant="ghost" icon={<Trash2 size={14} />} loading={remove.isPending} onClick={() => remove.mutate()}>{t('reports.removeImage')}</Button>}
      </span>
    </div>
  )
}
