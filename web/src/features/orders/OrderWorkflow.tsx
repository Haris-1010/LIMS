import { useState, type ReactNode } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Check, ClipboardCheck, ClipboardPen, FilePlus2, FlaskConical, Printer, ShieldCheck, TestTube2 } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { useSession } from '../../app/session'
import { Button } from '../../components/ui/Button'
import { useToast } from '../../components/ui/Toast'
import type { Paged } from '../admin/adminApi'
import { reportKeys, type ReportVersion } from '../reports/reportsApi'
import { resultKeys, type OrderResult } from '../results/resultsApi'
import { CollectAllDialog } from '../samples/OrderSamplesCard'
import { printLabels } from '../samples/labelBus'
import { useRefreshSamples } from '../samples/sampleHooks'
import { sampleKeys, type SampleListItem } from '../samples/samplesApi'
import './orders.css'

type StepKey = 'registered' | 'collect' | 'receive' | 'enter' | 'review' | 'issue' | 'deliver'
type StepState = 'done' | 'current' | 'todo'
interface Step { key: StepKey; state: StepState; icon: ReactNode; detail?: string; action?: ReactNode }

/**
 * The order's journey through the lab, shown at the top of the order page (so it is the first thing after creating an order):
 * registered → collect → receive → enter results → verify/approve → issue report → print. Each step shows how far it is and,
 * for people allowed to do it, the button that does it (or opens the screen where it is done). Nothing is skipped: every button
 * runs the same checks as the screens it stands in for.
 */
