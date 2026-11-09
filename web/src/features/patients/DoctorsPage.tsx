import { useState } from 'react'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, Pencil, Phone, Plus, Stethoscope } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { SearchInput, Switch } from '../../components/ui/Controls'
import { DataTable, type Column, type SortState } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/Toast'
import type { Paged } from '../admin/adminApi'
import { formatPhone, patientKeys, type DoctorDto } from './patientsApi'

const PAGE_SIZE = 25

export function DoctorsPage() {
  const { t, i18n } = useTranslation()
  const { can } = useSession()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<'all' | 'active' | 'inactive'>('active')
  const [sort, setSort] = useState<SortState>({ sort: 'name' })
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<DoctorDto | 'new' | null>(null)
  const canManage = can('doctors.manage')

  const params = { search, isActive: status === 'all' ? undefined : status === 'active', sort: sort.sort, desc: sort.desc, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({
    queryKey: patientKeys.doctors(params),
    queryFn: () => api<Paged<DoctorDto>>('/api/doctors', { query: params }),
    placeholderData: keepPreviousData,
  })

  const columns: Column<DoctorDto>[] = [
    { key: 'code', header: t('common.code'), sortKey: 'code', width: 90, render: (d) => <span className="num" style={{ color: 'var(--muted)' }}>{d.code}</span> },
    {
      key: 'name',
      header: t('doctors.doctor'),
      sortKey: 'name',
      render: (d) => (
        <span>
          <span className="cell-strong" dir="auto">{i18n.language === 'ur' && d.fullNameUr ? d.fullNameUr : d.fullName}</span>
          {d.qualification && <span className="cell-sub">{d.qualification}</span>}
        </span>
      ),
    },
    { key: 'specialty', header: t('doctors.specialty'), render: (d) => d.specialty ?? '—' },
    { key: 'hospital', header: t('doctors.hospital'), render: (d) => <span>{d.hospital ?? '—'}{d.city && <span className="cell-sub">{d.city}</span>}</span> },
    { key: 'phone', header: t('common.phone'), render: (d) => <span className="num">{formatPhone(d.phone)}</span> },
    { key: 'type', header: t('doctors.type'), render: (d) => (d.isInHouse ? <Badge tone="accent">{t('doctors.inHouse')}</Badge> : <Badge tone="primary">{t('doctors.referring')}</Badge>) },
    { key: 'status', header: t('common.status'), render: (d) => (d.isActive ? <Badge tone="success" dot>{t('common.active')}</Badge> : <Badge dot>{t('common.inactive')}</Badge>) },
    ...(canManage
      ? [{ key: 'edit', header: '', align: 'end' as const, width: 56, render: (d: DoctorDto) => <button type="button" className="icon-btn" aria-label={`${t('common.edit')} ${d.fullName}`} onClick={() => setEditing(d)}><Pencil size={16} /></button> }]
      : []),
  ]

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <h2>{t('doctors.title')}</h2>
          <p>{t('doctors.subtitle')}</p>
        </div>
        {canManage && <Button variant="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>{t('doctors.new')}</Button>}
      </div>
      <section className="card">
        <div className="card-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('doctors.searchPlaceholder')} />
          <select className="select select-sm" value={status} onChange={(e) => { setStatus(e.target.value as typeof status); setPage(1) }} aria-label={t('common.status')}>
            <option value="active">{t('common.active')}</option>
            <option value="inactive">{t('common.inactive')}</option>
            <option value="all">{t('common.all')}</option>
          </select>
        </div>
        <DataTable
          columns={columns}
          rows={data?.items}
          rowKey={(d) => d.id}
          loading={isLoading}
          sort={sort}
          onSort={(s) => { setSort(s); setPage(1) }}
          onRowClick={canManage ? setEditing : undefined}
          page={page}
          pageSize={PAGE_SIZE}
          total={data?.total}
          onPage={setPage}
          empty={search ? undefined : <EmptyState icon={<Stethoscope size={22} />} title={t('doctors.emptyTitle')} text={t('doctors.emptyText')} />}
        />
      </section>
      {editing && <DoctorModal doctor={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </div>
  )
}

function DoctorModal({ doctor, onClose }: { doctor: DoctorDto | null; onClose: () => void }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const [f, setF] = useState({
    code: doctor?.code ?? '',
    fullName: doctor?.fullName ?? '',
    fullNameUr: doctor?.fullNameUr ?? '',
    qualification: doctor?.qualification ?? '',
    specialty: doctor?.specialty ?? '',
    pmdcNo: doctor?.pmdcNo ?? '',
    phone: doctor?.phone ?? '',
    email: doctor?.email ?? '',
    hospital: doctor?.hospital ?? '',
    city: doctor?.city ?? '',
    isInHouse: doctor?.isInHouse ?? false,
    isActive: doctor?.isActive ?? true,
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }))
  const blank = (s: string) => (s.trim() ? s.trim() : null)

  const save = async () => {
    const e: Record<string, string> = {}
    if (!f.fullName.trim()) e.fullName = t('errors.name_required')
    if (!doctor && f.code && !/^[A-Za-z0-9_-]{1,20}$/.test(f.code)) e.code = t('common.codeHint')
    if (f.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.email)) e.email = t('common.invalidEmail')
    setErrors(e)
    if (Object.keys(e).length) return
    setSaving(true)
    setFailure(null)
    const body = {
      fullName: f.fullName.trim(), fullNameUr: blank(f.fullNameUr), qualification: blank(f.qualification), specialty: blank(f.specialty),
      pmdcNo: blank(f.pmdcNo), phone: blank(f.phone), email: blank(f.email), hospital: blank(f.hospital), city: blank(f.city), isInHouse: f.isInHouse,
    }
    try {
      if (doctor) await api(`/api/doctors/${doctor.id}`, { method: 'PUT', body: { ...body, isActive: f.isActive, rowVersion: doctor.rowVersion } })
      else await api('/api/doctors', { method: 'POST', body: { ...body, code: blank(f.code) } })
      toast.show({ tone: 'success', title: doctor ? t('common.saved') : t('common.created') })
      await qc.invalidateQueries({ queryKey: ['doctors'] })
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
      title={doctor ? t('doctors.editTitle') : t('doctors.createTitle')}
      subtitle={doctor?.code}
      footer={<><Button onClick={onClose}>{t('common.cancel')}</Button><Button variant="primary" loading={saving} onClick={() => void save()}>{doctor ? t('common.save') : t('common.create')}</Button></>}
    >
      {failure && (
        <div className="banner tone-danger" role="alert" style={{ marginBottom: 16 }}>
          <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
          <span>{failure}</span>
        </div>
      )}
      <form className="form-grid" onSubmit={(e) => { e.preventDefault(); void save() }} noValidate>
        <Field label={t('common.nameEn')} required autoFocus value={f.fullName} error={errors.fullName} maxLength={200} onChange={(e) => set('fullName', e.target.value)} />
        <Field label={t('common.nameUr')} dir="rtl" lang="ur" className="urdu-input" value={f.fullNameUr} maxLength={200} onChange={(e) => set('fullNameUr', e.target.value)} />
        <Field label={t('doctors.qualification')} value={f.qualification} maxLength={200} placeholder="MBBS, FCPS" onChange={(e) => set('qualification', e.target.value)} />
        <Field label={t('doctors.specialty')} value={f.specialty} maxLength={100} onChange={(e) => set('specialty', e.target.value)} />
        <Field label={t('doctors.hospital')} value={f.hospital} maxLength={200} onChange={(e) => set('hospital', e.target.value)} />
        <Field label={t('patients.city')} value={f.city} maxLength={100} onChange={(e) => set('city', e.target.value)} />
        <Field label={t('common.phone')} type="tel" dir="ltr" icon={<Phone size={16} />} value={f.phone} maxLength={20} onChange={(e) => set('phone', e.target.value)} />
        <Field label={t('common.email')} type="email" dir="ltr" value={f.email} error={errors.email} maxLength={256} onChange={(e) => set('email', e.target.value)} />
        <Field label={t('doctors.pmdc')} dir="ltr" value={f.pmdcNo} maxLength={30} onChange={(e) => set('pmdcNo', e.target.value)} />
        <Field label={t('common.code')} dir="ltr" disabled={!!doctor} hint={!doctor ? t('doctors.codeAuto') : undefined} value={f.code} error={errors.code} maxLength={20} onChange={(e) => set('code', e.target.value.toUpperCase())} />
        <div className="span-2 form-stack">
          <Switch checked={f.isInHouse} onChange={(v) => set('isInHouse', v)} label={t('doctors.inHouse')} hint={t('doctors.inHouseHint')} />
          {doctor && <Switch checked={f.isActive} onChange={(v) => set('isActive', v)} label={t('common.active')} />}
        </div>
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}
