import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Trans, useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import {
  AlertTriangle,
  ArrowRight,
  Banknote,
  BarChart3,
  Building2,
  CheckCircle2,
  ClipboardCheck,
  ClipboardList,
  Download,
  FileCheck2,
  FlaskConical,
  Info,
  RefreshCw,
  Table2,
  TestTube2,
  UserPlus,
  Users,
  XCircle,
} from 'lucide-react'
import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { displayName, useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { EmptyState } from '../../components/ui/EmptyState'
import { Skeleton } from '../../components/ui/Skeleton'
import { StatCard } from '../../components/ui/StatCard'
import { useToast } from '../../components/ui/Toast'
import { formatCompact, formatLongDate, formatMoney, formatNumber, formatShortDate } from '../../lib/format'
import { fetchDashboardSummary, type ActivityType, type DashboardRange, type DashboardSummary } from './dashboardApi'
import './dashboard.css'

export function DashboardPage() {
  const { t, i18n } = useTranslation()
  const { user } = useSession()
  const branchId = user?.activeBranchId ?? 'all'
  const toast = useToast()
  const [range, setRange] = useState<DashboardRange>('today')

  const { data, isLoading, isFetching, refetch } = useQuery({
    queryKey: ['dashboard', range, branchId],
    queryFn: () => fetchDashboardSummary(range, branchId),
  })

  const firstName = user ? displayName(user, i18n.language).split(' ')[0] : ''
  const s = data?.stats

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <h2>{t('dashboard.greeting', { name: firstName })}</h2>
          <p>
            {formatLongDate(new Date(), i18n.language)} · {t('dashboard.subtitle')}
          </p>
        </div>
        <div className="toolbar">
          <div className="segmented" role="group" aria-label="Date range">
            {(['today', 'd7', 'd30'] as const).map((r) => (
              <button key={r} type="button" aria-pressed={range === r} onClick={() => setRange(r)}>
                {t(`dashboard.range.${r}`)}
              </button>
            ))}
          </div>
          <Button
            iconOnly
            aria-label={t('dashboard.refresh')}
            title={t('dashboard.refresh')}
            onClick={() => refetch()}
            icon={<RefreshCw size={16} className={isFetching ? 'spin' : undefined} />}
          />
          <Button
            icon={<Download size={16} />}
            onClick={() => toast.show({ tone: 'info', title: t('toast.exported'), text: t('toast.exportedText') })}
          >
            {t('dashboard.export')}
          </Button>
        </div>
      </div>

      {data?.isPreview && (
        <div className="banner" style={{ marginBottom: 16 }}>
          <Info size={16} aria-hidden style={{ flexShrink: 0 }} />
          <span>{t('dashboard.preview')}</span>
        </div>
      )}

      <div className="grid-stats">
        <StatCard
          loading={isLoading}
          label={t('dashboard.stats.totalPatients')}
          value={s ? formatNumber(s.totalPatients) : ''}
          icon={<Users size={18} />}
          foot={t('dashboard.stats.allTime')}
        />
        <StatCard
          loading={isLoading}
          label={t('dashboard.stats.todayRegistrations')}
          value={s ? formatNumber(s.registrations) : ''}
          icon={<UserPlus size={18} />}
          tone="accent"
          delta={s?.registrationsDelta}
          foot={t('dashboard.vsPrevious')}
        />
        <StatCard
          loading={isLoading}
          label={t('dashboard.stats.todayOrders')}
          value={s ? formatNumber(s.orders) : ''}
          icon={<ClipboardList size={18} />}
          delta={s?.ordersDelta}
          foot={t('dashboard.vsPrevious')}
        />
        <StatCard
          loading={isLoading}
          label={t('dashboard.stats.cashCollection')}
          value={s ? formatMoney(s.cashCollected) : ''}
          icon={<Banknote size={18} />}
          tone="success"
          delta={s?.cashDelta}
          foot={t('dashboard.vsPrevious')}
        />
      </div>

      <div className="grid-stats cols-5 section-gap">
        <StatCard
          loading={isLoading}
          label={t('dashboard.stats.pendingSamples')}
          value={s ? formatNumber(s.pendingSamples) : ''}
          icon={<TestTube2 size={18} />}
          tone="warning"
          foot={s && <span className="text-warning">{t('dashboard.stats.toCollect', { count: s.overdueSamples })}</span>}
        />
        <StatCard
          loading={isLoading}
          label={t('dashboard.stats.testsInProgress')}
          value={s ? formatNumber(s.testsInProgress) : ''}
          icon={<FlaskConical size={18} />}
          foot={s && t('dashboard.stats.acrossDepartments', { count: s.departments })}
        />
        <StatCard
          loading={isLoading}
          label={t('dashboard.stats.awaitingApproval')}
          value={s ? formatNumber(s.awaitingApproval) : ''}
          icon={<ClipboardCheck size={18} />}
          tone="danger"
          foot={s && t('dashboard.stats.oldest', { time: waitLabel(s.oldestWaitingMinutes, t) })}
        />
        <StatCard
          loading={isLoading}
          label={t('dashboard.stats.completedReports')}
          value={s ? formatNumber(s.completedReports) : ''}
          icon={<FileCheck2 size={18} />}
          tone="success"
          foot={s && t('dashboard.stats.readyToPrint', { count: s.readyToPrint })}
        />
        <StatCard
          loading={isLoading}
          label={t('dashboard.stats.corporateOutstanding')}
          value={s ? formatMoney(s.corporateOutstanding) : ''}
          icon={<Building2 size={18} />}
          tone="accent"
          foot={s && t('dashboard.stats.clients', { count: s.corporateClients })}
        />
      </div>

      <div className="grid-2-1 section-gap">
        <CollectionsCard data={data} loading={isLoading} />
        <PipelineCard data={data} loading={isLoading} />
      </div>

      <div className="grid-2-1 section-gap">
        <ApprovalsCard data={data} loading={isLoading} />
        <ActivityCard data={data} loading={isLoading} />
      </div>
    </div>
  )
}

