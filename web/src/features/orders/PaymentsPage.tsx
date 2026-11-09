import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Banknote } from 'lucide-react'
import { api } from '../../lib/api'
import { formatDateTime, formatMoney } from '../../lib/format'
import { Badge } from '../../components/ui/Badge'
import { SearchInput } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { StatCard } from '../../components/ui/StatCard'
import type { Paged } from '../admin/adminApi'
import { METHODS, orderKeys, type DaySummary, type PaymentListItem } from './ordersApi'
import './orders.css'

const PAGE_SIZE = 25

export function PaymentsPage() {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const [method, setMethod] = useState('')
  const [page, setPage] = useState(1)
  const params = { search, method: method || undefined, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: orderKeys.payments(params), queryFn: () => api<Paged<PaymentListItem>>('/api/billing/payments', { query: params }), placeholderData: keepPreviousData })
  const summary = useQuery({ queryKey: orderKeys.summary, queryFn: () => api<DaySummary>('/api/billing/day-summary') })
  const s = summary.data

  const columns: Column<PaymentListItem>[] = [
    { key: 'no', header: t('payments.receiptNo'), width: 150, render: (p) => <span className="num cell-strong">{p.receiptNo}</span> },
    { key: 'time', header: t('payments.time'), render: (p) => formatDateTime(p.receivedAtUtc, i18n.language) },
    { key: 'order', header: t('payments.order'), render: (p) => <Link to={`/orders/${p.orderId}`} className="num">{p.orderNo}</Link> },
    { key: 'patient', header: t('orders.patientCol'), render: (p) => <span><span dir="auto">{p.patientName}</span><span className="cell-sub num">{p.patientNo}</span></span> },
    { key: 'method', header: t('orders.method'), render: (p) => <span>{t(`paymentMethod.${p.method}`)}{p.reference && <span className="cell-sub num">{p.reference}</span>}</span> },
    { key: 'by', header: t('orders.receivedBy'), render: (p) => p.receivedByName ?? '—' },
    { key: 'amount', header: t('orders.amount'), align: 'end', render: (p) => <span className="num cell-strong">{formatMoney(p.amount)}</span> },
  ]

  return (
    <div className="page">
      <div className="page-head"><div><h2>{t('payments.title')}</h2><p>{t('payments.subtitle')}</p></div></div>
      <div className="grid-stats" style={{ gridTemplateColumns: 'repeat(4, minmax(0, 1fr))', marginBottom: 16 }}>
        <StatCard loading={summary.isLoading} label={t('payments.received')} value={formatMoney(s?.totalReceived ?? 0)} icon={<Banknote size={18} />} tone="success" foot={t('payments.summaryTitle')} />
        <StatCard loading={summary.isLoading} label={t('payments.refunded')} value={formatMoney(s?.totalRefunded ?? 0)} icon={<Banknote size={18} />} tone="warning" />
        <StatCard loading={summary.isLoading} label={t('payments.netCollected')} value={formatMoney((s?.totalReceived ?? 0) - (s?.totalRefunded ?? 0))} icon={<Banknote size={18} />} />
        <div className="card stat">
          <span className="stat-label">{t('orders.method')}</span>
          <div className="badge-list">{s?.byMethod.map((m) => <Badge key={m.method} tone="primary">{t(`paymentMethod.${m.method}`)} · <span className="num">{formatMoney(m.received - m.refunded)}</span></Badge>)}</div>
        </div>
      </div>
      <section className="card">
        <div className="card-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('payments.search')} />
          <select className="select select-sm" value={method} onChange={(e) => { setMethod(e.target.value); setPage(1) }} aria-label={t('orders.method')}>
            <option value="">{t('payments.anyMethod')}</option>
            {METHODS.map((m) => <option key={m} value={m}>{t(`paymentMethod.${m}`)}</option>)}
          </select>
        </div>
        <DataTable columns={columns} rows={data?.items} rowKey={(p) => p.id} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage}
          empty={search || method ? undefined : <EmptyState icon={<Banknote size={22} />} title={t('payments.emptyTitle')} text={t('payments.emptyText')} />} />
      </section>
    </div>
  )
}
