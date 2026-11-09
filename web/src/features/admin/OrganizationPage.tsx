import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, Building2, Layers, Mail, Pencil, Phone, Plus, Star } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Switch, Tabs, TextAreaField } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import { keys, localName, nullIfBlank, useBranches, useDepartments, type BranchDto, type DepartmentDto, type OrganizationDto } from './adminApi'

type Tab = 'organization' | 'branches' | 'departments'

export function OrganizationPage() {
  const { t } = useTranslation()
  const [tab, setTab] = useState<Tab>('branches')
  const branches = useBranches()
  const departments = useDepartments()

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <h2>{t('org.title')}</h2>
          <p>{t('org.subtitle')}</p>
        </div>
      </div>
      <div style={{ marginBottom: 16 }}>
        <Tabs
          value={tab}
          onChange={setTab}
          tabs={[
            { key: 'branches', label: t('org.tabBranches'), count: branches.data?.length },
            { key: 'departments', label: t('org.tabDepartments'), count: departments.data?.length },
            { key: 'organization', label: t('org.tabOrganization') },
          ]}
        />
      </div>
      {tab === 'organization' && <OrganizationForm />}
      {tab === 'branches' && <BranchesTab />}
      {tab === 'departments' && <DepartmentsTab />}
    </div>
  )
}

/* ---------------- Organisation profile ---------------- */
function OrganizationForm() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const { data, isLoading } = useQuery({ queryKey: keys.organization, queryFn: () => api<OrganizationDto>('/api/organization') })
  const [form, setForm] = useState<OrganizationDto | null>(null)
  const [saving, setSaving] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  useEffect(() => setForm(data ?? null), [data])

  if (isLoading || !form) return <Card><div className="form-stack"><Skeleton height={40} /><Skeleton height={40} /><Skeleton height={80} /></div></Card>

  const set = (k: keyof OrganizationDto, v: string) => setForm((f) => (f ? { ...f, [k]: v } : f))
  const dirty = JSON.stringify(form) !== JSON.stringify(data)

  const save = async () => {
    if (!form.nameEn.trim()) return setFailure(t('common.required'))
    setSaving(true)
    setFailure(null)
    try {
      await api('/api/organization', {
        method: 'PUT',
        body: { nameEn: form.nameEn.trim(), nameUr: nullIfBlank(form.nameUr), phone: nullIfBlank(form.phone), email: nullIfBlank(form.email), addressEn: nullIfBlank(form.addressEn), addressUr: nullIfBlank(form.addressUr), rowVersion: form.rowVersion },
      })
      toast.show({ tone: 'success', title: t('common.saved') })
      await qc.invalidateQueries({ queryKey: keys.organization })
      await qc.invalidateQueries({ queryKey: ['session'] })
    } catch (e) {
      setFailure(errorMessage(t, e))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Card
      title={t('org.tabOrganization')}
      actions={<Button variant="primary" loading={saving} disabled={!dirty} onClick={() => void save()}>{t('common.save')}</Button>}
    >
      {failure && <ErrorBanner text={failure} />}
      <div className="form-grid" style={{ maxWidth: 900 }}>
        <Field label={t('org.orgCode')} value={form.code} dir="ltr" disabled />
        <span />
        <Field label={t('common.nameEn')} required value={form.nameEn} onChange={(e) => set('nameEn', e.target.value)} maxLength={200} />
        <Field label={t('common.nameUr')} dir="rtl" lang="ur" className="urdu-input" value={form.nameUr ?? ''} onChange={(e) => set('nameUr', e.target.value)} maxLength={200} />
        <Field label={t('common.phone')} type="tel" dir="ltr" icon={<Phone size={16} />} value={form.phone ?? ''} onChange={(e) => set('phone', e.target.value)} maxLength={30} />
        <Field label={t('common.email')} type="email" dir="ltr" icon={<Mail size={16} />} value={form.email ?? ''} onChange={(e) => set('email', e.target.value)} maxLength={256} />
        <TextAreaField label={t('common.addressEn')} rows={3} value={form.addressEn ?? ''} onChange={(e) => set('addressEn', e.target.value)} maxLength={500} />
        <TextAreaField label={t('common.addressUr')} rows={3} dir="rtl" lang="ur" className="input textarea urdu-input" value={form.addressUr ?? ''} onChange={(e) => set('addressUr', e.target.value)} maxLength={500} />
      </div>
    </Card>
  )
}

