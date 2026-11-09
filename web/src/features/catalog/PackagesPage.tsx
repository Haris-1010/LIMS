import { useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, Package, Pencil, Plus } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatMoney } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { FormSection, SearchInput, Switch, TextAreaField } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/Toast'
import type { Paged } from '../admin/adminApi'
import { catalogKeys, localName, type PackageDto, type TestListItem } from './catalogApi'
import './catalog.css'

const savingPct = (price: number, total: number) => (total > 0 ? Math.round(((total - price) / total) * 100) : 0)

export function PackagesPage() {
  const { t, i18n } = useTranslation()
  const { can } = useSession()
  const [editing, setEditing] = useState<PackageDto | 'new' | null>(null)
  const { data, isLoading } = useQuery({ queryKey: catalogKeys.packages, queryFn: () => api<PackageDto[]>('/api/catalog/packages') })
  const canEdit = can('catalog.manage') && can('catalog.pricing')

  const columns: Column<PackageDto>[] = [
    { key: 'name', header: t('catalog.package'), render: (p) => <span><span className="cell-strong" dir="auto">{localName(p, i18n.language)}</span><span className="cell-sub num">{p.code}</span></span> },
    { key: 'tests', header: t('catalog.includedTests'), render: (p) => <span className="cell-sub" style={{ fontSize: 'var(--text-sm)' }}>{p.tests.map((x) => x.code).join(', ')}</span> },
    { key: 'total', header: t('catalog.testsTotal'), align: 'end', render: (p) => <span className="num" style={{ color: 'var(--muted)' }}>{formatMoney(p.testsTotal)}</span> },
    {
      key: 'price',
      header: t('catalog.price'),
      align: 'end',
      render: (p) => (
        <span>
          <span className="num cell-strong">{formatMoney(p.price)}</span>
          {savingPct(p.price, p.testsTotal) > 0 && <span className="cell-sub">{t('catalog.saving', { pct: savingPct(p.price, p.testsTotal) })}</span>}
        </span>
      ),
    },
    { key: 'status', header: t('common.status'), render: (p) => (p.isActive ? <Badge tone="success" dot>{t('common.active')}</Badge> : <Badge dot>{t('common.inactive')}</Badge>) },
    ...(canEdit ? [{ key: 'edit', header: '', align: 'end' as const, width: 56, render: (p: PackageDto) => <button type="button" className="icon-btn" aria-label={`${t('common.edit')} ${p.nameEn}`} onClick={() => setEditing(p)}><Pencil size={16} /></button> }] : []),
  ]

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <h2>{t('catalog.packagesTitle')}</h2>
          <p>{t('catalog.packagesSubtitle')}</p>
        </div>
        {canEdit && <Button variant="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>{t('catalog.newPackage')}</Button>}
      </div>
      <section className="card">
        <DataTable columns={columns} rows={data} rowKey={(p) => p.id} loading={isLoading} onRowClick={canEdit ? setEditing : undefined}
          empty={<EmptyState icon={<Package size={22} />} title={t('catalog.packagesEmpty')} text={t('catalog.packagesEmptyText')} />} />
      </section>
      {editing && <PackageModal pkg={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </div>
  )
}

function PackageModal({ pkg, onClose }: { pkg: PackageDto | null; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const [f, setF] = useState({ code: pkg?.code ?? '', nameEn: pkg?.nameEn ?? '', nameUr: pkg?.nameUr ?? '', description: pkg?.description ?? '', price: String(pkg?.price ?? ''), isActive: pkg?.isActive ?? true })
  const [selected, setSelected] = useState<Map<number, { code: string; nameEn: string; price: number }>>(
    () => new Map((pkg?.tests ?? []).map((x) => [x.id, { code: x.code, nameEn: x.nameEn, price: x.price }])),
  )
  const [search, setSearch] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }))

  const tests = useQuery({
    queryKey: catalogKeys.tests({ picker: search }),
    queryFn: () => api<Paged<TestListItem>>('/api/catalog/tests', { query: { search, isActive: true, pageSize: 50 } }),
  })
  const total = useMemo(() => [...selected.values()].reduce((s, x) => s + x.price, 0), [selected])
  const price = Number(f.price)

  const toggle = (x: TestListItem) =>
    setSelected((m) => {
      const next = new Map(m)
      if (next.has(x.id)) next.delete(x.id)
      else next.set(x.id, { code: x.code, nameEn: x.nameEn, price: x.price })
      return next
    })

  const save = async () => {
    const e: Record<string, string> = {}
    if (!pkg && !/^[A-Za-z0-9_-]{1,20}$/.test(f.code)) e.code = t('common.codeHint')
    if (!f.nameEn.trim()) e.nameEn = t('common.required')
    if (!Number.isFinite(price) || price < 0 || f.price.trim() === '') e.price = t('common.required')
    if (selected.size < 2) e.tests = t('errors.package_tests_required')
    setErrors(e)
    if (Object.keys(e).length) return
    setSaving(true)
    setFailure(null)
    const body = { code: pkg?.code ?? f.code, nameEn: f.nameEn.trim(), nameUr: f.nameUr.trim() || null, description: f.description.trim() || null, price, testIds: [...selected.keys()] }
    try {
      if (pkg) await api(`/api/catalog/packages/${pkg.id}`, { method: 'PUT', body: { ...body, isActive: f.isActive, rowVersion: pkg.rowVersion } })
      else await api('/api/catalog/packages', { method: 'POST', body })
      toast.show({ tone: 'success', title: pkg ? t('common.saved') : t('common.created') })
      await qc.invalidateQueries({ queryKey: catalogKeys.packages })
      onClose()
    } catch (err) {
      setFailure(errorMessage(t, err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal open size="lg" onClose={onClose} title={pkg ? t('catalog.editPackage') : t('catalog.newPackage')} subtitle={pkg?.code}
      footer={<><Button onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={saving} onClick={() => void save()}>{pkg ? t('common.save') : t('common.create')}</Button></>}>
      {failure && <div className="banner tone-danger" role="alert" style={{ marginBottom: 16 }}><AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} /><span>{failure}</span></div>}
      <form onSubmit={(e) => { e.preventDefault(); void save() }} noValidate>
        <FormSection title={t('catalog.sectionGeneral')}>
          <div className="form-grid">
            <Field label={t('common.nameEn')} required autoFocus value={f.nameEn} error={errors.nameEn} maxLength={200} onChange={(e) => set('nameEn', e.target.value)} />
            <Field label={t('common.nameUr')} dir="rtl" lang="ur" className="urdu-input" value={f.nameUr} maxLength={200} onChange={(e) => set('nameUr', e.target.value)} />
            <Field label={t('common.code')} required={!pkg} disabled={!!pkg} dir="ltr" value={f.code} error={errors.code} maxLength={20} onChange={(e) => set('code', e.target.value.toUpperCase())} />
            <Field label={t('catalog.packagePrice')} type="number" min={0} step="0.01" dir="ltr" required value={f.price} error={errors.price} onChange={(e) => set('price', e.target.value)} />
            <div className="span-2"><TextAreaField label={t('common.description')} rows={2} value={f.description} maxLength={500} onChange={(e) => set('description', e.target.value)} /></div>
          </div>
        </FormSection>

        <FormSection title={t('catalog.pickTests')}>
          <div className="package-summary">
            <span>{t('catalog.selectedCount', { count: selected.size })}</span>
            <span>{t('catalog.testsTotal')}: <strong className="num">{formatMoney(total)}</strong></span>
            {Number.isFinite(price) && f.price.trim() !== '' && total > 0 && (
              <Badge tone={price <= total ? 'success' : 'warning'}>{t('catalog.saving', { pct: savingPct(price, total) })}</Badge>
            )}
          </div>
          <SearchInput value={search} onChange={setSearch} placeholder={t('catalog.searchTests')} />
          <div className="test-picker">
            {tests.data?.items.map((x) => (
              <label key={x.id} className={`check-card${selected.has(x.id) ? ' is-on' : ''}`}>
                <input type="checkbox" checked={selected.has(x.id)} onChange={() => toggle(x)} />
                <span style={{ flex: 1 }}>
                  <span className="check-label" dir="auto">{localName(x, i18n.language)}</span>
                  <span className="check-sub num">{x.code} · {formatMoney(x.price)}</span>
                </span>
              </label>
            ))}
          </div>
          {errors.tests && <span className="field-error" role="alert">{errors.tests}</span>}
        </FormSection>
        {pkg && <Switch checked={f.isActive} onChange={(v) => set('isActive', v)} label={t('common.active')} />}
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}
