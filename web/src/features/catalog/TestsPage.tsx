import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { FlaskConical, Plus } from 'lucide-react'
import { api } from '../../lib/api'
import { formatMoney } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { SearchInput } from '../../components/ui/Controls'
import { DataTable, type Column, type SortState } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import type { Paged } from '../admin/adminApi'
import { catalogKeys, localName, useCategories, type TestListItem } from './catalogApi'
import { TestFormModal } from './TestFormModal'
import './catalog.css'

const PAGE_SIZE = 25

export function TestsPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { can } = useSession()
  const [search, setSearch] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [status, setStatus] = useState<'all' | 'active' | 'inactive'>('active')
  const [sort, setSort] = useState<SortState>({ sort: 'name' })
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const categories = useCategories()

  const params = { search, categoryId: categoryId || undefined, isActive: status === 'all' ? undefined : status === 'active', sort: sort.sort, desc: sort.desc, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({
    queryKey: catalogKeys.tests(params),
    queryFn: () => api<Paged<TestListItem>>('/api/catalog/tests', { query: params }),
    placeholderData: keepPreviousData,
  })

  const columns: Column<TestListItem>[] = [
    { key: 'code', header: t('common.code'), sortKey: 'code', width: 100, render: (x) => <span className="num cell-strong">{x.code}</span> },
    {
      key: 'name',
      header: t('catalog.test'),
      sortKey: 'name',
      render: (x) => (
        <span>
          <span className="cell-strong" dir="auto">{localName(x, i18n.language)}</span>
          {x.shortName && <span className="cell-sub num">{x.shortName}</span>}
        </span>
      ),
    },
    {
      key: 'cat',
      header: t('catalog.category'),
      render: (x) => (
        <span>
          {localName({ nameEn: x.categoryName, nameUr: x.categoryNameUr }, i18n.language)}
          <span className="cell-sub">{localName({ nameEn: x.departmentName, nameUr: x.departmentNameUr }, i18n.language)}</span>
        </span>
      ),
    },
    { key: 'sample', header: t('catalog.sampleType'), render: (x) => localName({ nameEn: x.sampleTypeName, nameUr: x.sampleTypeNameUr }, i18n.language) },
    { key: 'tat', header: t('catalog.tat'), render: (x) => t('catalog.tatHours', { count: x.turnaroundHours }) },
    { key: 'price', header: t('catalog.price'), sortKey: 'price', align: 'end', render: (x) => <span className="num">{formatMoney(x.price)}</span> },
    {
      key: 'ranges',
      header: t('catalog.ranges'),
      render: (x) =>
        x.parameterCount === 0 ? (
          <Badge>{t('catalog.noParameters')}</Badge>
        ) : (
          <span className="badge-list">
            <Badge tone={x.parametersWithApprovedRanges === x.parameterCount ? 'success' : 'warning'} dot>
              {t('catalog.rangesApproved', { approved: x.parametersWithApprovedRanges, total: x.parameterCount })}
            </Badge>
            {x.draftRangeCount > 0 && <Badge tone="info">{t('catalog.draftsWaiting', { count: x.draftRangeCount })}</Badge>}
          </span>
        ),
    },
    { key: 'status', header: t('common.status'), render: (x) => (x.isActive ? <Badge tone="success" dot>{t('common.active')}</Badge> : <Badge dot>{t('common.inactive')}</Badge>) },
  ]

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <h2>{t('catalog.testsTitle')}</h2>
          <p>{t('catalog.testsSubtitle')}</p>
        </div>
        {can('catalog.manage') && <Button variant="primary" icon={<Plus size={16} />} onClick={() => setCreating(true)}>{t('catalog.newTest')}</Button>}
      </div>
      <section className="card">
        <div className="card-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('catalog.searchTests')} />
          <select className="select select-sm" value={categoryId} onChange={(e) => { setCategoryId(e.target.value); setPage(1) }} aria-label={t('catalog.category')}>
            <option value="">{t('catalog.anyCategory')}</option>
            {categories.data?.map((c) => <option key={c.id} value={c.id}>{localName(c, i18n.language)}</option>)}
          </select>
          <select className="select select-sm" value={status} onChange={(e) => { setStatus(e.target.value as typeof status); setPage(1) }} aria-label={t('common.status')}>
            <option value="active">{t('common.active')}</option>
            <option value="inactive">{t('common.inactive')}</option>
            <option value="all">{t('common.all')}</option>
          </select>
        </div>
        <DataTable
          columns={columns}
          rows={data?.items}
          rowKey={(x) => x.id}
          loading={isLoading}
          sort={sort}
          onSort={(s) => { setSort(s); setPage(1) }}
          onRowClick={(x) => navigate(`/catalog/tests/${x.id}`)}
          page={page}
          pageSize={PAGE_SIZE}
          total={data?.total}
          onPage={setPage}
          empty={search || categoryId ? undefined : <EmptyState icon={<FlaskConical size={22} />} title={t('catalog.emptyTitle')} text={t('catalog.emptyText')} />}
        />
      </section>
      {creating && <TestFormModal test={null} onClose={() => setCreating(false)} onSaved={(id) => navigate(`/catalog/tests/${id}`)} />}
    </div>
  )
}
