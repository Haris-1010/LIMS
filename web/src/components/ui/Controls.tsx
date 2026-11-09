import { useEffect, useId, useRef, useState, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react'
import { Search, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'

/* ---------------- Switch ---------------- */
export function Switch({
  checked,
  onChange,
  label,
  hint,
  disabled,
}: {
  checked: boolean
  onChange: (v: boolean) => void
  label: string
  hint?: string
  disabled?: boolean
}) {
  const id = useId()
  return (
    <div className="switch-row">
      <button
        id={id}
        type="button"
        role="switch"
        aria-checked={checked}
        className="switch"
        disabled={disabled}
        onClick={() => onChange(!checked)}
        aria-describedby={hint ? `${id}-hint` : undefined}
      >
        <span className="switch-thumb" />
      </button>
      <label htmlFor={id} className="switch-text">
        <span className="switch-label">{label}</span>
        {hint && (
          <span id={`${id}-hint`} className="field-hint">
            {hint}
          </span>
        )}
      </label>
    </div>
  )
}

/* ---------------- Select field ---------------- */
interface SelectFieldProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label: string
  hint?: string
  error?: string
  children: ReactNode
}

export function SelectField({ label, hint, error, children, id, required, ...rest }: SelectFieldProps) {
  const autoId = useId()
  const sid = id ?? autoId
  return (
    <div className="field">
      <label className="field-label" htmlFor={sid}>
        {label}
        {required && <span className="req" aria-hidden>*</span>}
      </label>
      <select id={sid} className="select" aria-invalid={error ? true : undefined} required={required} {...rest}>
        {children}
      </select>
      {error ? <span className="field-error" role="alert">{error}</span> : hint && <span className="field-hint">{hint}</span>}
    </div>
  )
}

/* ---------------- Textarea field ---------------- */
interface TextAreaFieldProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label: string
  error?: string
}

export function TextAreaField({ label, error, id, ...rest }: TextAreaFieldProps) {
  const autoId = useId()
  const tid = id ?? autoId
  return (
    <div className="field">
      <label className="field-label" htmlFor={tid}>
        {label}
      </label>
      <textarea id={tid} className="input textarea" aria-invalid={error ? true : undefined} {...rest} />
      {error && <span className="field-error" role="alert">{error}</span>}
    </div>
  )
}

/* ---------------- Search input (debounced) ---------------- */
export function SearchInput({
  value,
  onChange,
  placeholder,
  delay = 300,
  autoFocus,
}: {
  value: string
  onChange: (v: string) => void
  placeholder?: string
  delay?: number
  autoFocus?: boolean
}) {
  const { t } = useTranslation()
  const [text, setText] = useState(value)
  const cb = useRef(onChange)
  cb.current = onChange

  useEffect(() => setText(value), [value])
  useEffect(() => {
    if (text === value) return
    const h = window.setTimeout(() => cb.current(text), delay)
    return () => window.clearTimeout(h)
  }, [text, value, delay])

  return (
    <div className="input-wrap search-input">
      <Search size={16} className="input-icon" aria-hidden />
      <input
        type="search"
        className="input has-icon has-action"
        value={text}
        autoFocus={autoFocus}
        placeholder={placeholder ?? t('common.search')}
        aria-label={placeholder ?? t('common.search')}
        onChange={(e) => setText(e.target.value)}
        onKeyDown={(e) => e.key === 'Enter' && cb.current(text)}
      />
      {text && (
        <button type="button" className="icon-btn input-action" style={{ width: 30, height: 30 }} onClick={() => { setText(''); cb.current('') }} aria-label={t('table.clearFilters')}>
          <X size={14} />
        </button>
      )}
    </div>
  )
}

/* ---------------- Tabs ---------------- */
export function Tabs<T extends string>({
  tabs,
  value,
  onChange,
}: {
  tabs: { key: T; label: string; count?: number }[]
  value: T
  onChange: (key: T) => void
}) {
  return (
    <div className="tabs" role="tablist">
      {tabs.map((tab) => (
        <button
          key={tab.key}
          type="button"
          role="tab"
          aria-selected={value === tab.key}
          className="tab"
          onClick={() => onChange(tab.key)}
          onKeyDown={(e) => {
            if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return
            const rtl = document.documentElement.dir === 'rtl'
            const step = (e.key === 'ArrowRight') !== rtl ? 1 : -1
            const i = tabs.findIndex((x) => x.key === value)
            onChange(tabs[(i + step + tabs.length) % tabs.length]!.key)
          }}
        >
          {tab.label}
          {tab.count !== undefined && <span className="tab-count num">{tab.count}</span>}
        </button>
      ))}
    </div>
  )
}

/* ---------------- Checkbox list ---------------- */
export function CheckList({
  items,
  selected,
  onChange,
  columns = 2,
  disabled,
}: {
  items: { id: number | string; label: string; sub?: string }[]
  selected: (number | string)[]
  onChange: (next: (number | string)[]) => void
  columns?: number
  disabled?: boolean
}) {
  return (
    <div className="check-list" style={{ gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))` }}>
      {items.map((item) => {
        const on = selected.includes(item.id)
        return (
          <label key={item.id} className={`check-card${on ? ' is-on' : ''}${disabled ? ' is-disabled' : ''}`}>
            <input
              type="checkbox"
              checked={on}
              disabled={disabled}
              onChange={() => onChange(on ? selected.filter((x) => x !== item.id) : [...selected, item.id])}
            />
            <span>
              <span className="check-label">{item.label}</span>
              {item.sub && <span className="check-sub">{item.sub}</span>}
            </span>
          </label>
        )
      })}
    </div>
  )
}

/* ---------------- Form section ---------------- */
export function FormSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <fieldset className="form-section">
      <legend className="form-section-title">{title}</legend>
      {children}
    </fieldset>
  )
}
