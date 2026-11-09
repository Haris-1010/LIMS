import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, Repeat } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Button } from '../../components/ui/Button'
import { FormSection, TextAreaField } from '../../components/ui/Controls'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/Toast'
import { formatAgeBand, formatLimits, fromDays, toDays, type AgeUnit, type ParameterDto, type RangeDto, type RangeSex } from './catalogApi'

const num = (s: string) => (s.trim() === '' ? null : Number(s))

/** Create or edit a DRAFT range. Values are entered by lab staff from their validated source. */
export function RangeFormModal({ parameter, range, replaces, onClose, onSaved }: {
  parameter: ParameterDto
  range: RangeDto | null
  replaces: RangeDto | null
  onClose: () => void
  onSaved: () => void
}) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const base = range ?? replaces
  const from = fromDays(base?.ageFromDays ?? 0)
  const to = base?.ageToDays != null ? fromDays(base.ageToDays) : { value: NaN, unit: 'y' as AgeUnit }
  const numeric = parameter.resultType === 1

  const [f, setF] = useState({
    sex: (base?.sex ?? 0) as RangeSex,
    fromValue: String(from.value),
    fromUnit: from.unit,
    toValue: Number.isNaN(to.value) ? '' : String(to.value),
    toUnit: to.unit,
    low: base?.low?.toString() ?? '',
    high: base?.high?.toString() ?? '',
    criticalLow: base?.criticalLow?.toString() ?? '',
    criticalHigh: base?.criticalHigh?.toString() ?? '',
    displayText: base?.displayText ?? '',
    source: range?.source ?? '',
    notes: range?.notes ?? '',
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => {
    setF((x) => ({ ...x, [k]: v }))
    setErrors((e) => ({ ...e, [k]: '' }))
  }

  const save = async () => {
    const e: Record<string, string> = {}
    const ageFrom = toDays(Number(f.fromValue) || 0, f.fromUnit)
    const ageTo = f.toValue.trim() ? toDays(Number(f.toValue), f.toUnit) : null
    if (ageTo !== null && ageTo <= ageFrom) e.toValue = t('errors.range_age_invalid')
    for (const k of ['low', 'high', 'criticalLow', 'criticalHigh'] as const)
      if (f[k].trim() && !Number.isFinite(Number(f[k]))) e[k] = t('common.required')
    const [low, high, cl, ch] = [num(f.low), num(f.high), num(f.criticalLow), num(f.criticalHigh)]
    if (low !== null && high !== null && low > high) e.high = t('errors.range_low_above_high')
    if (cl !== null && low !== null && cl > low) e.criticalLow = t('errors.range_critical_low_invalid')
    if (ch !== null && high !== null && ch < high) e.criticalHigh = t('errors.range_critical_high_invalid')
    if (numeric && low === null && high === null && !f.displayText.trim()) e.low = t('errors.range_empty')
    if (!numeric && !f.displayText.trim()) e.displayText = t('errors.range_text_required')
    setErrors(e)
    if (Object.keys(e).length) return

    setSaving(true)
    setFailure(null)
    const body = {
      sex: f.sex, ageFromDays: ageFrom, ageToDays: ageTo,
      low: numeric ? low : null, high: numeric ? high : null, criticalLow: numeric ? cl : null, criticalHigh: numeric ? ch : null,
      displayText: f.displayText.trim() || null, source: f.source.trim() || null, notes: f.notes.trim() || null,
      replacesRangeId: range ? range.replacesRangeId : replaces?.id ?? null,
    }
    try {
      if (range) await api(`/api/catalog/ranges/${range.id}`, { method: 'PUT', body: { ...body, rowVersion: range.rowVersion } })
      else await api(`/api/catalog/parameters/${parameter.id}/ranges`, { method: 'POST', body })
      toast.show({ tone: 'success', title: t('common.saved') })
      await qc.invalidateQueries({ queryKey: ['catalog'] })
      onSaved()
      onClose()
    } catch (err) {
      setFailure(errorMessage(t, err))
    } finally {
      setSaving(false)
    }
  }

  const unitSelect = (value: AgeUnit, onChange: (u: AgeUnit) => void, label: string) => (
    <select className="select age-unit" value={value} aria-label={label} onChange={(e) => onChange(e.target.value as AgeUnit)}>
      {(['d', 'm', 'y'] as const).map((u) => <option key={u} value={u}>{t(`ageUnit.${u}`)}</option>)}
    </select>
  )
  const unitSuffix = numeric && parameter.unitCode ? ` (${parameter.unitCode})` : ''

  return (
    <Modal
      open
      onClose={onClose}
      title={range ? t('catalog.editRangeTitle') : t('catalog.createRangeTitle')}
      subtitle={parameter.nameEn}
      footer={<><Button onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={saving} onClick={() => void save()}>{t('common.save')}</Button></>}
    >
      {failure && (
        <div className="banner tone-danger" role="alert" style={{ marginBottom: 16 }}>
          <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
          <span>{failure}</span>
        </div>
      )}
      {replaces && (
        <div className="banner" style={{ marginBottom: 16 }}>
          <Repeat size={16} aria-hidden style={{ flexShrink: 0 }} />
          <span>{t('catalog.replacesRange', { id: replaces.id })}: {t(`rangeSex.${replaces.sex}`)}, {formatAgeBand(t, replaces.ageFromDays, replaces.ageToDays)}, <span className="num">{formatLimits(replaces)}</span></span>
        </div>
      )}
      <form onSubmit={(e) => { e.preventDefault(); void save() }} noValidate>
        <FormSection title={t('catalog.ageBand')}>
          <div className="field" style={{ marginBottom: 12 }}>
            <span className="field-label">{t('catalog.sex')}</span>
            <div className="segmented" role="radiogroup" aria-label={t('catalog.sex')}>
              {([0, 1, 2] as RangeSex[]).map((s) => (
                <button key={s} type="button" role="radio" aria-checked={f.sex === s} aria-pressed={f.sex === s} onClick={() => set('sex', s)}>{t(`rangeSex.${s}`)}</button>
              ))}
            </div>
          </div>
          <div className="form-grid">
            <div className="age-input">
              <Field label={t('catalog.ageFrom')} type="number" min={0} dir="ltr" value={f.fromValue} onChange={(e) => set('fromValue', e.target.value)} />
              {unitSelect(f.fromUnit, (u) => set('fromUnit', u), t('catalog.ageFrom'))}
            </div>
            <div className="age-input">
              <Field label={t('catalog.ageTo')} hint={t('catalog.ageToHint')} type="number" min={0} dir="ltr" value={f.toValue} error={errors.toValue} onChange={(e) => set('toValue', e.target.value)} />
              {unitSelect(f.toUnit, (u) => set('toUnit', u), t('catalog.ageTo'))}
            </div>
          </div>
        </FormSection>

        <FormSection title={t('catalog.limits')}>
          <div className="form-grid">
            {numeric && (
              <>
                <Field label={t('catalog.low') + unitSuffix} type="number" step="any" dir="ltr" value={f.low} error={errors.low} onChange={(e) => set('low', e.target.value)} />
                <Field label={t('catalog.high') + unitSuffix} type="number" step="any" dir="ltr" value={f.high} error={errors.high} onChange={(e) => set('high', e.target.value)} />
                <Field label={t('catalog.criticalLow') + unitSuffix} type="number" step="any" dir="ltr" hint={t('catalog.criticalHint')} value={f.criticalLow} error={errors.criticalLow} onChange={(e) => set('criticalLow', e.target.value)} />
                <Field label={t('catalog.criticalHigh') + unitSuffix} type="number" step="any" dir="ltr" value={f.criticalHigh} error={errors.criticalHigh} onChange={(e) => set('criticalHigh', e.target.value)} />
              </>
            )}
            <div className="span-2">
              <Field label={t('catalog.displayText')} hint={t('catalog.displayTextHint')} value={f.displayText} error={errors.displayText} maxLength={200} onChange={(e) => set('displayText', e.target.value)} />
            </div>
          </div>
        </FormSection>

        <FormSection title={t('catalog.source')}>
          <div className="form-stack">
            <Field label={t('catalog.source')} hint={t('catalog.sourceHint')} value={f.source} maxLength={300} onChange={(e) => set('source', e.target.value)} />
            <TextAreaField label={t('catalog.notes')} rows={2} value={f.notes} maxLength={500} onChange={(e) => set('notes', e.target.value)} />
          </div>
        </FormSection>
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}
