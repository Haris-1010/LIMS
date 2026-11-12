import { useEffect, useMemo, useState } from 'react'
import { createPortal } from 'react-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Printer } from 'lucide-react'
import { api } from '../../lib/api'
import { formatDateTime } from '../../lib/format'
import { Card } from '../../components/ui/Card'
import { Button } from '../../components/ui/Button'

interface HistoryPoint {
  dateUtc: string
  resultId: number
  testId: number
  testCode: string
  testNameEn: string
  testNameUr: string | null
  parameterId: number
  parameterCode: string
  parameterNameEn: string
  parameterNameUr: string | null
  value: number
  unit: string | null
  rangeLow: number | null
  rangeHigh: number | null
  flag: number
}

interface PatientHistory {
  patientId: number
  months: number | null
  points: HistoryPoint[]
}

const periods = [3, 6, 12] as const

export function PatientHistoryGraph({ patientId, patientName, patientNo }: { patientId: number; patientName: string; patientNo: string }) {
  const { t, i18n } = useTranslation()
  const [months, setMonths] = useState<number | null>(6)
  const [parameterId, setParameterId] = useState<number | null>(null)
  const { data, isLoading } = useQuery({
    queryKey: ['patients', 'history', patientId, months],
    queryFn: () => api<PatientHistory>(`/api/patients/${patientId}/history`, { query: { months: months ?? undefined } }),
  })

  const parameters = useMemo(() => {
    const map = new Map<number, HistoryPoint>()
    for (const point of data?.points ?? []) if (!map.has(point.parameterId)) map.set(point.parameterId, point)
    return [...map.values()]
  }, [data?.points])
  const selectedId = parameterId ?? parameters[0]?.parameterId ?? null
  const selected = (data?.points ?? []).filter((point) => point.parameterId === selectedId)
  const label = (point: HistoryPoint) => i18n.language === 'ur' && point.parameterNameUr ? point.parameterNameUr : point.parameterNameEn
  const testLabel = (point: HistoryPoint) => i18n.language === 'ur' && point.testNameUr ? point.testNameUr : point.testNameEn
  const [printing, setPrinting] = useState(false)

  return (
    <>
      <Card id="history-print-root" title={t('patients.historyTitle')} subtitle={t('patients.historySubtitle')}
        actions={<Button variant="secondary" size="sm" icon={<Printer size={15} />} onClick={() => setPrinting(true)} disabled={selected.length === 0}>{t('patients.printHistory')}</Button>}>
        <div className="history-print-heading">
          <strong>{patientName}</strong><span>{patientNo}</span><span>{t('patients.historyTitle')}</span>
          <span>{months ? t(`patients.historyMonths.${months}`) : t('patients.historyAll')}</span>
        </div>
        <div className="history-toolbar">
          <select className="select select-sm" value={months ?? 'all'} onChange={(e) => setMonths(e.target.value === 'all' ? null : Number(e.target.value))}>
            {periods.map((value) => <option key={value} value={value}>{t(`patients.historyMonths.${value}`)}</option>)}
            <option value="all">{t('patients.historyAll')}</option>
          </select>
          <select className="select select-sm" value={selectedId ?? ''} onChange={(e) => setParameterId(e.target.value ? Number(e.target.value) : null)} disabled={parameters.length === 0}>
            {parameters.map((point) => <option key={point.parameterId} value={point.parameterId}>{label(point)} · {point.testCode}</option>)}
          </select>
        </div>
        {isLoading ? <p className="field-hint">{t('common.loading')}</p> : selected.length === 0 ? (
          <p className="field-hint">{t('patients.historyEmpty')}</p>
        ) : (
          <HistoryChart points={selected} label={label} testLabel={testLabel} language={i18n.language} t={t} />
        )}
      </Card>
      {printing && selected.length > 0 && createPortal(
        <HistoryPrintDocument patientName={patientName} patientNo={patientNo} title={t('patients.historyTitle')}
          period={months ? t(`patients.historyMonths.${months}`) : t('patients.historyAll')} points={selected}
          label={label} testLabel={testLabel} language={i18n.language} t={t} onDone={() => setPrinting(false)} />,
        document.body,
      )}
    </>
  )
}

function HistoryPrintDocument({ patientName, patientNo, title, period, points, label, testLabel, language, t, onDone }: {
  patientName: string
  patientNo: string
  title: string
  period: string
  points: HistoryPoint[]
  label: (point: HistoryPoint) => string
  testLabel: (point: HistoryPoint) => string
  language: string
  t: (key: string, options?: Record<string, unknown>) => string
  onDone: () => void
}) {
  useEffect(() => {
    const cleanup = () => {
      document.body.classList.remove('printing-history')
      onDone()
    }
    document.body.classList.add('printing-history')
    window.addEventListener('afterprint', cleanup, { once: true })
    const timer = window.setTimeout(() => window.print(), 100)
    return () => {
      window.clearTimeout(timer)
      window.removeEventListener('afterprint', cleanup)
      document.body.classList.remove('printing-history')
    }
  }, [onDone])

  return (
    <main className="history-print-document" dir={language === 'ur' ? 'rtl' : 'ltr'}>
      <header><strong>{patientName}</strong><span>{patientNo}</span><h1>{title}</h1><span>{period}</span></header>
      <HistoryChart points={points} label={label} testLabel={testLabel} language={language} t={t} />
    </main>
  )
}