export function OrderWorkflow({ orderId, cancelled }: { orderId: number; cancelled: boolean }) {
  const { t } = useTranslation()
  const { can } = useSession()
  const toast = useToast()
  const refreshSamples = useRefreshSamples()
  const [collecting, setCollecting] = useState<{ sample: SampleListItem; count: number } | null>(null)

  // Same queries (and cache) as the sample, result and report cards, so the bar moves when they do.
  const samplesQ = useQuery({
    queryKey: sampleKeys.ofOrder(orderId),
    queryFn: () => api<Paged<SampleListItem>>('/api/samples', { query: { orderId, pageSize: 50 } }),
    enabled: can('samples.view'),
  })
  const resultsQ = useQuery({ queryKey: resultKeys.ofOrder(orderId), queryFn: () => api<OrderResult[]>(`/api/orders/${orderId}/results`), enabled: can('results.view') })
  const reportsQ = useQuery({ queryKey: reportKeys.versions(orderId), queryFn: () => api<ReportVersion[]>(`/api/orders/${orderId}/reports`), enabled: can('reports.view') })

  const receive = useMutation({
    mutationFn: async (list: SampleListItem[]) => {
      let ok = 0
      for (const s of list) {
        await api(`/api/samples/${s.id}/receive`, { method: 'POST' })
        ok++
      }
      return ok
    },
    onSuccess: (n) => toast.show({ tone: 'success', title: t('workflow.receivedMany', { count: n }) }),
    onError: (e) => toast.show({ tone: 'danger', title: t('samples.actionFailed'), text: errorMessage(t, e) }),
    onSettled: refreshSamples,
  })

  if (cancelled) return null

  const samples = (samplesQ.data?.items ?? []).filter((s) => s.status !== 80 && s.status !== 90)
  const toCollect = samples.filter((s) => s.status === 0)
  const toReceive = samples.filter((s) => s.status === 10 || s.status === 15)
  const items = (resultsQ.data ?? []).filter((r) => r.itemStatus !== 90)
  const toEnter = items.filter((r) => r.resultStatus === null || r.resultStatus === 0)
  const toReview = items.filter((r) => r.resultStatus === 10 || r.resultStatus === 20)
  const released = items.filter((r) => r.resultStatus === 30)
  const live = reportsQ.data?.find((v) => v.status !== 90)

  const know = { samples: samplesQ.isSuccess, results: resultsQ.isSuccess, reports: reportsQ.isSuccess }
  const done: Record<StepKey, boolean> = {
    registered: true,
    collect: know.samples && samples.length > 0 && toCollect.length === 0,
    receive: know.samples && samples.length > 0 && toCollect.length === 0 && toReceive.length === 0,
    enter: know.results && items.length > 0 && toEnter.length === 0,
    review: know.results && items.length > 0 && released.length === items.length,
    issue: know.reports && !!live && !live.isOutdated,
    deliver: know.reports && !!live && !live.isOutdated && live.printCount > 0,
  }
  const order: StepKey[] = ['registered', 'collect', 'receive', 'enter', 'review', 'issue', 'deliver']
  const current = order.find((k) => !done[k]) ?? null
  const state = (k: StepKey): StepState => (done[k] ? 'done' : k === current ? 'current' : 'todo')
  const reportLink = `/reports/order/${orderId}`

  const steps: Step[] = [
    { key: 'registered', state: 'done', icon: <ClipboardPen size={16} /> },
    {
      key: 'collect', state: state('collect'), icon: <TestTube2 size={16} />,
      detail: know.samples ? (toCollect.length ? t('workflow.waitingCount', { count: toCollect.length }) : samples.length ? t('workflow.allDone') : t('workflow.noSamples')) : undefined,
      action: toCollect.length > 0 && can('samples.collect')
        ? <Button size="sm" variant={current === 'collect' ? 'primary' : 'secondary'} icon={<TestTube2 size={14} />} onClick={() => setCollecting({ sample: toCollect[0]!, count: toCollect.length })}>{t('workflow.collect', { count: toCollect.length })}</Button>
        : undefined,
    },
    {
      key: 'receive', state: state('receive'), icon: <FlaskConical size={16} />,
      detail: know.samples && samples.length > 0 ? (toReceive.length ? t('workflow.waitingCount', { count: toReceive.length }) : toCollect.length ? undefined : t('workflow.allDone')) : undefined,
      action: toReceive.length > 0 && can('samples.receive')
        ? <Button size="sm" variant={current === 'receive' ? 'primary' : 'secondary'} icon={<FlaskConical size={14} />} loading={receive.isPending} onClick={() => receive.mutate(toReceive)}>{t('workflow.receive', { count: toReceive.length })}</Button>
        : undefined,
    },
    {
      key: 'enter', state: state('enter'), icon: <ClipboardPen size={16} />,
      detail: know.results && items.length > 0 ? (toEnter.length ? t('workflow.ofTests', { done: items.length - toEnter.length, total: items.length }) : t('workflow.allDone')) : undefined,
      action: toEnter.length > 0 && can('results.enter') && done.receive
        ? <Link className={`btn btn-sm ${current === 'enter' ? 'btn-primary' : 'btn-secondary'}`} to={`/results/item/${toEnter[0]!.orderItemId}`}><ClipboardPen size={14} aria-hidden /> {t('workflow.enter')}</Link>
        : undefined,
    },
    {
      key: 'review', state: state('review'), icon: <ShieldCheck size={16} />,
      detail: know.results && items.length > 0 ? t('workflow.ofTests', { done: released.length, total: items.length }) : undefined,
      action: toReview.length > 0 && (can('results.verify') || can('results.approve'))
        ? <Link className={`btn btn-sm ${current === 'review' ? 'btn-primary' : 'btn-secondary'}`} to={`/results/item/${toReview[0]!.orderItemId}`}><ShieldCheck size={14} aria-hidden /> {t('workflow.review')}</Link>
        : undefined,
    },
    {
      key: 'issue', state: state('issue'), icon: <ClipboardCheck size={16} />,
      detail: live ? (live.isOutdated ? t('workflow.outdated') : live.reportNo) : undefined,
      action: released.length > 0 && can('reports.issue') && (!live || live.isOutdated)
        ? <Link className={`btn btn-sm ${current === 'issue' ? 'btn-primary' : 'btn-secondary'}`} to={reportLink}><FilePlus2 size={14} aria-hidden /> {live ? t('workflow.issueNew') : t('workflow.issue')}</Link>
        : undefined,
    },
    {
      key: 'deliver', state: state('deliver'), icon: <Printer size={16} />,
      detail: live && live.printCount > 0 ? t('workflow.printedCount', { count: live.printCount }) : undefined,
      action: live && can('reports.print')
        ? <Link className={`btn btn-sm ${current === 'deliver' ? 'btn-primary' : 'btn-secondary'}`} to={reportLink}><Printer size={14} aria-hidden /> {t('workflow.print')}</Link>
        : undefined,
    },
  ]

  const doneCount = steps.filter((s) => s.state === 'done').length

  return (
    <section className="card workflow no-print" aria-label={t('workflow.title')}>
      <header className="workflow-head">
        <div>
          <h3 className="card-title">{t('workflow.title')}</h3>
          <p className="card-subtitle">{current ? t('workflow.next', { step: t(`workflow.step.${current}`) }) : t('workflow.complete')}</p>
        </div>
        <span className="workflow-progress" aria-hidden><span style={{ width: `${(doneCount / steps.length) * 100}%` }} /></span>
      </header>
      <ol className="workflow-steps">
        {steps.map((s, i) => (
          <li key={s.key} className={`workflow-step is-${s.state}`} aria-current={s.state === 'current' ? 'step' : undefined}>
            <span className="workflow-dot" aria-hidden>{s.state === 'done' ? <Check size={15} strokeWidth={3} /> : s.icon}</span>
            <span className="workflow-body">
              <span className="workflow-label"><span className="num workflow-n">{i + 1}</span> {t(`workflow.step.${s.key}`)}</span>
              {s.detail && <span className="workflow-detail">{s.detail}</span>}
              {s.action && <span className="workflow-action">{s.action}</span>}
            </span>
          </li>
        ))}
      </ol>
      {collecting && <CollectAllDialog orderId={orderId} sample={collecting.sample} count={collecting.count} onClose={() => setCollecting(null)} onPrint={printLabels} />}
    </section>
  )
}
