import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { ClipboardList, Plus } from 'lucide-react'
import { api } from '../../lib/api'
import { formatDateTime, formatMoney } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { SearchInput } from '../../components/ui/Controls'
import { DataTable, type Column, type SortState } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import type { Paged } from '../admin/adminApi'
import { invoiceTone, orderKeys, type OrderListItem } from './ordersApi'
import './orders.css'

const PAGE_SIZE = 25
const isoDay = (offset: number) => {
  const d = new Date()
  d.setDate(d.getDate() - offset)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export function OrdersPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { can } = useSession()
  const [search, setSearch] = useState('')
  const [range, setRange] = useState<'today' | '7' | '30' | 'all'>('today')
  const [outstanding, setOutstanding] = useState(false)
  const [sort, setSort] = useState<SortState>({})
  const [page, setPage] = useState(1)

  const params = {
    search, from: range === 'all' ? undefined : isoDay(range === 'today' ? 0 : Number(range)), to: range === 'all' ? undefined : isoDay(0),
    outstanding: outstanding || undefined, sort: sort.sort, desc: sort.desc, page, pageSize: PAGE_SIZE,
  }
  const { data, isLoading } = useQuery({ queryKey: orderKeys.list(params), queryFn: () => api<Paged<OrderListItem>>('/api/orders', { query: params }), placeholderData: keepPreviousData })

  const columns: Column<OrderListItem>[] = [
    { key: 'no', header: t('orders.orderNo'), width: 150, render: (o) => <span><span className="num cell-strong">{o.orderNo}</span>{o.priority === 1 && <Badge tone="danger">{t('orders.urgent')}</Badge>}</span> },
    { key: 'date', header: t('orders.date'), sortKey: 'date', render: (o) => formatDateTime(o.createdAtUtc, i18n.language) },
    { key: 'patient', header: t('orders.patientCol'), render: (o) => <span><span className="cell-strong" dir="auto">{i18n.language === 'ur' && o.patientNameUr ? o.patientNameUr : o.patientName}</span><span className="cell-sub num">{o.patientNo}</span></span> },
    { key: 'doctor', header: t('orders.doctorCol'), render: (o) => o.doctorName ?? '—' },
    { key: 'tests', header: t('orders.tests'), align: 'center', render: (o) => <span className="num">{o.testCount}</span> },
    { key: 'total', header: t('orders.total'), align: 'end', render: (o) => <span className="num">{formatMoney(o.net)}</span> },
    { key: 'balance', header: t('orders.balance'), sortKey: 'balance', align: 'end', render: (o) => <span className={`num${o.balance !== 0 ? ' cell-strong' : ''}`}>{formatMoney(Math.abs(o.balance))}</span> },
    { key: 'status', header: t('orders.status'), render: (o) => <Badge tone={invoiceTone[o.invoiceStatus]} dot>{t(`invoiceStatus.${o.invoiceStatus}`)}</Badge> },
  ]

  return (
    <div className="page">
      <div className="page-head">
        <div><h2>{t('orders.listTitle')}</h2><p>{t('orders.listSubtitle')}</p></div>
        {can('orders.create') && <Link to="/orders/new" className="btn btn-primary"><Plus size={16} /> {t('nav.newOrder')}</Link>}
      </div>
      <section className="card">
        <div className="card-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('orders.searchOrders')} />
          <div className="segmented" role="group" aria-label={t('orders.date')}>
            {(['today', '7', '30', 'all'] as const).map((r) => (
              <button key={r} type="button" aria-pressed={range === r} onClick={() => { setRange(r); setPage(1) }}>{r === 'today' ? t('orders.today') : r === '7' ? t('orders.last7') : r === '30' ? t('orders.last30') : t('orders.allDates')}</button>
            ))}
          </div>
          <label className="checkbox"><input type="checkbox" checked={outstanding} onChange={(e) => { setOutstanding(e.target.checked); setPage(1) }} />{t('orders.outstandingOnly')}</label>
        </div>
        <DataTable columns={columns} rows={data?.items} rowKey={(o) => o.id} loading={isLoading} sort={sort} onSort={(s) => { setSort(s); setPage(1) }}
          onRowClick={(o) => navigate(`/orders/${o.id}`)} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage}
          empty={search || outstanding ? undefined : <EmptyState icon={<ClipboardList size={22} />} title={t('orders.emptyTitle')} text={t('orders.emptyText')} />} />
      </section>
    </div>
  )
}
