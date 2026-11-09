import type { ReactNode } from 'react'

export type Tone = 'neutral' | 'primary' | 'success' | 'warning' | 'danger' | 'info' | 'accent'

export function Badge({ tone = 'neutral', dot, children }: { tone?: Tone; dot?: boolean; children: ReactNode }) {
  return <span className={`badge badge-${tone}${dot ? ' badge-dot' : ''}`}>{children}</span>
}
