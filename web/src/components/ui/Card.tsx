import type { HTMLAttributes, ReactNode } from 'react'

interface CardProps extends Omit<HTMLAttributes<HTMLElement>, 'title'> {
  title?: ReactNode
  subtitle?: ReactNode
  actions?: ReactNode
  footer?: ReactNode
  /** Remove body padding, e.g. for edge-to-edge tables. */
  flush?: boolean
}

export function Card({ title, subtitle, actions, footer, flush, className, children, ...rest }: CardProps) {
  return (
    <section className={['card', className].filter(Boolean).join(' ')} {...rest}>
      {(title || actions) && (
        <header className="card-header">
          <div>
            {title && <h3 className="card-title">{title}</h3>}
            {subtitle && <p className="card-subtitle">{subtitle}</p>}
          </div>
          {actions}
        </header>
      )}
      <div className={flush ? undefined : 'card-body'}>{children}</div>
      {footer && <footer className="card-footer">{footer}</footer>}
    </section>
  )
}
