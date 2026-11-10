import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { AlertTriangle, Phone } from 'lucide-react'
import { api } from '../../lib/api'
import { formatDateTime } from '../../lib/format'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Tabs } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { patientName } from '../patients/patientsApi'
import type { Paged } from '../admin/adminApi'
import { NotifyDialog } from './ResultParts'
import { resultKeys, type AlertDto } from './resultsApi'
import './results.css'

const PAGE_SIZE = 25

/** Critical values that must be reported to someone and read back. Open alerts first; each is closed by recording who was told. */
export function CriticalAlertsPage() {
  const { t, i18n } = useTranslation()
  const [status, setStatus] = useState<'0' | '1'>('0')
  const [page, setPage] = useState(1)
  const [notifying, setNotifying] = useState<AlertDto | null>(null)
  const params = { status, page, pageSize: PAGE_SIZE }
  const { data, isLoading } = useQuery({ queryKey: resultKeys.alerts(params), queryFn: () => api<Paged<AlertDto>>('/api/critical-alerts', { query: params }), placeholderData: keepPreviousData, refetchInterval: 20_000 })
  const pick = (en: string, ur: string | null) => (i18n.language === 'ur' && ur ? ur : en)

  const columns: Column<AlertDto>[] = [
    { key: 'patient', header: t('orders.patientCol'), render: (a) => <span><Link to={`/results/item/${a.orderItemId}`} dir="auto">{patientName({ fullName: a.patientName, fullNameUr: a.patientNameUr }, i18n.language)}</Link><span className="cell-sub num">{a.patientNo}{a.patientMobile ? ` · ${a.patientMobile}` : ''}</span></span> },
    { key: 'test', header: t('results.test'), render: (a) => <span>{pick(a.testNameEn, a.testNameUr)}<span className="cell-sub num">{a.testCode}</span></span> },
    { key: 'value', header: t('results.criticalValue'), render: (a) => <span><strong dir="auto">{pick(a.parameterNameEn, a.parameterNameUr)}: <span className="num">{a.valueText}</span> {a.unit}</strong><span className="cell-sub num">{a.isHigh ? t('results.aboveLimit', { limit: a.limit ?? '—' }) : t('results.belowLimit', { limit: a.limit ?? '—' })}</span></span> },
    { key: 'at', header: t('results.raisedAt'), render: (a) => formatDateTime(a.createdAtUtc, i18n.language) },
    { key: 'state', header: t('orders.status'), render: (a) => a.status === 0
      ? <Button size="sm" variant="primary" icon={<Phone size={14} />} onClick={(e) => { e.stopPropagation(); setNotifying(a) }}>{t('results.report')}</Button>
      : <span><Badge tone="success" dot>{t('alertStatus.1')}</Badge><span className="cell-sub">{a.notifiedTo} · {t(`notifyMethod.${a.method}`)} · {a.notifiedAtUtc ? formatDateTime(a.notifiedAtUtc, i18n.language) : ''}</span><span className="cell-sub">{a.notifiedByName}</span></span> },
  ]

  return (
    <div className="page">
      <div className="page-head"><div><h2>{t('results.alertsTitle')}</h2><p>{t('results.alertsSubtitle')}</p></div></div>
      <div style={{ marginBottom: 16 }}><Tabs value={status} onChange={(s) => { setStatus(s); setPage(1) }} tabs={[{ key: '0', label: t('alertStatus.0') }, { key: '1', label: t('alertStatus.1') }]} /></div>
      <section className="card">
        <DataTable columns={columns} rows={data?.items} rowKey={(a) => a.id} loading={isLoading} page={page} pageSize={PAGE_SIZE} total={data?.total} onPage={setPage}
          empty={<EmptyState icon={<AlertTriangle size={22} />} title={status === '0' ? t('results.noOpenAlertsTitle') : t('results.noAlertsTitle')} text={t('results.noOpenAlertsText')} />} />
      </section>
      {notifying && <NotifyDialog alert={notifying} onClose={() => setNotifying(null)} />}
    </div>
  )
}