/* ---------------- Branches ---------------- */
function BranchesTab() {
  const { t, i18n } = useTranslation()
  const { data, isLoading } = useBranches()
  const [editing, setEditing] = useState<BranchDto | 'new' | null>(null)

  const columns: Column<BranchDto>[] = [
    {
      key: 'name',
      header: t('common.name'),
      render: (b) => (
        <span>
          <span className="cell-strong" dir="auto">{localName(b, i18n.language)}</span>
          <span className="cell-sub num" style={{ textAlign: 'start' }}>{b.code}</span>
        </span>
      ),
    },
    { key: 'main', header: '', render: (b) => b.isMain && <Badge tone="primary"><Star size={11} aria-hidden /> {t('org.main')}</Badge> },
    { key: 'phone', header: t('common.phone'), render: (b) => <span className="num">{b.phone ?? '—'}</span> },
    { key: 'staff', header: t('org.staff'), align: 'end', render: (b) => <span className="num">{b.userCount}</span> },
    { key: 'status', header: t('common.status'), render: (b) => (b.isActive ? <Badge tone="success" dot>{t('common.active')}</Badge> : <Badge dot>{t('common.inactive')}</Badge>) },
    {
      key: 'edit',
      header: '',
      align: 'end',
      width: 56,
      render: (b) => (
        <button type="button" className="icon-btn" aria-label={`${t('common.edit')} ${b.nameEn}`} onClick={() => setEditing(b)}>
          <Pencil size={16} />
        </button>
      ),
    },
  ]

  return (
    <section className="card">
      <header className="card-header">
        <h3 className="card-title">{t('org.tabBranches')}</h3>
        <Button variant="primary" size="sm" icon={<Plus size={15} />} onClick={() => setEditing('new')}>{t('org.newBranch')}</Button>
      </header>
      <DataTable
        columns={columns}
        rows={data}
        rowKey={(b) => b.id}
        loading={isLoading}
        onRowClick={setEditing}
        empty={<EmptyState icon={<Building2 size={22} />} title={t('org.emptyBranches')} />}
      />
      {editing && <BranchModal branch={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </section>
  )
}

function BranchModal({ branch, onClose }: { branch: BranchDto | null; onClose: () => void }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const [f, setF] = useState({
    code: branch?.code ?? '',
    nameEn: branch?.nameEn ?? '',
    nameUr: branch?.nameUr ?? '',
    phone: branch?.phone ?? '',
    email: branch?.email ?? '',
    addressEn: branch?.addressEn ?? '',
    addressUr: branch?.addressUr ?? '',
    isActive: branch?.isActive ?? true,
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }))

  const save = async () => {
    const e: Record<string, string> = {}
    if (!branch && !/^[A-Za-z0-9_-]{1,20}$/.test(f.code)) e.code = t('common.codeHint')
    if (!f.nameEn.trim()) e.nameEn = t('common.required')
    setErrors(e)
    if (Object.keys(e).length) return
    setSaving(true)
    setFailure(null)
    const body = { nameEn: f.nameEn.trim(), nameUr: nullIfBlank(f.nameUr), phone: nullIfBlank(f.phone), email: nullIfBlank(f.email), addressEn: nullIfBlank(f.addressEn), addressUr: nullIfBlank(f.addressUr) }
    try {
      if (branch) await api(`/api/organization/branches/${branch.id}`, { method: 'PUT', body: { ...body, isActive: f.isActive, rowVersion: branch.rowVersion } })
      else await api('/api/organization/branches', { method: 'POST', body: { ...body, code: f.code } })
      toast.show({ tone: 'success', title: branch ? t('common.saved') : t('common.created') })
      await qc.invalidateQueries({ queryKey: ['admin'] })
      await qc.invalidateQueries({ queryKey: ['session'] })
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
      title={branch ? t('org.editBranch') : t('org.newBranch')}
      footer={<><Button onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={saving} onClick={() => void save()}>{branch ? t('common.save') : t('common.create')}</Button></>}
    >
      {failure && <ErrorBanner text={failure} />}
      <form className="form-grid" onSubmit={(e) => { e.preventDefault(); void save() }} noValidate>
        <Field label={t('org.branchCode')} hint={!branch ? t('common.codeHint') : undefined} required={!branch} disabled={!!branch} dir="ltr" value={f.code} error={errors.code} maxLength={20} onChange={(e) => set('code', e.target.value.toUpperCase())} />
        <Field label={t('common.phone')} type="tel" dir="ltr" value={f.phone} maxLength={30} onChange={(e) => set('phone', e.target.value)} />
        <Field label={t('common.nameEn')} required value={f.nameEn} error={errors.nameEn} maxLength={200} onChange={(e) => set('nameEn', e.target.value)} />
        <Field label={t('common.nameUr')} dir="rtl" lang="ur" className="urdu-input" value={f.nameUr} maxLength={200} onChange={(e) => set('nameUr', e.target.value)} />
        <div className="span-2"><Field label={t('common.email')} type="email" dir="ltr" value={f.email} maxLength={256} onChange={(e) => set('email', e.target.value)} /></div>
        <TextAreaField label={t('common.addressEn')} rows={2} value={f.addressEn} maxLength={500} onChange={(e) => set('addressEn', e.target.value)} />
        <TextAreaField label={t('common.addressUr')} rows={2} dir="rtl" lang="ur" className="input textarea urdu-input" value={f.addressUr} maxLength={500} onChange={(e) => set('addressUr', e.target.value)} />
        {branch && (
          <div className="span-2">
            <Switch checked={f.isActive} disabled={branch.isMain} onChange={(v) => set('isActive', v)} label={t('common.active')} hint={branch.isMain ? t('errors.main_branch_required') : t('org.branchActiveHint')} />
          </div>
        )}
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}

/* ---------------- Departments ---------------- */
function DepartmentsTab() {
  const { t, i18n } = useTranslation()
  const { data, isLoading } = useDepartments()
  const [editing, setEditing] = useState<DepartmentDto | 'new' | null>(null)

  const columns: Column<DepartmentDto>[] = [
    { key: 'order', header: '#', width: 60, render: (d) => <span className="num" style={{ color: 'var(--muted)' }}>{d.sortOrder}</span> },
    { key: 'name', header: t('common.name'), render: (d) => <span className="cell-strong" dir="auto">{localName(d, i18n.language)}</span> },
    { key: 'code', header: t('common.code'), render: (d) => <span className="num">{d.code}</span> },
    { key: 'status', header: t('common.status'), render: (d) => (d.isActive ? <Badge tone="success" dot>{t('common.active')}</Badge> : <Badge dot>{t('common.inactive')}</Badge>) },
    { key: 'edit', header: '', align: 'end', width: 56, render: (d) => <button type="button" className="icon-btn" aria-label={`${t('common.edit')} ${d.nameEn}`} onClick={() => setEditing(d)}><Pencil size={16} /></button> },
  ]

  return (
    <section className="card">
      <header className="card-header">
        <h3 className="card-title">{t('org.tabDepartments')}</h3>
        <Button variant="primary" size="sm" icon={<Plus size={15} />} onClick={() => setEditing('new')}>{t('org.newDepartment')}</Button>
      </header>
      <DataTable
        columns={columns}
        rows={data}
        rowKey={(d) => d.id}
        loading={isLoading}
        onRowClick={setEditing}
        empty={<EmptyState icon={<Layers size={22} />} title={t('org.emptyDepartments')} text={t('org.emptyDepartmentsText')} />}
      />
      {editing && <DepartmentModal dept={editing === 'new' ? null : editing} nextOrder={((data?.at(-1)?.sortOrder ?? 0) + 10)} onClose={() => setEditing(null)} />}
    </section>
  )
}

function DepartmentModal({ dept, nextOrder, onClose }: { dept: DepartmentDto | null; nextOrder: number; onClose: () => void }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const [f, setF] = useState({ code: dept?.code ?? '', nameEn: dept?.nameEn ?? '', nameUr: dept?.nameUr ?? '', sortOrder: String(dept?.sortOrder ?? nextOrder), isActive: dept?.isActive ?? true })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }))

  const save = async () => {
    const e: Record<string, string> = {}
    if (!dept && !/^[A-Za-z0-9_-]{1,20}$/.test(f.code)) e.code = t('common.codeHint')
    if (!f.nameEn.trim()) e.nameEn = t('common.required')
    const order = Number(f.sortOrder)
    if (!Number.isInteger(order) || order < 0 || order > 10000) e.sortOrder = t('common.required')
    setErrors(e)
    if (Object.keys(e).length) return
    setSaving(true)
    setFailure(null)
    const body = { nameEn: f.nameEn.trim(), nameUr: nullIfBlank(f.nameUr), sortOrder: order }
    try {
      if (dept) await api(`/api/organization/departments/${dept.id}`, { method: 'PUT', body: { ...body, isActive: f.isActive, rowVersion: dept.rowVersion } })
      else await api('/api/organization/departments', { method: 'POST', body: { ...body, code: f.code } })
      toast.show({ tone: 'success', title: dept ? t('common.saved') : t('common.created') })
      await qc.invalidateQueries({ queryKey: keys.departments })
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
      size="sm"
      onClose={onClose}
      title={dept ? t('org.editDepartment') : t('org.newDepartment')}
      footer={<><Button onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={saving} onClick={() => void save()}>{dept ? t('common.save') : t('common.create')}</Button></>}
    >
      {failure && <ErrorBanner text={failure} />}
      <form className="form-stack" onSubmit={(e) => { e.preventDefault(); void save() }} noValidate>
        <Field label={t('org.departmentCode')} hint={!dept ? t('common.codeHint') : undefined} required={!dept} disabled={!!dept} dir="ltr" value={f.code} error={errors.code} maxLength={20} onChange={(e) => set('code', e.target.value.toUpperCase())} />
        <Field label={t('common.nameEn')} required value={f.nameEn} error={errors.nameEn} maxLength={200} onChange={(e) => set('nameEn', e.target.value)} />
        <Field label={t('common.nameUr')} dir="rtl" lang="ur" className="urdu-input" value={f.nameUr} maxLength={200} onChange={(e) => set('nameUr', e.target.value)} />
        <Field label={t('common.sortOrder')} type="number" inputMode="numeric" min={0} max={10000} dir="ltr" value={f.sortOrder} error={errors.sortOrder} onChange={(e) => set('sortOrder', e.target.value)} />
        {dept && <Switch checked={f.isActive} onChange={(v) => set('isActive', v)} label={t('common.active')} />}
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}

function ErrorBanner({ text }: { text: string }) {
  return (
    <div className="banner tone-danger" role="alert" style={{ marginBottom: 16 }}>
      <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
      <span>{text}</span>
    </div>
  )
}
