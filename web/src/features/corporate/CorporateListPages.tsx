import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { FileText, Plus, Wallet } from 'lucide-react'
import { api } from '../../lib/api'
import { formatDateTime, formatMoney } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { SearchInput } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import type { Paged } from '../admin/adminApi'
import { bucketTone, corporateKeys, type ClientListItem, type CorporateInvoice, type Receipt } from './corporateApi'
import { AllocateModal, ReceiptModal } from './CorporateParts'
import { clientName } from './corporateHooks'

const PAGE_SIZE = 25

function useClientOptions() {
  const { data } = useQuery({ queryKey: corporateKeys.clients({ options: true }), queryFn: () => api<Paged<ClientListItem>>('/api/corporate/clients', { query: { pageSize: 100 } }), staleTime: 60_000 })
  return data?.items ?? []
}

/** Credit invoices of every corporate client, with how late each is. */
export function CorporateInvoicesPage() {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const [clientId, setClientId] = useState('')
  const [outstanding, setOutstanding] = useState(true)
  const [page, setPage] = useState(1)
  const clients = useClientOptions()
  const params = { search, clientId: clientId || undefined, outstandingOnly: outstanding || undefined, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: corporateKeys.invoices(params), queryFn: () => api<Paged<CorporateInvoice>>('/api/corporate/invoices', { query: params }), placeholderData: keepPreviousData })

  const columns: Column<CorporateInvoice>[] = [
    { key: 'no', header: t('corporate.invoice'), width: 150, render: (i) => <Link to={`/orders/${i.orderId}`} className="num cell-strong">{i.invoiceNo}</Link> },
    { key: 'client', header: t('corporate.client'), render: (i) => <Link to={`/corporate/clients/${i.clientId}`} dir="auto">{i.clientName}</Link> },
    { key: 'patient', header: t('orders.patientCol'), render: (i) => <span><span dir="auto">{i18n.language === 'ur' && i.patientNameUr ? i.patientNameUr : i.patientName}</span><span className="cell-sub num">{i.orderNo}</span></span> },
    { key: 'due', header: t('corporate.due'), render: (i) => <span>{i.dueDate ?? '—'} {i.bucket !== null && i.bucket > 0 && <Badge tone={bucketTone[i.bucket]}>{t(`corporate.bucketShort.${i.bucket}`)}</Badge>}</span> },
    { key: 'net', header: t('orders.net'), align: 'end', render: (i) => <span className="num">{formatMoney(i.net)}</span> },
    { key: 'paid', header: t('orders.paid'), align: 'end', render: (i) => <span className="num">{formatMoney(i.paid)}</span> },
    { key: 'balance', header: t('orders.balanceDue'), align: 'end', render: (i) => <span className="num cell-strong">{formatMoney(i.balance)}</span> },
  ]
  return (
    <div className="page">
      <div className="page-head"><div><h2>{t('corporate.invoicesTitle')}</h2><p>{t('corporate.invoicesSubtitle')}</p></div></div>
      <section className="card">
        <div className="card-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('corporate.searchInvoices')} />
          <select className="select select-sm" value={clientId} onChange={(e) => { setClientId(e.target.value); setPage(1) }} aria-label={t('corporate.client')}>
            <option value="">{t('corporate.allClients')}</option>
            {clients.map((c) => <option key={c.id} value={c.id}>{clientName(c, i18n.language)}</option>)}
          </select>
          <label className="checkbox"><input type="checkbox" checked={outstanding} onChange={(e) => { setOutstanding(e.target.checked); setPage(1) }} />{t('corporate.outstandingOnly')}</label>
        </div>
        <DataTable columns={columns} rows={data?.items} rowKey={(i) => i.invoiceId} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage}
          empty={<EmptyState icon={<FileText size={22} />} title={t('corporate.noInvoicesTitle')} text={t('corporate.noInvoicesText')} />} />
      </section>
    </div>
  )
}

/** Money and credit notes received from clients, and what has been matched to invoices. */
export function CorporateReceiptsPage() {
  const { t, i18n } = useTranslation()
  const { can } = useSession()
  const [search, setSearch] = useState('')
  const [clientId, setClientId] = useState('')
  const [unallocated, setUnallocated] = useState(false)
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const [allocating, setAllocating] = useState<Receipt | null>(null)
  const clients = useClientOptions()
  const params = { search, clientId: clientId || undefined, unallocatedOnly: unallocated || undefined, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: corporateKeys.receipts(params), queryFn: () => api<Paged<Receipt>>('/api/corporate/receipts', { query: params }), placeholderData: keepPreviousData })

  const columns: Column<Receipt>[] = [
    { key: 'no', header: t('corporate.receiptNo'), width: 150, render: (r) => <span className="num cell-strong">{r.receiptNo}</span> },
    { key: 'client', header: t('corporate.client'), render: (r) => <Link to={`/corporate/clients/${r.clientId}`} dir="auto" onClick={(e) => e.stopPropagation()}>{r.clientName}</Link> },
    { key: 'kind', header: t('corporate.kind'), render: (r) => (r.kind === 2 ? <Badge tone="info">{t('corporate.kindCreditNote')}</Badge> : <span>{r.method ? t(`paymentMethod.${r.method}`) : ''}{r.reference && <span className="cell-sub num">{r.reference}</span>}</span>) },
    { key: 'at', header: t('corporate.date'), render: (r) => <span>{formatDateTime(r.receivedAtUtc, i18n.language)}{r.receivedByName && <span className="cell-sub">{r.receivedByName}</span>}</span> },
    { key: 'amount', header: t('orders.amount'), align: 'end', render: (r) => <span className="num cell-strong">{formatMoney(r.amount)}</span> },
    { key: 'left', header: t('corporate.unallocated'), align: 'end', render: (r) => (r.unallocated > 0 ? <Badge tone="warning">{formatMoney(r.unallocated)}</Badge> : <Badge tone="success">{t('corporate.fullyAllocated')}</Badge>) },
  ]
  return (
    <div className="page">
      <div className="page-head">
        <div><h2>{t('corporate.receiptsTitle')}</h2><p>{t('corporate.receiptsSubtitle')}</p></div>
        {(can('corporate.receive') || can('corporate.adjust')) && <Button variant="primary" icon={<Plus size={16} />} onClick={() => setCreating(true)}>{t('corporate.newReceipt')}</Button>}
      </div>
      <section className="card">
        <div className="card-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('corporate.searchReceipts')} />
          <select className="select select-sm" value={clientId} onChange={(e) => { setClientId(e.target.value); setPage(1) }} aria-label={t('corporate.client')}>
            <option value="">{t('corporate.allClients')}</option>
            {clients.map((c) => <option key={c.id} value={c.id}>{clientName(c, i18n.language)}</option>)}
          </select>
          <label className="checkbox"><input type="checkbox" checked={unallocated} onChange={(e) => { setUnallocated(e.target.checked); setPage(1) }} />{t('corporate.unallocatedOnly')}</label>
        </div>
        <DataTable columns={columns} rows={data?.items} rowKey={(r) => r.id} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage} onRowClick={(r) => setAllocating(r)}
          empty={<EmptyState icon={<Wallet size={22} />} title={t('corporate.noReceiptsTitle')} text={t('corporate.noReceiptsText')} />} />
      </section>
      {creating && <ReceiptModal onClose={() => setCreating(false)} onCreated={(r) => { setCreating(false); setAllocating(r) }} />}
      {allocating && <AllocateModal receipt={allocating} onClose={() => setAllocating(null)} />}
    </div>
  )
}
