import { forwardRef, useId, type InputHTMLAttributes, type ReactNode } from 'react'
import { AlertCircle } from 'lucide-react'

interface FieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  error?: string
  hint?: string
  icon?: ReactNode
  /** Element shown inside the input at the end, e.g. a show-password button. */
  action?: ReactNode
}

/** Labelled text input with icon, hint and accessible error message. */
export const Field = forwardRef<HTMLInputElement, FieldProps>(function Field(
  { label, error, hint, icon, action, required, id, className, ...rest },
  ref,
) {
  const autoId = useId()
  const inputId = id ?? autoId
  const describedBy = error ? `${inputId}-error` : hint ? `${inputId}-hint` : undefined

  return (
    <div className="field">
      <label className="field-label" htmlFor={inputId}>
        {label}
        {required && (
          <span className="req" aria-hidden>
            *
          </span>
        )}
      </label>
      <div className="input-wrap">
        {icon && <span className="input-icon">{icon}</span>}
        <input
          ref={ref}
          id={inputId}
          className={['input', icon && 'has-icon', action && 'has-action', className].filter(Boolean).join(' ')}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          aria-required={required || undefined}
          {...rest}
        />
        {action && <span className="input-action">{action}</span>}
      </div>
      {error ? (
        <span id={`${inputId}-error`} className="field-error" role="alert">
          <AlertCircle size={13} aria-hidden />
          {error}
        </span>
      ) : (
        hint && (
          <span id={`${inputId}-hint`} className="field-hint">
            {hint}
          </span>
        )
      )}
    </div>
  )
})
