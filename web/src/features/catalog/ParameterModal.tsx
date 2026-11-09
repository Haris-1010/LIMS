import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, Plus, X } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Button } from '../../components/ui/Button'
import { FormSection, SelectField, Switch } from '../../components/ui/Controls'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/Toast'
import { useUnits, type OptionDto, type ParameterDto, type ResultType } from './catalogApi'

export function ParameterModal({ testId, parameter, nextOrder, onClose }: { testId: number; parameter: ParameterDto | null; nextOrder: number; onClose: () => void }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const units = useUnits()
  const [f, setF] = useState({
    code: parameter?.code ?? '',
    nameEn: parameter?.nameEn ?? '',
    nameUr: parameter?.nameUr ?? '',
    resultType: (parameter?.resultType ?? 1) as ResultType,
    unitId: parameter?.unitId?.toString() ?? '',
    decimalPlaces: String(parameter?.decimalPlaces ?? 1),
    groupName: parameter?.groupName ?? '',
    sortOrder: String(parameter?.sortOrder ?? nextOrder),
    isActive: parameter?.isActive ?? true,
  })
  const [options, setOptions] = useState<OptionDto[]>(
    parameter?.options.length ? parameter.options : [{ valueEn: '', valueUr: '', isAbnormal: false }, { valueEn: '', valueUr: '', isAbnormal: false }],
  )
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }))
  const setOption = (i: number, patch: Partial<OptionDto>) => setOptions((list) => list.map((o, j) => (j === i ? { ...o, ...patch } : o)))

  const save = async () => {
    const e: Record<string, string> = {}
    if (!parameter && !/^[A-Za-z0-9_-]{1,20}$/.test(f.code)) e.code = t('common.codeHint')
    if (!f.nameEn.trim()) e.nameEn = t('common.required')
    const filled = options.filter((o) => o.valueEn.trim())
    if (f.resultType === 2 && filled.length < 2) e.options = t('errors.options_required')
    setErrors(e)
    if (Object.keys(e).length) return
    setSaving(true)
    setFailure(null)
    const body = {
      code: parameter?.code ?? f.code,
      nameEn: f.nameEn.trim(),
      nameUr: f.nameUr.trim() || null,
      resultType: f.resultType,
      unitId: f.resultType === 1 && f.unitId ? Number(f.unitId) : null,
      decimalPlaces: f.resultType === 1 ? Number(f.decimalPlaces) : 0,
      groupName: f.groupName.trim() || null,
      sortOrder: Number(f.sortOrder) || 0,
      options: f.resultType === 2 ? filled.map((o) => ({ valueEn: o.valueEn.trim(), valueUr: o.valueUr?.trim() || null, isAbnormal: o.isAbnormal })) : [],
    }
    try {
      if (parameter) await api(`/api/catalog/parameters/${parameter.id}`, { method: 'PUT', body: { ...body, isActive: f.isActive, rowVersion: parameter.rowVersion } })
      else await api(`/api/catalog/tests/${testId}/parameters`, { method: 'POST', body })
      toast.show({ tone: 'success', title: parameter ? t('common.saved') : t('common.created') })
      await qc.invalidateQueries({ queryKey: ['catalog'] })
      onClose()
    } catch (err) {
      setFailure(errorMessage(t, err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={parameter ? t('catalog.editParameterTitle') : t('catalog.createParameterTitle')}
      subtitle={parameter?.code}
      footer={<><Button onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={saving} onClick={() => void save()}>{parameter ? t('common.save') : t('common.add')}</Button></>}
    >
      {failure && (
        <div className="banner tone-danger" role="alert" style={{ marginBottom: 16 }}>
          <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
          <span>{failure}</span>
        </div>
      )}
      <form onSubmit={(e) => { e.preventDefault(); void save() }} noValidate>
        <FormSection title={t('catalog.sectionGeneral')}>
          <div className="form-grid">
            <Field label={t('common.nameEn')} required autoFocus value={f.nameEn} error={errors.nameEn} maxLength={200} onChange={(e) => set('nameEn', e.target.value)} />
            <Field label={t('common.nameUr')} dir="rtl" lang="ur" className="urdu-input" value={f.nameUr} maxLength={200} onChange={(e) => set('nameUr', e.target.value)} />
            <Field label={t('common.code')} required={!parameter} disabled={!!parameter} dir="ltr" value={f.code} error={errors.code} maxLength={20} onChange={(e) => set('code', e.target.value.toUpperCase())} />
            <SelectField label={t('catalog.resultTypeLabel')} value={f.resultType} onChange={(e) => set('resultType', Number(e.target.value) as ResultType)}>
              {([1, 2, 3, 4] as const).map((rt) => <option key={rt} value={rt}>{t(`resultType.${rt}`)}</option>)}
            </SelectField>
            {f.resultType === 1 && (
              <>
                <SelectField label={t('catalog.unit')} value={f.unitId} onChange={(e) => set('unitId', e.target.value)}>
                  <option value="">—</option>
                  {units.data?.map((u) => <option key={u.id} value={u.id}>{u.code} — {u.name}</option>)}
                </SelectField>
                <Field label={t('catalog.decimals')} type="number" min={0} max={6} dir="ltr" hint={t('catalog.decimalsHint')} value={f.decimalPlaces} onChange={(e) => set('decimalPlaces', e.target.value)} />
              </>
            )}
            <Field label={t('catalog.group')} hint={t('catalog.groupHint')} value={f.groupName} maxLength={100} onChange={(e) => set('groupName', e.target.value)} />
            <Field label={t('catalog.displayOrder')} type="number" min={0} dir="ltr" value={f.sortOrder} onChange={(e) => set('sortOrder', e.target.value)} />
          </div>
        </FormSection>

        {f.resultType === 2 && (
          <FormSection title={t('catalog.options')}>
            <p className="field-hint" style={{ marginBottom: 10 }}>{t('catalog.optionsHint')}</p>
            <div className="option-rows">
              {options.map((o, i) => (
                <div key={i} className="option-row">
                  <input className="input" aria-label={t('catalog.optionValue')} placeholder={t('catalog.optionValue')} value={o.valueEn} maxLength={100} onChange={(e) => setOption(i, { valueEn: e.target.value })} />
                  <input className="input urdu-input" dir="rtl" lang="ur" aria-label={t('catalog.optionValueUr')} placeholder={t('catalog.optionValueUr')} value={o.valueUr ?? ''} maxLength={100} onChange={(e) => setOption(i, { valueUr: e.target.value })} />
                  <label className="checkbox"><input type="checkbox" checked={o.isAbnormal} onChange={(e) => setOption(i, { isAbnormal: e.target.checked })} />{t('catalog.abnormal')}</label>
                  <button type="button" className="icon-btn" aria-label={t('common.delete')} disabled={options.length <= 2} onClick={() => setOptions((l) => l.filter((_, j) => j !== i))}><X size={15} /></button>
                </div>
              ))}
            </div>
            {errors.options && <span className="field-error" role="alert">{errors.options}</span>}
            <Button size="sm" variant="ghost" icon={<Plus size={14} />} onClick={() => setOptions((l) => [...l, { valueEn: '', valueUr: '', isAbnormal: false }])}>{t('catalog.addOption')}</Button>
          </FormSection>
        )}
        {parameter && <Switch checked={f.isActive} onChange={(v) => set('isActive', v)} label={t('common.active')} />}
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}
