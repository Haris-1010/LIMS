import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, Pencil, Plus, Tags } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Switch } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/Toast'
import { catalogKeys, localName, useCategories, type CategoryDto } from './catalogApi'

export function CategoriesPage() {
  const { t, i18n } = useTranslation()
  const { can } = useSession()
  const { data, isLoading } = useCategories()
  const [editing, setEditing] = useState<CategoryDto | 'new' | null>(null)
  const canManage = can('catalog.manage')

  const columns: Column<CategoryDto>[] = [
    { key: 'order', header: '#', width: 60, render: (c) => <span className="num" style={{ color: 'var(--muted)' }}>{c.sortOrder}</span> },
    { key: 'name', header: t('common.name'), render: (c) => <span><span className="cell-strong" dir="auto">{localName(c, i18n.language)}</span><span className="cell-sub num">{c.code}</span></span> },
    { key: 'tests', header: t('catalog.testsCount'), align: 'end', render: (c) => <span className="num">{c.testCount}</span> },
    { key: 'status', header: t('common.status'), render: (c) => (c.isActive ? <Badge tone="success" dot>{t('common.active')}</Badge> : <Badge dot>{t('common.inactive')}</Badge>) },
    ...(canManage ? [{ key: 'edit', header: '', align: 'end' as const, width: 56, render: (c: CategoryDto) => <button type="button" className="icon-btn" aria-label={`${t('common.edit')} ${c.nameEn}`} onClick={() => setEditing(c)}><Pencil size={16} /></button> }] : []),
  ]

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <h2>{t('catalog.categoriesTitle')}</h2>
          <p>{t('catalog.categoriesSubtitle')}</p>
        </div>
        {canManage && <Button variant="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>{t('catalog.newCategory')}</Button>}
      </div>
      <section className="card">
        <DataTable columns={columns} rows={data} rowKey={(c) => c.id} loading={isLoading} onRowClick={canManage ? setEditing : undefined}
          empty={<EmptyState icon={<Tags size={22} />} title={t('catalog.categoriesEmpty')} text={t('catalog.categoriesEmptyText')} />} />
      </section>
      {editing && <CategoryModal category={editing === 'new' ? null : editing} nextOrder={((data?.at(-1)?.sortOrder ?? 0) + 10)} onClose={() => setEditing(null)} />}
    </div>
  )
}

function CategoryModal({ category, nextOrder, onClose }: { category: CategoryDto | null; nextOrder: number; onClose: () => void }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const [f, setF] = useState({ code: category?.code ?? '', nameEn: category?.nameEn ?? '', nameUr: category?.nameUr ?? '', sortOrder: String(category?.sortOrder ?? nextOrder), isActive: category?.isActive ?? true })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }))

  const save = async () => {
    const e: Record<string, string> = {}
    if (!category && !/^[A-Za-z0-9_-]{1,20}$/.test(f.code)) e.code = t('common.codeHint')
    if (!f.nameEn.trim()) e.nameEn = t('common.required')
    setErrors(e)
    if (Object.keys(e).length) return
    setSaving(true)
    setFailure(null)
    const body = { code: category?.code ?? f.code, nameEn: f.nameEn.trim(), nameUr: f.nameUr.trim() || null, sortOrder: Number(f.sortOrder) || 0 }
    try {
      if (category) await api(`/api/catalog/categories/${category.id}`, { method: 'PUT', body: { ...body, isActive: f.isActive, rowVersion: category.rowVersion } })
      else await api('/api/catalog/categories', { method: 'POST', body })
      toast.show({ tone: 'success', title: category ? t('common.saved') : t('common.created') })
      await qc.invalidateQueries({ queryKey: catalogKeys.categories })
      onClose()
    } catch (err) {
      setFailure(errorMessage(t, err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal open size="sm" onClose={onClose} title={category ? t('catalog.editCategory') : t('catalog.newCategory')}
      footer={<><Button onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={saving} onClick={() => void save()}>{category ? t('common.save') : t('common.create')}</Button></>}>
      {failure && <div className="banner tone-danger" role="alert" style={{ marginBottom: 16 }}><AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} /><span>{failure}</span></div>}
      <form className="form-stack" onSubmit={(e) => { e.preventDefault(); void save() }} noValidate>
        <Field label={t('common.code')} required={!category} disabled={!!category} dir="ltr" hint={!category ? t('common.codeHint') : undefined} value={f.code} error={errors.code} maxLength={20} onChange={(e) => set('code', e.target.value.toUpperCase())} />
        <Field label={t('common.nameEn')} required autoFocus value={f.nameEn} error={errors.nameEn} maxLength={200} onChange={(e) => set('nameEn', e.target.value)} />
        <Field label={t('common.nameUr')} dir="rtl" lang="ur" className="urdu-input" value={f.nameUr} maxLength={200} onChange={(e) => set('nameUr', e.target.value)} />
        <Field label={t('common.sortOrder')} type="number" min={0} dir="ltr" value={f.sortOrder} onChange={(e) => set('sortOrder', e.target.value)} />
        {category && <Switch checked={f.isActive} onChange={(v) => set('isActive', v)} label={t('common.active')} />}
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}
