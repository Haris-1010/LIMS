import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { FileText } from 'lucide-react'
import { useSession } from '../../app/session'
import { api } from '../../lib/api'
import { Badge } from '../../components/ui/Badge'
import { Card } from '../../components/ui/Card'
import { Skeleton } from '../../components/ui/Skeleton'
import { ResultStatusBadge } from './ResultParts'
import { resultKeys, type OrderResult } from './resultsApi'

/** Result status per test on the order page (for people with results.view). Values are opened on the result screen, not shown here. */
export function OrderResultsCard({ orderId }: { orderId: number }) {
  const { t, i18n } = useTranslation()
  const { can } = useSession()
  const { data, isLoading } = useQuery({ queryKey: resultKeys.ofOrder(orderId), queryFn: () => api<OrderResult[]>(`/api/orders/${orderId}/results`) })
  return (
    <Card title={t('results.orderCardTitle')} subtitle={t('results.orderCardSubtitle')} flush
      actions={can('reports.view') && data?.some((r) => r.hasReleased) ? <Link to={`/reports/order/${orderId}`} className="btn btn-secondary btn-sm"><FileText size={14} aria-hidden /> {t('results.openReport')}</Link> : undefined}>
      {isLoading ? <div style={{ padding: 16 }}><Skeleton height={60} radius={8} /></div> : (
        <ul className="ledger" style={{ padding: '0 var(--space-5)' }}>
          {data?.map((r) => (
            <li key={r.orderItemId} style={{ gridTemplateColumns: 'minmax(0, 1fr) auto' }}>
              <span>
                <span className="cell-strong" dir="auto">{i18n.language === 'ur' && r.testNameUr ? r.testNameUr : r.testNameEn}</span>
                <span className="cell-sub num">{r.testCode}</span>
              </span>
              <span className="badge-list">
                {r.itemStatus === 90 ? <Badge>{t('orders.cancelled')}</Badge> : r.resultStatus === null ? <Badge>{t('results.notStarted')}</Badge> : <Link to={`/results/item/${r.orderItemId}`}><ResultStatusBadge status={r.resultStatus} /></Link>}
                {r.version !== null && r.version > 1 && <Badge tone="warning">{t('results.version', { n: r.version })}</Badge>}
              </span>
            </li>
          ))}
        </ul>
      )}
    </Card>
  )
}
