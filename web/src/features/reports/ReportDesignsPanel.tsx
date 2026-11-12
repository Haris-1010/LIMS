import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ArrowDown, ArrowUp, Palette, Plus, Save, Star, Trash2 } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { SelectField, Switch } from '../../components/ui/Controls'
import { EmptyState } from '../../components/ui/EmptyState'
import { Field } from '../../components/ui/Field'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import { ReportDocument } from './ReportDocument'
import { ACCENT_SWATCHES, CLASSIC, PRESETS, sampleView, withPreset } from './reportDesign'
import { reportKeys, type DesignStyle, type LogoPosition, type ReportDesign, type ReportImages, type ReportLayoutDto, type TableStyle } from './reportsApi'

type HeaderMode = 'logo' | 'image' | 'none'
type Editing = { id: number | null; name: string; isDefault: boolean; design: ReportDesign; rowVersion: string | null }

const blank = (): Editing => ({ id: null, name: '', isDefault: false, design: withPreset(CLASSIC, 'modern'), rowVersion: null })
// Designs saved before header/footer images existed have no value for them.
const fromDto = (l: ReportLayoutDto): Editing => ({ id: l.id, name: l.name, isDefault: l.isDefault, design: { ...CLASSIC, ...l.design }, rowVersion: l.rowVersion })

