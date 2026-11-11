import { useTranslation } from 'react-i18next'
import { ArrowRight, FileText } from 'lucide-react'
import { useSession } from '../../app/session'
import { Button } from '../../components/ui/Button'
import { nextTest } from './orderTests'
import type { OrderResult, ResultStatus } from './resultsApi'

const dot: Record<ResultStatus | 'none', string> = { none: 'is-none', 0: 'is-draft', 10: 'is-review', 20: 'is-review', 30: 'is-done', 90: 'is-none' }

/**
 * Strip on the result screen: every test of the order with its state, a "next test" button and a link to the order's report,
 * so a whole order can be entered (or reviewed) without going back to the queue. `onGo` lets the page warn about unsaved values.
 */
export function OrderTestsBar({ orderId, tests, currentItemId, onGo }: { orderId: number; tests: OrderResult[]; currentItemId: number; onGo: (path: string) => void }) {
  const { t, i18n } = useTranslation()
  const { can } = useSession()
  const next = nextTest(tests, currentItemId)
  const released = tests.filter((x) => x.resultStatus === 30).length
  if (tests.length === 0) return null

  return (
    <nav className="card order-tests no-print" aria-label={t('results.orderTests')}>
      <div className="order-tests-head">
        <strong>{t('results.orderTests')}</strong>
        <span className="field-hint">{t('results.orderTestsReleased', { done: released, total: tests.length })}</span>
        <span className="toolbar order-tests-actions">
          {next && <Button size="sm" variant="primary" icon={<ArrowRight size={14} className="flip-rtl" />} onClick={() => onGo(`/results/item/${next.orderItemId}`)}>{t('results.nextTest', { code: next.testCode })}</Button>}
          {can('reports.view') && <Button size="sm" icon={<FileText size={14} />} onClick={() => onGo(`/reports/order/${orderId}`)}>{t('results.openReport')}</Button>}
        </span>
      </div>
      {tests.length > 1 && (
        <ol className="order-tests-list">
          {tests.map((x, i) => (
            <li key={x.orderItemId}>
              <button type="button" aria-current={x.orderItemId === currentItemId ? 'page' : undefined} onClick={() => x.orderItemId !== currentItemId && onGo(`/results/item/${x.orderItemId}`)}
                title={x.resultStatus === null ? t('results.notStarted') : t(`resultStatus.${x.resultStatus}`)}>
                <span className={`order-tests-dot ${dot[x.resultStatus ?? 'none']}`} aria-hidden />
                <span className="num order-tests-n">{i + 1}</span>
                <span className="order-tests-name" dir="auto">{i18n.language === 'ur' && x.testNameUr ? x.testNameUr : x.testNameEn}</span>
                <span className="num order-tests-code">{x.testCode}</span>
              </button>
            </li>
          ))}
        </ol>
      )}
    </nav>
  )
}