type T = ReturnType<typeof useTranslation>['t']

function waitLabel(minutes: number, t: T) {
  return minutes >= 60 ? t('common.hoursAgo', { count: Math.floor(minutes / 60) }) : t('common.minutesAgo', { count: minutes })
}

/* ---------------- Collections chart ---------------- */
function CollectionsCard({ data, loading }: { data?: DashboardSummary; loading: boolean }) {
  const { t, i18n } = useTranslation()
  const [asTable, setAsTable] = useState(false)
  const rows = data?.trend ?? []

  return (
    <Card
      title={t('dashboard.trend.title')}
      subtitle={t('dashboard.trend.subtitle')}
      actions={
        <Button
          size="sm"
          variant="ghost"
          icon={asTable ? <BarChart3 size={15} /> : <Table2 size={15} />}
          onClick={() => setAsTable((v) => !v)}
        >
          {asTable ? t('dashboard.trend.chart') : t('dashboard.trend.table')}
        </Button>
      }
    >
      {loading ? (
        <Skeleton height={280} radius={8} />
      ) : asTable ? (
        <div className="table-wrap" style={{ maxHeight: 280 }}>
          <table className="table">
            <thead>
              <tr>
                <th>{t('dashboard.trend.date')}</th>
                <th style={{ textAlign: 'end' }}>{t('dashboard.trend.cash')}</th>
                <th style={{ textAlign: 'end' }}>{t('dashboard.trend.credit')}</th>
                <th style={{ textAlign: 'end' }}>{t('dashboard.trend.total')}</th>
              </tr>
            </thead>
            <tbody>
              {[...rows].reverse().map((r) => (
                <tr key={r.date}>
                  <td>{formatShortDate(new Date(r.date), i18n.language)}</td>
                  <td className="num" style={{ textAlign: 'end' }}>{formatMoney(r.cash)}</td>
                  <td className="num" style={{ textAlign: 'end' }}>{formatMoney(r.credit)}</td>
                  <td className="num cell-strong" style={{ textAlign: 'end' }}>{formatMoney(r.cash + r.credit)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        // Time runs left-to-right in both languages; only the surrounding text flips.
        <div dir="ltr" style={{ height: 280 }}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={rows} margin={{ top: 8, right: 4, left: 4, bottom: 0 }} barCategoryGap="28%">
              <CartesianGrid vertical={false} stroke="var(--chart-grid)" />
              <XAxis
                dataKey="date"
                tickFormatter={(d: string) => formatShortDate(new Date(d), i18n.language)}
                tick={{ fontSize: 11, fill: 'var(--muted)' }}
                axisLine={{ stroke: 'var(--border)' }}
                tickLine={false}
                interval="preserveStartEnd"
                minTickGap={16}
              />
              <YAxis
                tickFormatter={(v: number) => formatCompact(v)}
                tick={{ fontSize: 11, fill: 'var(--muted)' }}
                axisLine={false}
                tickLine={false}
                width={44}
              />
              <Tooltip
                cursor={{ fill: 'rgba(44,100,166,0.06)' }}
                content={({ active, payload, label }) =>
                  active && payload?.length ? (
                    <div className="chart-tip" dir={i18n.dir()}>
                      <div className="chart-tip-title">{formatShortDate(new Date(label as string), i18n.language)}</div>
                      {payload.map((p) => (
                        <div key={p.dataKey as string} className="chart-tip-row">
                          <span className="chart-swatch" style={{ background: p.color }} />
                          <span>{p.name}</span>
                          <span className="num">{formatMoney(p.value as number)}</span>
                        </div>
                      ))}
                    </div>
                  ) : null
                }
              />
              <Legend
                verticalAlign="top"
                align="right"
                height={28}
                iconType="circle"
                iconSize={8}
                formatter={(v: string) => <span style={{ color: 'var(--text-2)', fontSize: 12 }}>{v}</span>}
              />
              <Bar dataKey="cash" name={t('dashboard.trend.cash')} stackId="a" fill="var(--chart-1)" stroke="var(--surface)" strokeWidth={1} />
              <Bar
                dataKey="credit"
                name={t('dashboard.trend.credit')}
                stackId="a"
                fill="var(--chart-2)"
                radius={[4, 4, 0, 0]}
                stroke="var(--surface)"
                strokeWidth={1}
              />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
    </Card>
  )
}

/* ---------------- Sample pipeline ---------------- */
function PipelineCard({ data, loading }: { data?: DashboardSummary; loading: boolean }) {
  const { t } = useTranslation()
  const max = Math.max(1, ...(data?.pipeline.map((p) => p.count) ?? [1]))

  return (
    <Card title={t('dashboard.pipeline.title')} subtitle={t('dashboard.pipeline.subtitle')}>
      {loading ? (
        <div className="stack" style={{ gap: 18 }}>
          {Array.from({ length: 6 }, (_, i) => (
            <Skeleton key={i} height={26} />
          ))}
        </div>
      ) : (
        <ul className="pipeline">
          {data!.pipeline.map((p) => (
            <li key={p.key} className={`is-${p.key}`}>
              <div className="pipeline-row">
                <span className="pipeline-label">
                  {p.key === 'rejected' && <XCircle size={14} aria-hidden />}
                  {p.key === 'completed' && <CheckCircle2 size={14} aria-hidden />}
                  {t(`dashboard.pipeline.${p.key}`)}
                </span>
                <span className="num pipeline-count">{formatNumber(p.count)}</span>
              </div>
              <div className="pipeline-track" aria-hidden>
                <span className="pipeline-bar" style={{ width: `${(p.count / max) * 100}%` }} />
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  )
}

/* ---------------- Approvals ---------------- */
function ApprovalsCard({ data, loading }: { data?: DashboardSummary; loading: boolean }) {
  const { t } = useTranslation()
  const flagBadge = {
    critical: (
      <Badge tone="danger">
        <AlertTriangle size={12} aria-hidden />
        {t('dashboard.approvals.critical')}
      </Badge>
    ),
    abnormal: (
      <Badge tone="warning" dot>
        {t('dashboard.approvals.abnormal')}
      </Badge>
    ),
    normal: (
      <Badge tone="success" dot>
        {t('dashboard.approvals.normal')}
      </Badge>
    ),
  }

  return (
    <Card
      title={t('dashboard.approvals.title')}
      subtitle={t('dashboard.approvals.subtitle')}
      flush
      actions={
        <Link to="/results/verification" className="btn btn-ghost btn-sm">
          {t('dashboard.approvals.viewAll')}
          <ArrowRight size={14} className="flip-rtl" aria-hidden />
        </Link>
      }
    >
      <div className="table-wrap">
        <table className="table">
          <thead>
            <tr>
              <th>{t('dashboard.approvals.order')}</th>
              <th>{t('dashboard.approvals.patient')}</th>
              <th>{t('dashboard.approvals.test')}</th>
              <th>{t('dashboard.approvals.waiting')}</th>
              <th>{t('dashboard.approvals.status')}</th>
            </tr>
          </thead>
          <tbody>
            {loading
              ? Array.from({ length: 5 }, (_, i) => (
                  <tr key={i}>
                    {Array.from({ length: 5 }, (_, j) => (
                      <td key={j}>
                        <Skeleton height={14} width={j === 1 ? 140 : 90} />
                      </td>
                    ))}
                  </tr>
                ))
              : data!.approvals.map((a) => (
                  <tr key={a.orderNo}>
                    <td className="num cell-strong">{a.orderNo}</td>
                    <td>
                      <span className="cell-strong" dir="auto">
                        {a.patient}
                      </span>
                      <span className="cell-sub num">{a.patientNo}</span>
                    </td>
                    <td>
                      <span dir="auto">{a.test}</span>
                      <span className="cell-sub" dir="auto">
                        {a.department}
                      </span>
                    </td>
                    <td>{waitLabel(a.waitingMinutes, t)}</td>
                    <td>{flagBadge[a.flag]}</td>
                  </tr>
                ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}

/* ---------------- Activity ---------------- */
const activityIcon: Record<ActivityType, { icon: typeof Users; tone: string }> = {
  registration: { icon: UserPlus, tone: 'primary' },
  payment: { icon: Banknote, tone: 'success' },
  approval: { icon: ClipboardCheck, tone: 'accent' },
  sample: { icon: TestTube2, tone: 'primary' },
  rejection: { icon: XCircle, tone: 'warning' },
  critical: { icon: AlertTriangle, tone: 'danger' },
}

function ActivityCard({ data, loading }: { data?: DashboardSummary; loading: boolean }) {
  const { t } = useTranslation()
  return (
    <Card title={t('dashboard.activity.title')}>
      {loading ? (
        <div className="stack">
          {Array.from({ length: 5 }, (_, i) => (
            <div key={i} style={{ display: 'flex', gap: 12 }}>
              <Skeleton width={32} height={32} radius={8} />
              <div style={{ flex: 1 }} className="stack">
                <Skeleton height={12} />
                <Skeleton height={10} width="40%" />
              </div>
            </div>
          ))}
        </div>
      ) : data!.activity.length === 0 ? (
        <EmptyState icon={<ClipboardList size={22} />} title={t('dashboard.activity.empty')} text={t('dashboard.activity.emptyText')} />
      ) : (
        <ul className="activity">
          {data!.activity.map((a) => {
            const { icon: Icon, tone } = activityIcon[a.type]
            return (
              <li key={a.id}>
                <span className={`stat-icon tone-${tone}`} aria-hidden>
                  <Icon size={16} />
                </span>
                <div>
                  <p className="activity-text">
                    <Trans i18nKey={`dashboard.activity.${a.type}`} values={a.params} components={{ b: <strong /> }} />
                  </p>
                  <p className="activity-time">{waitLabel(a.minutesAgo, t)}</p>
                </div>
              </li>
            )
          })}
        </ul>
      )}
    </Card>
  )
}
