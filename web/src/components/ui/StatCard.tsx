import type { ReactNode } from 'react'
import { ArrowDownRight, ArrowRight, ArrowUpRight } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Skeleton } from './Skeleton'

interface StatCardProps {
  label: string
  value: string
  icon: ReactNode
  tone?: 'primary' | 'accent' | 'warning' | 'danger' | 'success'
  /** Percent change versus the previous period. Positive = up. */
  delta?: number
  /** Whether "up" is good for this metric (false for e.g. pending samples). */
  upIsGood?: boolean
  foot?: ReactNode
  loading?: boolean
  /** When set, the whole card links to this page. */
  to?: string
}

export function StatCard({ label, value, icon, tone = 'primary', delta, upIsGood = true, foot, loading, to }: StatCardProps) {
  if (loading) {
    return (
      <div className="card stat" aria-busy>
        <div className="stat-top">
          <Skeleton width={110} height={14} />
          <Skeleton width={34} height={34} radius={8} />
        </div>
        <Skeleton width={90} height={28} />
        <Skeleton width={140} height={12} />
      </div>
    )
  }

  const good = delta === undefined ? undefined : delta >= 0 === upIsGood
  const body = (
    <>
      <div className="stat-top">
        <span className="stat-label">{label}</span>
        <span className={`stat-icon tone-${tone}`} aria-hidden>
          {icon}
        </span>
      </div>
      <span className="stat-value">{value}</span>
      <span className="stat-foot">
        {delta !== undefined && (
          <span className={`delta ${good ? 'up' : 'down'}`}>
            {delta >= 0 ? <ArrowUpRight size={13} aria-hidden /> : <ArrowDownRight size={13} aria-hidden />}
            {Math.abs(delta).toFixed(1)}%
          </span>
        )}
        {foot}
        {to && <ArrowRight size={14} className="stat-go flip-rtl" aria-hidden />}
      </span>
    </>
  )

  return to ? (
    <Link to={to} className="card stat stat-link">
      {body}
    </Link>
  ) : (
    <div className="card stat">{body}</div>
  )
}
