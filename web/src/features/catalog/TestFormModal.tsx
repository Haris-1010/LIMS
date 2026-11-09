import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, Lock } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { useSession } from '../../app/session'
import { Button } from '../../components/ui/Button'
import { CheckList, FormSection, SelectField, Switch, TextAreaField } from '../../components/ui/Controls'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/Toast'
import { useDepartments } from '../admin/adminApi'
import { localName, useCategories, useSampleTypes, type TestDetail } from './catalogApi'

const blank = (s: string) => (s.trim() ? s.trim() : null)

export function TestFormModal({ test, onClose, onSaved }: { test: TestDetail | null; onClose: () => void; onSaved?: (id: number) => void }) {
  const { t, i18n } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const { user, can } = useSession()
  const categories = useCategories()
  const departments = useDepartments()
  const sampleTypes = useSampleTypes()
  const canPrice = can('catalog.pricing')

  const [f, setF] = useState({
    code: test?.code ?? '',
    nameEn: test?.nameEn ?? '',
    nameUr: test?.nameUr ?? '',
    shortName: test?.shortName ?? '',
    categoryId: test?.categoryId.toString() ?? '',
    departmentId: test?.departmentId.toString() ?? '',
    sampleTypeId: test?.sampleTypeId.toString() ?? '',
    sampleVolume: test?.sampleVolume ?? '',
    collectionNotes: test?.collectionNotes ?? '',
    method: test?.method ?? '',
    turnaroundHours: String(test?.turnaroundHours ?? 24),
    price: String(test?.price ?? 0),
    reportNotes: test?.reportNotes ?? '',
    sortOrder: String(test?.sortOrder ?? 0),
    availableAllBranches: test?.availableAllBranches ?? true,
    branchIds: test?.branchIds ?? [],
    isActive: test?.isActive ?? true,
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
    if (!test && !/^[A-Za-z0-9_-]{1,20}$/.test(f.code)) e.code = t('common.codeHint')
    if (!f.nameEn.trim()) e.nameEn = t('common.required')
    if (!f.categoryId) e.categoryId = t('errors.category_invalid')
    if (!f.departmentId) e.departmentId = t('errors.department_invalid')
    if (!f.sampleTypeId) e.sampleTypeId = t('errors.sample_type_invalid')
    const price = Number(f.price)
    if (!Number.isFinite(price) || price < 0) e.price = t('common.required')
    const tat = Number(f.turnaroundHours)
    if (!Number.isInteger(tat) || tat < 0) e.turnaroundHours = t('common.required')
    if (!f.availableAllBranches && f.branchIds.length === 0) e.branchIds = t('errors.branch_required')
    setErrors(e)
    if (Object.keys(e).length) return

    setSaving(true)
    setFailure(null)
    const body = {
      code: test?.code ?? f.code,
      nameEn: f.nameEn.trim(), nameUr: blank(f.nameUr), shortName: blank(f.shortName),
      categoryId: Number(f.categoryId), departmentId: Number(f.departmentId), sampleTypeId: Number(f.sampleTypeId),
      sampleVolume: blank(f.sampleVolume), collectionNotes: blank(f.collectionNotes), method: blank(f.method),
      turnaroundHours: tat, price, reportNotes: blank(f.reportNotes), sortOrder: Number(f.sortOrder) || 0,
      availableAllBranches: f.availableAllBranches, branchIds: f.availableAllBranches ? [] : f.branchIds,
    }
    try {
      const saved = test
        ? await api<TestDetail>(`/api/catalog/tests/${test.id}`, { method: 'PUT', body: { ...body, isActive: f.isActive, rowVersion: test.rowVersion } })
        : await api<TestDetail>('/api/catalog/tests', { method: 'POST', body })
      toast.show({ tone: 'success', title: test ? t('common.saved') : t('common.created') })
      await qc.invalidateQueries({ queryKey: ['catalog'] })
      onClose()
      onSaved?.(saved.id)
    } catch (err) {
      setFailure(errorMessage(t, err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal
      open
      size="lg"
      onClose={onClose}
      title={test ? t('catalog.editTestTitle') : t('catalog.createTestTitle')}
      subtitle={test?.code}
      footer={<><Button onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={saving} onClick={() => void save()}>{test ? t('common.save') : t('common.create')}</Button></>}
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
            <Field label={t('catalog.testCode')} required={!test} disabled={!!test} dir="ltr" hint={!test ? t('common.codeHint') : undefined} value={f.code} error={errors.code} maxLength={20} onChange={(e) => set('code', e.target.value.toUpperCase())} />
            <Field label={t('catalog.shortName')} dir="ltr" hint={t('catalog.shortNameHint')} value={f.shortName} maxLength={30} onChange={(e) => set('shortName', e.target.value)} />
            <Field label={t('common.nameEn')} required autoFocus value={f.nameEn} error={errors.nameEn} maxLength={200} onChange={(e) => set('nameEn', e.target.value)} />
            <Field label={t('common.nameUr')} dir="rtl" lang="ur" className="urdu-input" value={f.nameUr} maxLength={200} onChange={(e) => set('nameUr', e.target.value)} />
            <SelectField label={t('catalog.category')} required value={f.categoryId} error={errors.categoryId} onChange={(e) => set('categoryId', e.target.value)}>
              <option value="">—</option>
              {categories.data?.filter((c) => c.isActive || c.id.toString() === f.categoryId).map((c) => <option key={c.id} value={c.id}>{localName(c, i18n.language)}</option>)}
            </SelectField>
            <SelectField label={t('catalog.department')} required value={f.departmentId} error={errors.departmentId} onChange={(e) => set('departmentId', e.target.value)}>
              <option value="">—</option>
              {departments.data?.filter((d) => d.isActive || d.id.toString() === f.departmentId).map((d) => <option key={d.id} value={d.id}>{localName(d, i18n.language)}</option>)}
            </SelectField>
          </div>
        </FormSection>

        <FormSection title={t('catalog.sectionSample')}>
          <div className="form-grid">
            <SelectField label={t('catalog.sampleType')} required value={f.sampleTypeId} error={errors.sampleTypeId} onChange={(e) => set('sampleTypeId', e.target.value)}>
              <option value="">—</option>
              {sampleTypes.data?.map((s) => <option key={s.id} value={s.id}>{localName(s, i18n.language)}{s.capColour ? ` · ${s.capColour}` : ''}</option>)}
            </SelectField>
            <Field label={t('catalog.sampleVolume')} value={f.sampleVolume} maxLength={50} placeholder="3 mL" onChange={(e) => set('sampleVolume', e.target.value)} />
            <Field label={t('catalog.tatLabel')} type="number" inputMode="numeric" min={0} dir="ltr" required value={f.turnaroundHours} error={errors.turnaroundHours} onChange={(e) => set('turnaroundHours', e.target.value)} />
            <Field label={t('catalog.method')} value={f.method} maxLength={200} onChange={(e) => set('method', e.target.value)} />
            <div className="span-2">
              <TextAreaField label={t('catalog.collectionNotes')} rows={2} value={f.collectionNotes} maxLength={500} onChange={(e) => set('collectionNotes', e.target.value)} />
            </div>
          </div>
        </FormSection>

        <FormSection title={t('catalog.sectionPricing')}>
          <div className="form-stack">
            <div className="form-grid">
              <Field
                label={t('catalog.priceLabel')}
                type="number"
                inputMode="decimal"
                min={0}
                step="0.01"
                dir="ltr"
                disabled={!canPrice}
                icon={!canPrice ? <Lock size={14} /> : undefined}
                hint={!canPrice ? t('catalog.priceLocked') : undefined}
                value={f.price}
                error={errors.price}
                onChange={(e) => set('price', e.target.value)}
              />
              <Field label={t('catalog.displayOrder')} type="number" inputMode="numeric" min={0} dir="ltr" value={f.sortOrder} onChange={(e) => set('sortOrder', e.target.value)} />
            </div>
            <Switch checked={f.availableAllBranches} onChange={(v) => set('availableAllBranches', v)} label={t('catalog.allBranches')} />
            {!f.availableAllBranches && (
              <div className="field">
                <span className="field-label">{t('catalog.branchesLabel')}</span>
                <CheckList
                  items={user?.branches.map((b) => ({ id: b.id, label: localName(b, i18n.language), sub: b.code })) ?? []}
                  selected={f.branchIds}
                  onChange={(v) => set('branchIds', v as number[])}
                />
                {errors.branchIds && <span className="field-error" role="alert">{errors.branchIds}</span>}
              </div>
            )}
            {test && <Switch checked={f.isActive} onChange={(v) => set('isActive', v)} label={t('common.active')} />}
          </div>
        </FormSection>

        <FormSection title={t('catalog.sectionReport')}>
          <TextAreaField label={t('catalog.reportNotes')} rows={3} value={f.reportNotes} maxLength={2000} onChange={(e) => set('reportNotes', e.target.value)} />
        </FormSection>
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}
