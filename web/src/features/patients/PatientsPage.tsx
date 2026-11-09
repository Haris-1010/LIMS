import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { UserPlus, UserRound } from 'lucide-react'
import { api } from '../../lib/api'
import { formatDateTime } from '../../lib/format'
import { useSession } from '../../app/session'
import { Button } from '../../components/ui/Button'
import { SearchInput } from '../../components/ui/Controls'
import { DataTable, type Column, type SortState } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { formatAge, formatPhone, patientKeys, patientName, type PatientListItem } from './patientsApi'
import './patients.css'
import { PatientFormModal } from './PatientFormModal'
import type { Paged } from '../admin/adminApi'

const PAGE_SIZE = 25

export function PatientsPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { can } = useSession()
  const [params, setParams] = useSearchParams()
  const search = params.get('q') ?? ''
  const [sort, setSort] = useState<SortState>({})
  const [page, setPage] = useState(1)
  const [registering, setRegistering] = useState(false)

  const query = { search, sort: sort.sort, desc: sort.desc, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({
    queryKey: patientKeys.list(query),
    queryFn: () => api<Paged<PatientListItem>>('/api/patients', { query }),
    placeholderData: keepPreviousData,
  })

  const setSearch = (v: string) => {
    setParams(v ? { q: v } : {}, { replace: true })
    setPage(1)
  }

  /** A USB barcode scanner types the patient number and presses Enter: open the record directly. */
  const onEnter = async (v: string) => {
    if (!/^P-\d+$/i.test(v.trim())) return
    const res = await api<Paged<PatientListItem>>('/api/patients', { query: { search: v.trim(), pageSize: 2 } })
    const exact = res.items.filter((p) => p.patientNo.toUpperCase() === v.trim().toUpperCase())
    if (exact.length === 1) navigate(`/patients/${exact[0]!.id}`)
  }

  const columns: Column<PatientListItem>[] = [
    { key: 'no', header: t('patients.patientNo'), sortKey: 'patientNo', width: 130, render: (p) => <span className="num cell-strong">{p.patientNo}</span> },
    {
      key: 'name',
      header: t('patients.patient'),
      sortKey: 'name',
      render: (p) => (
        <span>
          <span className="cell-strong" dir="auto">{patientName(p, i18n.language)}</span>
          {p.guardianName && <span className="cell-sub" dir="auto">{p.guardianName}</span>}
        </span>
      ),
    },
    {
      key: 'age',
      header: t('patients.ageSex'),
      render: (p) => (
        <span>
          {formatAge(t, p.dateOfBirth)} · {t(`sex.short${p.sex}`)}
        </span>
      ),
    },
    { key: 'mobile', header: t('patients.mobile'), render: (p) => <span className="num">{formatPhone(p.mobile)}</span> },
    { key: 'city', header: t('patients.city'), render: (p) => p.city ?? '—' },
    { key: 'registered', header: t('patients.registered'), render: (p) => <span style={{ color: 'var(--muted)' }}>{formatDateTime(p.registeredAtUtc, i18n.language)}</span> },
  ]

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <h2>{t('patients.title')}</h2>
          <p>{t('patients.subtitle')}</p>
        </div>
        {can('patients.create') && (
          <Button variant="primary" icon={<UserPlus size={16} />} onClick={() => setRegistering(true)}>
            {t('patients.register')}
          </Button>
        )}
      </div>

      <section className="card">
        <div className="card-toolbar">
          <div className="search-wide" onKeyDown={(e) => e.key === 'Enter' && void onEnter((e.target as HTMLInputElement).value)}>
            <SearchInput value={search} onChange={setSearch} placeholder={t('patients.searchPlaceholder')} autoFocus />
          </div>
        </div>
        <DataTable
          columns={columns}
          rows={data?.items}
          rowKey={(p) => p.id}
          loading={isLoading}
          sort={sort}
          onSort={(s) => { setSort(s); setPage(1) }}
          onRowClick={(p) => navigate(`/patients/${p.id}`)}
          page={page}
          pageSize={PAGE_SIZE}
          total={data?.total}
          onPage={setPage}
          empty={
            search ? undefined : (
              <EmptyState
                icon={<UserRound size={22} />}
                title={t('patients.emptyTitle')}
                text={t('patients.emptyText')}
                action={can('patients.create') && <Button variant="primary" icon={<UserPlus size={16} />} onClick={() => setRegistering(true)}>{t('patients.register')}</Button>}
              />
            )
          }
        />
      </section>

      {registering && (
        <PatientFormModal
          patient={null}
          onClose={() => setRegistering(false)}
          onSaved={(id) => navigate(`/patients/${id}`)}
        />
      )}
    </div>
  )
}