function HistoryChart({ points, label, testLabel, language, t }: {
  points: HistoryPoint[]
  label: (point: HistoryPoint) => string
  testLabel: (point: HistoryPoint) => string
  language: string
  t: (key: string, options?: Record<string, unknown>) => string
}) {
  const width = 720
  const height = 300
  const left = 48
  const right = 18
  const top = 24
  const bottom = 42
  const unit = points[points.length - 1]?.unit ?? points[0]?.unit ?? null
  const consistentPoints = points.filter((point) => point.unit === unit)
  const values = consistentPoints.flatMap((point) => [point.value, point.rangeLow, point.rangeHigh].filter((value): value is number => value !== null))
  const rawMin = Math.min(...values)
  const rawMax = Math.max(...values)
  const padding = rawMin === rawMax ? Math.max(Math.abs(rawMin) * 0.1, 1) : (rawMax - rawMin) * 0.12
  const min = rawMin - padding
  const max = rawMax + padding
  const x = (index: number) => left + (consistentPoints.length === 1 ? (width - left - right) / 2 : index * (width - left - right) / (consistentPoints.length - 1))
  const y = (value: number) => top + (max - value) * (height - top - bottom) / (max - min)
  const path = consistentPoints.map((point, index) => `${index === 0 ? 'M' : 'L'} ${x(index)} ${y(point.value)}`).join(' ')
  const dateFormat = language === 'ur' ? 'ur-PK' : 'en-GB'

  return (
    <div className="history-chart-wrap">
      <div className="history-chart-title"><div><strong>{label(points[0])}</strong><span>{testLabel(points[0])}</span></div><b>{unit ?? ''}</b></div>
      <svg className="history-chart" viewBox={`0 0 ${width} ${height}`} role="img" aria-label={label(points[0])} direction="ltr">
        <line x1={left} x2={left} y1={top} y2={height - bottom} className="history-axis" />
        <line x1={left} x2={width - right} y1={height - bottom} y2={height - bottom} className="history-axis" />
        {[0.25, 0.5, 0.75].map((fraction) => {
          const value = min + (max - min) * fraction
          return <g key={fraction}><line x1={left} x2={width - right} y1={y(value)} y2={y(value)} className="history-grid" /><text x={left - 8} y={y(value) + 4} textAnchor="end" className="history-axis-label">{value.toFixed(1)}</text></g>
        })}
        <text x={left - 8} y={top + 4} textAnchor="end" className="history-axis-label">{max.toFixed(1)}</text>
        <text x={left - 8} y={height - bottom + 4} textAnchor="end" className="history-axis-label">{min.toFixed(1)}</text>
        <path d={path} className="history-line" />
        {consistentPoints.map((point, index) => (
          <g key={`${point.resultId}-${point.parameterId}`}>
            {point.rangeLow !== null && point.rangeHigh !== null && <line x1={x(index)} x2={x(index)} y1={y(point.rangeHigh)} y2={y(point.rangeLow)} className="history-range" />}
            <circle cx={x(index)} cy={y(point.value)} r="5" className={`history-point flag-${point.flag}`}>
              <title>{`${point.value}${unit ? ` ${unit}` : ''} · ${formatDateTime(point.dateUtc, language)}`}</title>
            </circle>
            <text x={x(index)} y={y(point.value) - 12} textAnchor="middle" className="history-value">{point.value}</text>
            <text x={x(index)} y={height - bottom + 22} textAnchor="middle" className="history-date">{new Date(point.dateUtc).toLocaleDateString(dateFormat)}</text>
          </g>
        ))}
      </svg>
      <p className="field-hint">{unit ?? ''} · {t('patients.historyCount', { count: consistentPoints.length })} · {t('patients.historyRangeNote')}</p>
      {consistentPoints.length < points.length && <p className="field-hint">{t('patients.historyUnitNote')}</p>}
      <table className="history-points">
        <thead><tr><th>{t('patients.historyDate')}</th><th>{t('patients.historyValue')}</th><th>{t('patients.historyRange')}</th></tr></thead>
        <tbody>{[...consistentPoints].reverse().map((point) => (
          <tr key={point.resultId}><td>{formatDateTime(point.dateUtc, language)}</td><td className="num">{point.value} {point.unit ?? ''}</td>
            <td className="num">{point.rangeLow ?? '—'} – {point.rangeHigh ?? '—'}</td></tr>
        ))}</tbody>
      </table>
    </div>
  )
}
