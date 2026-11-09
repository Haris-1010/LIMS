import { useEffect, useId, useRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { overlayCount, pushOverlay } from '../../lib/overlayStack'

interface ModalProps {
  open: boolean
  title: ReactNode
  subtitle?: ReactNode
  size?: 'sm' | 'md' | 'lg' | 'xl'
  onClose: () => void
  footer?: ReactNode
  children: ReactNode
  /** Rendered between the header and the scrolling body (e.g. tabs). */
  toolbar?: ReactNode
}

/**
 * Accessible modal dialog for forms: focus is trapped inside, Escape and the backdrop close it,
 * focus returns to the opener afterwards. The body scrolls; header and footer stay visible.
 */
export function Modal({ open, title, subtitle, size = 'md', onClose, footer, children, toolbar }: ModalProps) {
  const { t } = useTranslation()
  const ref = useRef<HTMLDivElement>(null)
  const titleId = useId()
  const closeRef = useRef(onClose)
  closeRef.current = onClose

  useEffect(() => {
    if (!open) return
    const previous = document.activeElement as HTMLElement | null
    const node = ref.current
    // Focus the first form control, or the dialog itself.
    const first = node?.querySelector<HTMLElement>('input:not([type=hidden]):not([disabled]), select, textarea')
    ;(first ?? node)?.focus()

    const overlay = pushOverlay()
    const onKey = (e: KeyboardEvent) => {
      if (!overlay.isTop()) return
      if (e.key === 'Escape') {
        e.stopPropagation()
        closeRef.current()
      }
      if (e.key !== 'Tab' || !node) return
      const items = [...node.querySelectorAll<HTMLElement>('a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])')]
      if (items.length === 0) return
      const [firstItem, lastItem] = [items[0]!, items[items.length - 1]!]
      if (e.shiftKey && document.activeElement === firstItem) {
        e.preventDefault()
        lastItem.focus()
      } else if (!e.shiftKey && document.activeElement === lastItem) {
        e.preventDefault()
        firstItem.focus()
      }
    }
    document.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      overlay.pop()
      document.removeEventListener('keydown', onKey)
      if (overlayCount() === 0) document.body.style.overflow = ''
      previous?.focus()
    }
  }, [open])

  if (!open) return null

  return createPortal(
    <div className="overlay" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div ref={ref} className={`modal modal-${size}`} role="dialog" aria-modal="true" aria-labelledby={titleId} tabIndex={-1}>
        <header className="modal-header">
          <div>
            <h2 id={titleId} className="modal-title">
              {title}
            </h2>
            {subtitle && <p className="modal-subtitle">{subtitle}</p>}
          </div>
          <button type="button" className="icon-btn" onClick={onClose} aria-label={t('common.close')}>
            <X size={18} />
          </button>
        </header>
        {toolbar}
        <div className="modal-body">{children}</div>
        {footer && <footer className="modal-footer">{footer}</footer>}
      </div>
    </div>,
    document.body,
  )
}
