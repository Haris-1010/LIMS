import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { CheckCircle2, Info, X, XCircle } from 'lucide-react'
import { useTranslation } from 'react-i18next'

type ToastTone = 'info' | 'success' | 'danger'
interface ToastItem {
  id: number
  tone: ToastTone
  title: string
  text?: string
}
interface ToastApi {
  show: (toast: Omit<ToastItem, 'id'>) => void
}

const ToastContext = createContext<ToastApi | null>(null)
let nextId = 1

export function ToastProvider({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const [items, setItems] = useState<ToastItem[]>([])

  const dismiss = useCallback((id: number) => setItems((list) => list.filter((x) => x.id !== id)), [])
  const show = useCallback(
    (toast: Omit<ToastItem, 'id'>) => {
      const id = nextId++
      setItems((list) => [...list.slice(-3), { ...toast, id }])
      window.setTimeout(() => dismiss(id), toast.tone === 'danger' ? 8000 : 4500)
    },
    [dismiss],
  )
  const api = useMemo(() => ({ show }), [show])

  const icons = { info: Info, success: CheckCircle2, danger: XCircle }

  return (
    <ToastContext.Provider value={api}>
      {children}
      {createPortal(
        <div className="toasts" role="region" aria-live="polite" aria-label="Notifications">
          {items.map((item) => {
            const Icon = icons[item.tone]
            return (
              <div key={item.id} className={`toast tone-${item.tone}`} role={item.tone === 'danger' ? 'alert' : 'status'}>
                <Icon size={18} className="toast-icon" aria-hidden />
                <div>
                  <p className="toast-title">{item.title}</p>
                  {item.text && <p className="toast-text">{item.text}</p>}
                </div>
                <button className="icon-btn toast-close" style={{ width: 28, height: 28 }} onClick={() => dismiss(item.id)} aria-label={t('common.close')}>
                  <X size={15} />
                </button>
              </div>
            )
          })}
        </div>,
        document.body,
      )}
    </ToastContext.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function useToast(): ToastApi {
  const ctx = useContext(ToastContext)
  if (!ctx) throw new Error('useToast must be used inside <ToastProvider>')
  return ctx
}
