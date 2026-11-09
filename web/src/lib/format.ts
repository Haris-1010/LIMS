/**
 * Formatting helpers. Numbers and money always use Latin digits (en-PK grouping) in both
 * languages so clinical values, invoice amounts and IDs read the same on screen and in print.
 * Dates use the active language's month names.
 */
const numberFmt = new Intl.NumberFormat('en-PK')
const compactFmt = new Intl.NumberFormat('en-PK', { notation: 'compact', maximumFractionDigits: 1 })

export function formatNumber(value: number): string {
  return numberFmt.format(value)
}

export function formatCompact(value: number): string {
  return compactFmt.format(value)
}

/** PKR with no decimals for display totals, e.g. "Rs 12,500". */
export function formatMoney(value: number, opts: { decimals?: number } = {}): string {
  const decimals = opts.decimals ?? 0
  return `Rs ${new Intl.NumberFormat('en-PK', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(value)}`
}

export function formatShortDate(date: Date, lang: string): string {
  return new Intl.DateTimeFormat(lang === 'ur' ? 'ur-PK' : 'en-GB', { day: 'numeric', month: 'short' }).format(date)
}

export function formatLongDate(date: Date, lang: string): string {
  return new Intl.DateTimeFormat(lang === 'ur' ? 'ur-PK' : 'en-GB', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(date)
}

/** UTC timestamp from the API → local date and time, e.g. "9 Nov 2026, 14:05". */
export function formatDateTime(utc: string, lang: string): string {
  return new Intl.DateTimeFormat(lang === 'ur' ? 'ur-PK' : 'en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(utc))
}

export function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]!.toUpperCase())
    .join('')
}
