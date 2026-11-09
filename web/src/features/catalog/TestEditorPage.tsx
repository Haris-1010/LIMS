import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, FlaskConical, ListPlus, Pencil, Ruler } from 'lucide-react'
import { api, ApiError } from '../../lib/api'
import { formatMoney } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Skeleton } from '../../components/ui/Skeleton'
import { useDepartments } from '../admin/adminApi'
import { catalogKeys, localName, useCategories, useSampleTypes, type ParameterDto, type TestDetail } from './catalogApi'
import { TestFormModal } from './TestFormModal'
import { ParameterModal } from './ParameterModal'
import { RangesModal } from './RangesModal'
import './catalog.css'

export function TestEditorPage() {
  const { t, i18n } = useTranslation()
  const { id } = useParams()
  const testId = Number(id)
  const { can } = useSession()
  const [editing, setEditing] = useState(false)
  const [param, setParam] = useState<ParameterDto | 'new' | null>(null)
  const [rangesFor, setRangesFor] = useState<ParameterDto | null>(null)
  const categories = useCategories()
  const departments = useDepartments()
  const sampleTypes = useSampleTypes()

  const { data: test, isLoading, error } = useQuery({
    queryKey: catalogKeys.test(testId),
    queryFn: () => api<TestDetail>(`/api/catalog/tests/${testId}`),
    enabled: Number.isInteger(testId),
  })

  if (isLoading) return <div className="page"><Skeleton height={140} radius={12} /><div style={{ height: 16 }} /><Skeleton height={300} radius={12} /></div>
  if (!test) {
    return (
      <div className="page">
        <Card>
          <EmptyState icon={<FlaskConical size={22} />} title={t('common.notFound')} text={error instanceof ApiError && error.status === 404 ? t('errors.not_found') : t('errors.server_error')}
            action={<Link to="/catalog/tests" className="btn btn-secondary"><ArrowLeft size={16} className="flip-rtl" /> {t('catalog.backToTests')}</Link>} />
        </Card>
      </div>
    )
  }

  const canManage = can('catalog.manage')
  const category = categories.data?.find((c) => c.id === test.categoryId)
  const department = departments.data?.find((d) => d.id === test.departmentId)
  const sample = sampleTypes.data?.find((s) => s.id === test.sampleTypeId)

  const columns: Column<ParameterDto>[] = [
    { key: 'order', header: '#', width: 50, render: (p) => <span className="num" style={{ color: 'var(--muted)' }}>{p.sortOrder}</span> },
    {
      key: 'name',
      header: t('catalog.parameter'),
      render: (p) => (
        <span>
          <span className="cell-strong" dir="auto">{localName(p, i18n.language)}</span>
          <span className="cell-sub num">{p.code}{p.groupName ? ` · ${p.groupName}` : ''}</span>
        </span>
      ),
    },
    { key: 'type', header: t('catalog.resultTypeLabel'), render: (p) => t(`resultType.${p.resultType}`) },
    { key: 'unit', header: t('catalog.unit'), render: (p) => (p.resultType === 1 ? <span className="num">{p.unitCode ?? '—'}</span> : p.options.length ? <span className="cell-sub" style={{ display: 'inline' }}>{p.options.map((o) => o.valueEn).join(' / ')}</span> : '—') },
    { key: 'dec', header: t('catalog.decimals'), align: 'center', render: (p) => (p.resultType === 1 ? <span className="num">{p.decimalPlaces}</span> : '—') },
    {
      key: 'ranges',
      header: t('catalog.ranges'),
      render: (p) => (
        <span className="badge-list">
          {p.approvedRangeCount > 0 ? <Badge tone="success" dot>{t('rangeStatus.1')} · {p.approvedRangeCount}</Badge> : <Badge tone="warning" dot>{t('catalog.noRanges')}</Badge>}
          {p.draftRangeCount > 0 && <Badge tone="info">{t('catalog.draftsWaiting', { count: p.draftRangeCount })}</Badge>}
        </span>
      ),
    },
    { key: 'status', header: t('common.status'), render: (p) => (p.isActive ? <Badge tone="success" dot>{t('common.active')}</Badge> : <Badge dot>{t('common.inactive')}</Badge>) },
    {
      key: 'actions',
      header: '',
      align: 'end',
      render: (p) => (
        <span className="row-actions">
          <Button size="sm" variant="ghost" icon={<Ruler size={14} />} onClick={() => setRangesFor(p)}>{t('catalog.manageRanges')}</Button>
          {canManage && <button type="button" className="icon-btn" aria-label={`${t('common.edit')} ${p.nameEn}`} onClick={() => setParam(p)}><Pencil size={15} /></button>}
        </span>
      ),
    },
  ]

  return (
    <div className="page">
      <Link to="/catalog/tests" className="back-link"><ArrowLeft size={15} className="flip-rtl" aria-hidden /> {t('catalog.backToTests')}</Link>

      <section className="card test-hero">
        <div className="test-hero-icon" aria-hidden><FlaskConical size={26} /></div>
        <div className="test-hero-main">
          <div className="test-hero-top">
            <h2 dir="auto">{localName(test, i18n.language)}</h2>
            <span className="patient-no num">{test.code}</span>
            {test.shortName && <Badge>{test.shortName}</Badge>}
            {!test.isActive && <Badge dot>{t('common.inactive')}</Badge>}
          </div>
          <dl className="test-facts">
            <div><dt>{t('catalog.category')}</dt><dd>{category ? localName(category, i18n.language) : '—'}</dd></div>
            <div><dt>{t('catalog.department')}</dt><dd>{department ? localName(department, i18n.language) : '—'}</dd></div>
            <div><dt>{t('catalog.sampleType')}</dt><dd>{sample ? localName(sample, i18n.language) : '—'}{test.sampleVolume ? ` · ${test.sampleVolume}` : ''}</dd></div>
            <div><dt>{t('catalog.tat')}</dt><dd>{t('catalog.tatHours', { count: test.turnaroundHours })}</dd></div>
            <div><dt>{t('catalog.price')}</dt><dd className="num">{formatMoney(test.price)}</dd></div>
          </dl>
        </div>
        {canManage && <Button icon={<Pencil size={16} />} onClick={() => setEditing(true)}>{t('common.edit')}</Button>}
      </section>

      <section className="card section-gap">
        <header className="card-header">
          <div>
            <h3 className="card-title">{t('catalog.parameters')}</h3>
            <p className="card-subtitle">{t('catalog.parametersHint')}</p>
          </div>
          {canManage && <Button variant="primary" size="sm" icon={<ListPlus size={15} />} onClick={() => setParam('new')}>{t('catalog.addParameter')}</Button>}
        </header>
        <DataTable
          columns={columns}
          rows={test.parameters}
          rowKey={(p) => p.id}
          empty={<EmptyState icon={<ListPlus size={22} />} title={t('catalog.noParamsTitle')} text={t('catalog.noParamsText')} />}
        />
      </section>

      {editing && <TestFormModal test={test} onClose={() => setEditing(false)} />}
      {param && (
        <ParameterModal
          testId={test.id}
          parameter={param === 'new' ? null : param}
          nextOrder={(test.parameters.at(-1)?.sortOrder ?? 0) + 10}
          onClose={() => setParam(null)}
        />
      )}
      {rangesFor && <RangesModal parameter={rangesFor} testId={test.id} onClose={() => setRangesFor(null)} />}
    </div>
  )
}
