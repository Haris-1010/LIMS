import { useQuery } from '@tanstack/react-query'
import { api } from '../../lib/api'
import { useSession } from '../../app/session'
import { resultKeys, type OrderResult } from './resultsApi'

/** The tests of an order, in order, for moving between them on the result screen. Cancelled tests are left out. */
export function useOrderTests(orderId: number | undefined) {
  const { can } = useSession()
  const { data } = useQuery({
    queryKey: resultKeys.ofOrder(orderId ?? 0),
    queryFn: () => api<OrderResult[]>(`/api/orders/${orderId}/results`),
    enabled: !!orderId && can('results.view') && can('orders.view'),
  })
  return (data ?? []).filter((r) => r.itemStatus !== 90)
}

/** The next test after the current one that still needs work (entry or review), else simply the next one. */
export function nextTest(tests: OrderResult[], currentItemId: number): OrderResult | null {
  const i = tests.findIndex((x) => x.orderItemId === currentItemId)
  const after = [...tests.slice(i + 1), ...tests.slice(0, Math.max(i, 0))]
  return after.find((x) => x.resultStatus !== 30) ?? tests[i + 1] ?? null
}