/** Named report designs: pick a ready style, adjust it, and see the result on a sample report while editing. */
export function ReportDesignsPanel({ images }: { images: ReportImages }) {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const { data, isLoading } = useQuery({ queryKey: reportKeys.layouts, queryFn: () => api<ReportLayoutDto[]>('/api/report-layouts') })
  const [edit, setEdit] = useState<Editing | null>(null)
  const [confirmDelete, setConfirmDelete] = useState(false)
  const sample = useMemo(() => sampleView(), [])

  // Open the default design (else the first, else a new one) once the list has loaded.
  useEffect(() => {
    if (!data || edit) return
    const first = data.find((l) => l.isDefault) ?? data[0]
    setEdit(first ? fromDto(first) : blank())
  }, [data, edit])

  const refresh = () => qc.invalidateQueries({ queryKey: reportKeys.layouts })
  const save = useMutation({
    mutationFn: async (e: Editing) => {
      const body = { name: e.name.trim(), isDefault: e.isDefault, design: e.design }
      if (e.id === null) return (await api<{ id: number }>('/api/report-layouts', { method: 'POST', body })).id
      await api(`/api/report-layouts/${e.id}`, { method: 'PUT', body: { ...body, rowVersion: e.rowVersion } })
      return e.id
    },
    onSuccess: async (id) => {
      toast.show({ tone: 'success', title: t('reports.designSaved') })
      await refresh()
      const list = qc.getQueryData<ReportLayoutDto[]>(reportKeys.layouts)
      const saved = list?.find((l) => l.id === id)
      if (saved) setEdit(fromDto(saved))
    },
    onError: (e) => toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: errorMessage(t, e) }),
  })
  const remove = useMutation({
    mutationFn: (id: number) => api(`/api/report-layouts/${id}`, { method: 'DELETE' }),
    onSuccess: async () => {
      toast.show({ tone: 'success', title: t('reports.designDeleted') })
      setConfirmDelete(false)
      setEdit(null)
      await refresh()
    },
    onError: (e) => toast.show({ tone: 'danger', title: t('reports.actionFailed'), text: errorMessage(t, e) }),
  })

  if (isLoading || !data || !edit) return <Skeleton height={400} radius={12} />

  const d = edit.design
  const set = (patch: Partial<ReportDesign>) => setEdit({ ...edit, design: { ...d, ...patch } })
  const move = (i: number, step: -1 | 1) => {
    const blocks = [...d.blocks]
    const j = i + step
    if (j < 0 || j >= blocks.length) return
    ;[blocks[i], blocks[j]] = [blocks[j]!, blocks[i]!]
    set({ blocks })
  }
  const mm = (v: string, min: number, max: number) => Math.min(max, Math.max(min, Math.round(Number(v) || min)))
  const headerMode: HeaderMode = !d.showLetterhead ? 'none' : d.useHeaderImage ? 'image' : 'logo'
  const setHeaderMode = (m: HeaderMode) => set({ showLetterhead: m !== 'none', useHeaderImage: m === 'image' })
  const isSavedDefault = data.find((l) => l.id === edit.id)?.isDefault === true

  return (
    <div className="design-layout">
      <Card title={t('reports.designsTitle')} flush className="design-list-card">
        <div style={{ padding: 'var(--space-3)' }}>
          <Button block icon={<Plus size={16} />} onClick={() => setEdit(blank())}>{t('reports.newDesign')}</Button>
        </div>
        {data.length === 0 ? (
          <EmptyState icon={<Palette size={22} />} title={t('reports.noDesigns')} text={t('reports.noDesignsText')} />
        ) : (
          <ul className="design-list" style={{ padding: '0 var(--space-3) var(--space-3)' }}>
            {data.map((l) => (
              <li key={l.id}>
                <button type="button" aria-current={edit.id === l.id} onClick={() => setEdit(fromDto(l))}>
                  <span className="design-list-swatch" style={{ background: l.design.accentColor }} aria-hidden />
                  <span className="design-list-name">
                    <span dir="auto">{l.name}</span>
                    <small>{t(`reports.style.${l.design.style}`)}</small>
                  </span>
                  {l.isDefault && <Badge tone="success">{t('reports.default')}</Badge>}
                </button>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card
        title={edit.id === null ? t('reports.newDesign') : edit.name || t('reports.design')}
        subtitle={t('reports.designsSubtitle')}
        className="design-editor"
        footer={
          <span className="toolbar">
            <Button variant="primary" icon={<Save size={16} />} loading={save.isPending} disabled={!edit.name.trim()} onClick={() => save.mutate(edit)}>{t('common.save')}</Button>
            {edit.id !== null && !isSavedDefault && (
              <Button variant="ghost" icon={<Star size={16} />} disabled={!edit.name.trim()} onClick={() => save.mutate({ ...edit, isDefault: true })}>{t('reports.saveAsDefault')}</Button>
            )}
            {edit.id !== null && <Button variant="ghost" icon={<Trash2 size={16} />} onClick={() => setConfirmDelete(true)}>{t('reports.deleteDesign')}</Button>}
          </span>
        }
      >
        <div className="form-stack">
          <Field label={t('reports.designName')} required value={edit.name} maxLength={100} onChange={(e) => setEdit({ ...edit, name: e.target.value })} />
          <Switch checked={edit.isDefault} onChange={(v) => setEdit({ ...edit, isDefault: v })} label={t('reports.makeDefault')} hint={t('reports.makeDefaultHint')} />

          <Section title={t('reports.sectionStyle')}>
            <div className="preset-grid">
              {(Object.keys(PRESETS) as DesignStyle[]).map((st) => (
                <button key={st} type="button" className="preset-card" aria-pressed={d.style === st} onClick={() => setEdit({ ...edit, design: withPreset(d, st) })}>
                  <PresetThumb style={st} accent={d.style === st ? d.accentColor : PRESETS[st].accentColor ?? d.accentColor} />
                  <strong>{t(`reports.style.${st}`)}</strong>
                  <small>{t(`reports.styleHint.${st}`)}</small>
                </button>
              ))}
            </div>
            <div>
              <div className="field-label" style={{ marginBottom: 6 }}>{t('reports.accentColor')}</div>
              <div className="swatches">
                {ACCENT_SWATCHES.map((c) => (
                  <button key={c} type="button" className="swatch" style={{ background: c }} aria-pressed={d.accentColor.toLowerCase() === c} aria-label={c} onClick={() => set({ accentColor: c })} />
                ))}
                <input type="color" className="color-input" value={d.accentColor} aria-label={t('reports.accentColor')} onChange={(e) => set({ accentColor: e.target.value })} />
              </div>
            </div>
            <div className="field">
              <label className="field-label" htmlFor="design-font">{t('reports.fontSize', { size: d.fontSizePt })}</label>
              <input id="design-font" className="range-input" type="range" min={7.5} max={12} step={0.5} value={d.fontSizePt} onChange={(e) => set({ fontSizePt: Number(e.target.value) })} />
              <span className="field-hint">{t('reports.fitHint')}</span>
            </div>
          </Section>

          <Section title={t('reports.sectionHeader')}>
            <div className="choice-grid" role="radiogroup" aria-label={t('reports.headerMode')}>
              {(['logo', 'image', 'none'] as const).map((m) => (
                <button key={m} type="button" role="radio" aria-checked={headerMode === m} className="choice-card" onClick={() => setHeaderMode(m)}>
                  <strong>{t(`reports.headerModes.${m}`)}</strong>
                  <small>{t(`reports.headerModeHint.${m}`)}</small>
                </button>
              ))}
            </div>
            {headerMode === 'image' && !images.header && <div className="notice is-warning" role="status"><span>{t('reports.noHeaderImage')}</span></div>}
            {headerMode === 'logo' && (
              <SelectField label={t('reports.logoPosition')} value={d.logoPosition} onChange={(e) => set({ logoPosition: e.target.value as LogoPosition })}>
                {(['start', 'center', 'end', 'none'] as const).map((x) => <option key={x} value={x}>{t(`reports.logoPos.${x}`)}</option>)}
              </SelectField>
            )}
            <Switch checked={d.showFooterImage} onChange={(v) => set({ showFooterImage: v })} label={t('reports.showFooterImage')} hint={images.footer ? t('reports.showFooterImageHint') : t('reports.noFooterImage')} />
          </Section>

          <Section title={t('reports.margins')}>
            <div className="margin-grid">
              <Field label={t('reports.marginTop')} type="number" min={5} max={80} value={d.marginTopMm} onChange={(e) => set({ marginTopMm: mm(e.target.value, 5, 80) })} />
              <Field label={t('reports.marginBottom')} type="number" min={5} max={60} value={d.marginBottomMm} onChange={(e) => set({ marginBottomMm: mm(e.target.value, 5, 60) })} />
              <Field label={t('reports.marginSide')} type="number" min={5} max={30} value={d.marginSideMm} onChange={(e) => set({ marginSideMm: mm(e.target.value, 5, 30) })} />
            </div>
            <p className="field-hint" style={{ margin: 0 }}>{t('reports.marginsHint')}</p>
          </Section>

          <Section title={t('reports.sectionResults')}>
            <SelectField label={t('reports.tableStyle')} value={d.tableStyle} onChange={(e) => set({ tableStyle: e.target.value as TableStyle })}>
              {(['lines', 'zebra', 'boxed'] as const).map((x) => <option key={x} value={x}>{t(`reports.tableStyles.${x}`)}</option>)}
            </SelectField>
            <Switch checked={d.showRangeBars} onChange={(v) => set({ showRangeBars: v })} label={t('reports.showRangeBars')} hint={t('reports.showRangeBarsHint')} />
            <Switch checked={d.showSingleParameterCharts} onChange={(v) => set({ showSingleParameterCharts: v })} label={t('reports.showSingleParameterCharts')} hint={t('reports.showSingleParameterChartsHint')} />
            <Switch checked={d.colourFlags} onChange={(v) => set({ colourFlags: v })} label={t('reports.colourFlags')} hint={t('reports.colourFlagsHint')} />
            <Switch checked={d.showFlagWords} onChange={(v) => set({ showFlagWords: v })} label={t('reports.showFlagWords')} />
          </Section>

          <Section title={t('reports.blocksTitle')}>
            <p className="field-hint" style={{ margin: 0 }}>{t('reports.blocksHint')}</p>
            <ol className="block-order">
              {d.blocks.map((b, i) => (
                <li key={b}>
                  <span className="num-badge num" aria-hidden>{i + 1}</span>
                  <span>{t(`reports.block.${b}`)}</span>
                  <Button size="sm" variant="ghost" iconOnly icon={<ArrowUp size={14} />} aria-label={`${t('reports.moveUp')}: ${t(`reports.block.${b}`)}`} disabled={i === 0} onClick={() => move(i, -1)} />
                  <Button size="sm" variant="ghost" iconOnly icon={<ArrowDown size={14} />} aria-label={`${t('reports.moveDown')}: ${t(`reports.block.${b}`)}`} disabled={i === d.blocks.length - 1} onClick={() => move(i, 1)} />
                </li>
              ))}
            </ol>
          </Section>
        </div>
      </Card>

      <div className="design-preview">
        <Card title={t('reports.livePreview')} subtitle={t('reports.onePageNote')} flush>
          <div className="report-stage">
            <div className="report-stage-scale"><ReportDocument view={sample} images={images} design={d} /></div>
          </div>
        </Card>
      </div>

      <ConfirmDialog
        open={confirmDelete}
        tone="danger"
        icon={<Trash2 size={20} />}
        title={t('reports.deleteDesignTitle')}
        text={t('reports.deleteDesignText')}
        confirmLabel={t('reports.deleteDesign')}
        loading={remove.isPending}
        onCancel={() => setConfirmDelete(false)}
        onConfirm={() => edit.id !== null && remove.mutate(edit.id)}
      />
    </div>
  )
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="design-section">
      <h4>{title}</h4>
      <div className="form-stack">{children}</div>
    </section>
  )
}

/** A tiny drawing of each ready style, so the choice is visual. */
function PresetThumb({ style, accent }: { style: DesignStyle; accent: string }) {
  return (
    <span className={`preset-thumb thumb-${style}`} style={{ ['--a' as string]: accent }} aria-hidden>
      <span className="pt-head" />
      <span className="pt-box" />
      <span className="pt-dept" />
      <span className="pt-row" />
      <span className="pt-row" />
      <span className="pt-row" />
    </span>
  )
}
