import { useEffect, useMemo, useRef, useState } from 'react'
import { CalendarDays, ChevronLeft, ChevronRight, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from './Button'
import './DateRangeFilter.css'

export interface DateRangeValue { from: string; to: string }

export function DateRangeFilter({ value, onChange }: { value: DateRangeValue; onChange: (value: DateRangeValue) => void }) {
  const { t, i18n } = useTranslation()
  const root = useRef<HTMLDivElement>(null)
  const [open, setOpen] = useState<'from' | 'to' | null>(null)
  const [month, setMonth] = useState(() => {
    const source = value.from || value.to
    const date = source ? parseDay(source) : new Date()
    return new Date(date.getFullYear(), date.getMonth(), 1)
  })
  const monthLabel = new Intl.DateTimeFormat(i18n.language === 'ur' ? 'ur-PK' : 'en-GB', { month: 'long', year: 'numeric' }).format(month)
  const weekdays = useMemo(() => {
    const formatter = new Intl.DateTimeFormat(i18n.language === 'ur' ? 'ur-PK' : 'en-GB', { weekday: 'short' })
    return Array.from({ length: 7 }, (_, index) => formatter.format(new Date(2024, 0, 7 + index)))
  }, [i18n.language])
  const days = useMemo(() => {
    const first = new Date(month.getFullYear(), month.getMonth(), 1)
    const start = new Date(first)
    start.setDate(1 - first.getDay())
    return Array.from({ length: 42 }, (_, index) => {
      const day = new Date(start)
      day.setDate(start.getDate() + index)
      return day
    })
  }, [month])

  useEffect(() => {
    const onDown = (event: MouseEvent) => { if (root.current && !root.current.contains(event.target as Node)) setOpen(null) }
    document.addEventListener('mousedown', onDown)
    return () => document.removeEventListener('mousedown', onDown)
  }, [])

  const choose = (day: Date) => {
    const selected = toDay(day)
    if (open === 'from') {
      onChange({ from: selected, to: value.to && selected <= value.to ? value.to : '' })
      setOpen(value.to && selected <= value.to ? null : 'to')
    } else {
      if (value.from && selected < value.from) onChange({ from: selected, to: value.from })
      else onChange({ from: value.from, to: selected })
      setOpen(null)
    }
  }
  const openPicker = (field: 'from' | 'to') => {
    const source = field === 'from' ? value.from : value.to
    if (source) setMonth(new Date(parseDay(source).getFullYear(), parseDay(source).getMonth(), 1))
    setOpen(field)
  }

  return (
    <div ref={root} className="date-range-filter" role="group" aria-label={t('common.dateRange')}>
      <span className="date-range-icon"><CalendarDays size={17} aria-hidden /></span>
      <span className="date-range-title">{t('common.dateRange')}</span>
      <button type="button" className={`date-range-field${open === 'from' ? ' is-open' : ''}`} onClick={() => openPicker('from')} aria-expanded={open === 'from'}>
        <span>{t('common.from')}</span><strong>{value.from ? displayDay(value.from, i18n.language) : t('common.selectDate')}</strong>
      </button>
      <span className="date-range-separator" aria-hidden>→</span>
      <button type="button" className={`date-range-field${open === 'to' ? ' is-open' : ''}`} onClick={() => openPicker('to')} aria-expanded={open === 'to'}>
        <span>{t('common.to')}</span><strong>{value.to ? displayDay(value.to, i18n.language) : t('common.selectDate')}</strong>
      </button>
      {(value.from || value.to) && <Button size="sm" variant="ghost" icon={<X size={14} />} aria-label={t('common.clear')} title={t('common.clear')} onClick={() => onChange({ from: '', to: '' })} />}
      {open && <div className="date-picker-popover">
        <div className="date-picker-head">
          <button type="button" className="date-picker-nav" onClick={() => setMonth(new Date(month.getFullYear(), month.getMonth() - 1, 1))} aria-label={t('common.previous')}><ChevronLeft size={17} className="flip-rtl" /></button>
          <strong>{monthLabel}</strong>
          <button type="button" className="date-picker-nav" onClick={() => setMonth(new Date(month.getFullYear(), month.getMonth() + 1, 1))} aria-label={t('common.next')}><ChevronRight size={17} className="flip-rtl" /></button>
        </div>
        <div className="date-picker-weekdays">{weekdays.map((day) => <span key={day}>{day}</span>)}</div>
        <div className="date-picker-grid">
          {days.map((day) => {
            const iso = toDay(day)
            const outside = day.getMonth() !== month.getMonth()
            const selected = iso === value.from || iso === value.to
            const between = !!value.from && !!value.to && iso > value.from && iso < value.to
            return <button key={iso} type="button" className={`date-picker-day${outside ? ' is-outside' : ''}${selected ? ' is-selected' : ''}${between ? ' is-between' : ''}`} onClick={() => choose(day)}>{day.getDate()}</button>
          })}
        </div>
        <button type="button" className="date-picker-today" onClick={() => choose(new Date())}>{t('common.today')}</button>
      </div>}
    </div>
  )
}

function parseDay(value: string) { const [year, month, day] = value.split('-').map(Number); return new Date(year, month - 1, day) }
function toDay(date: Date) { return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}` }
function displayDay(value: string, language: string) { return new Intl.DateTimeFormat(language === 'ur' ? 'ur-PK' : 'en-GB', { day: '2-digit', month: 'short', year: 'numeric' }).format(parseDay(value)) }
