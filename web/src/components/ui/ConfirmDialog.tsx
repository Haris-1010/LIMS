import { useEffect, useRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
import { Button } from './Button'
import { pushOverlay } from '../../lib/overlayStack'

interface ConfirmDialogProps {
  open: boolean
  title: string
  text: string
  icon: ReactNode
  tone?: 'primary' | 'danger'
  confirmLabel?: string
  loading?: boolean
  /** Information-only dialog: show just the confirm button. */
  hideCancel?: boolean
  onConfirm: () => void
  onCancel: () => void
}

/**
 * Accessible confirmation dialog used instead of window.confirm().
 * Focus moves to Cancel (the safe action), Escape closes, and focus returns on close.
 */
export function ConfirmDialog({
  open,
  title,
  text,
  icon,
  tone = 'primary',
  confirmLabel,
  loading,
  hideCancel,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const { t } = useTranslation()
  const cancelRef = useRef<HTMLButtonElement>(null)
  const confirmRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    if (!open) return
    const previous = document.activeElement as HTMLElement | null
    ;(cancelRef.current ?? confirmRef.current)?.focus()
    const overlay = pushOverlay()
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && overlay.isTop()) {
        e.stopPropagation()
        onCancel()
      }
    }
    document.addEventListener('keydown', onKey)
    return () => {
      overlay.pop()
      document.removeEventListener('keydown', onKey)
      previous?.focus()
    }
  }, [open, onCancel])

  if (!open) return null

  return createPortal(
    <div className="overlay" onMouseDown={(e) => e.target === e.currentTarget && onCancel()}>
      <div className="dialog" role="alertdialog" aria-modal="true" aria-labelledby="dlg-title" aria-describedby="dlg-text">
        <div className="dialog-body">
          <div className={`dialog-icon tone-${tone}`} aria-hidden>
            {icon}
          </div>
          <div>
            <h2 id="dlg-title" className="dialog-title">
              {title}
            </h2>
            <p id="dlg-text" className="dialog-text">
              {text}
            </p>
          </div>
        </div>
        <div className="dialog-actions">
          {!hideCancel && (
            <Button ref={cancelRef} onClick={onCancel}>
              {t('common.cancel')}
            </Button>
          )}
          <Button ref={confirmRef} variant={tone === 'danger' ? 'danger' : 'primary'} loading={loading} onClick={onConfirm}>
            {confirmLabel ?? t('common.confirm')}
          </Button>
        </div>
      </div>
    </div>,
    document.body,
  )
}
