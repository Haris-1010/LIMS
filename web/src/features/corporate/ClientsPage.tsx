import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Building2, Plus } from 'lucide-react'
import { api } from '../../lib/api'
import { formatMoney } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { SearchInput } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import type { Paged } from '../admin/adminApi'
import { corporateKeys, type ClientListItem } from './corporateApi'
import { ClientFormModal } from './CorporateParts'
import { clientName } from './corporateHooks'

const PAGE_SIZE = 25

/** Corporate clients with what each owes right now. */
export function ClientsPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { can } = useSession()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const params = { search, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: corporateKeys.clients(params), queryFn: () => api<Paged<ClientListItem>>('/api/corporate/clients', { query: params }), placeholderData: keepPreviousData })

  const columns: Column<ClientListItem>[] = [
    { key: 'code', header: t('common.code'), width: 120, render: (c) => <span className="num cell-strong">{c.code}</span> },
    { key: 'name', header: t('corporate.client'), render: (c) => <span><span dir="auto">{clientName(c, i18n.language)}</span>{c.contactName && <span className="cell-sub" dir="auto">{c.contactName}{c.phone ? ` · ${c.phone}` : ''}</span>}</span> },
    { key: 'terms', header: t('corporate.paymentTerms'), render: (c) => t('corporate.days', { count: c.paymentTermsDays }) },
    { key: 'limit', header: t('corporate.creditLimit'), align: 'end', render: (c) => (c.creditLimit === null ? '—' : <span className="num">{formatMoney(c.creditLimit)}</span>) },
    { key: 'balance', header: t('corporate.balance'), align: 'end', render: (c) => <span className="num cell-strong">{formatMoney(c.balance)}</span> },
    { key: 'overdue', header: t('corporate.overdue'), align: 'end', render: (c) => (c.overdue > 0 ? <Badge tone="danger">{formatMoney(c.overdue)}</Badge> : <span className="field-hint">—</span>) },
    { key: 'status', header: t('common.status'), render: (c) => <Badge tone={c.isActive ? 'success' : 'neutral'} dot>{c.isActive ? t('common.active') : t('common.inactive')}</Badge> },
  ]

  return (
    <div className="page">
      <div className="page-head">
        <div><h2>{t('corporate.clientsTitle')}</h2><p>{t('corporate.clientsSubtitle')}</p></div>
        {can('corporate.manage') && <Button variant="primary" icon={<Plus size={16} />} onClick={() => setCreating(true)}>{t('corporate.newClient')}</Button>}
      </div>
      <section className="card">
        <div className="card-toolbar"><SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('corporate.searchClients')} /></div>
        <DataTable columns={columns} rows={data?.items} rowKey={(c) => c.id} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage} onRowClick={(c) => navigate(`/corporate/clients/${c.id}`)}
          empty={search ? undefined : <EmptyState icon={<Building2 size={22} />} title={t('corporate.noClientsTitle')} text={t('corporate.noClientsText')} />} />
      </section>
      {creating && <ClientFormModal client={null} onClose={() => setCreating(false)} onSaved={(id) => navigate(`/corporate/clients/${id}`)} />}
    </div>
  )
}
