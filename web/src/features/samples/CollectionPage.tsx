import { useEffect, useMemo, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ScanBarcode, Settings2, TestTubes } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { EmptyState } from '../../components/ui/EmptyState'
import { Field } from '../../components/ui/Field'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import { formatAge, patientName } from '../patients/patientsApi'
import { CollectDialog, SampleDetailModal } from './SampleParts'
import { LabelSettingsModal } from './LabelSettingsModal'
import { pick, sampleKeys, type SampleDetail, type SampleListItem } from './samplesApi'
import './samples.css'

/** Phlebotomy worklist: Planned samples at the working branch, urgent first. Scan a barcode or pick a card. */
export function CollectionPage() {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const inputRef = useRef<HTMLInputElement>(null)
  const [filter, setFilter] = useState('')
  const [collecting, setCollecting] = useState<SampleListItem | null>(null)
  const [viewing, setViewing] = useState<number | null>(null)
  const [settings, setSettings] = useState(false)

  const { data, isLoading, error } = useQuery({ queryKey: sampleKeys.queue, queryFn: () => api<SampleListItem[]>('/api/samples/collection-queue'), refetchInterval: 30_000 })

  useEffect(() => { inputRef.current?.focus() }, [])

  const rows = useMemo(() => {
    const term = filter.trim().toLowerCase()
    if (!data) return undefined
    if (!term) return data
    return data.filter((s) => [s.barcode, s.patientName, s.patientNameUr ?? '', s.patientNo, s.orderNo].some((v) => v.toLowerCase().includes(term)))
  }, [data, filter])

  /** Enter in the box: a scanner types the barcode and presses Enter, so an exact barcode opens that sample directly. */
  const submit = async () => {
    const value = filter.trim()
    if (!value) return
    try {
      const d = await api<SampleDetail>(`/api/samples/by-barcode/${encodeURIComponent(value)}`)
      setFilter('')
      if (d.actions.collect) setCollecting(d.sample)
      else setViewing(d.sample.id)
    } catch {
      // Not a barcode: the text keeps filtering the list by patient, order or number.
      if (rows?.length === 1) setCollecting(rows[0]!)
      else if (!rows?.length) toast.show({ tone: 'info', title: t('samples.nothingFound') })
    }
  }

  return (
    <div className="page">
      <div className="page-head">
        <div><h2>{t('samples.collectionTitle')}</h2><p>{t('samples.collectionSubtitle')}</p></div>
        <Button variant="secondary" icon={<Settings2 size={16} />} onClick={() => setSettings(true)}>{t('samples.labelSettings')}</Button>
      </div>

      <section className="card" style={{ padding: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
        <div className="scan-box">
          <Field ref={inputRef} className="scan-input" label={t('samples.scanOrSearch')} value={filter} placeholder={t('samples.scanPlaceholder')} autoComplete="off" inputMode="text"
            onChange={(e) => setFilter(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); void submit() } }} />
          <Badge tone="primary"><ScanBarcode size={14} aria-hidden /> {t('samples.waiting', { count: data?.length ?? 0 })}</Badge>
        </div>
      </section>

      {isLoading && <div className="queue-grid">{[0, 1, 2].map((i) => <Skeleton key={i} height={150} radius={12} />)}</div>}
      {error && <p className="text-warning">{errorMessage(t, error)}</p>}
      {rows && rows.length === 0 && (
        <section className="card"><EmptyState icon={<TestTubes size={22} />} title={filter ? t('samples.nothingFound') : t('samples.queueEmptyTitle')} text={filter ? t('samples.nothingFoundText') : t('samples.queueEmptyText')} /></section>
      )}
      {rows && rows.length > 0 && (
        <div className="queue-grid">
          {rows.map((s) => (
            <article key={s.id} className={`queue-card${s.priority === 1 ? ' is-urgent' : ''}`}>
              <header>
                <div style={{ minWidth: 0 }}>
                  <strong dir="auto">{patientName({ fullName: s.patientName, fullNameUr: s.patientNameUr }, i18n.language)}</strong>
                  <span className="cell-sub num">{s.patientNo} · {formatAge(t, s.patientDateOfBirth)} · {t(`sex.short${s.patientSex}`)}</span>
                </div>
                <div className="badge-list">
                  {s.priority === 1 && <Badge tone="danger">{t('orders.urgent')}</Badge>}
                  {s.isRecollection && <Badge tone="warning">{t('samples.recollectionBadge')}</Badge>}
                </div>
              </header>
              <div>
                <span className="cap-dot" style={{ ['--cap' as string]: s.capColour ?? undefined }} aria-hidden />
                <strong>{pick(s.sampleTypeNameEn, s.sampleTypeNameUr, i18n.language)}</strong>
                {s.container && <span className="cell-sub">{s.container}</span>}
              </div>
              <div className="cell-sub num">{s.testCodes.join(', ')}</div>
              <div className="cell-sub num">{s.orderNo} · {s.barcode}</div>
              <footer>
                <Button variant="primary" size="sm" onClick={() => setCollecting(s)}>{t('samples.collectAction')}</Button>
                <Button variant="ghost" size="sm" onClick={() => setViewing(s.id)}>{t('samples.details')}</Button>
              </footer>
            </article>
          ))}
        </div>
      )}

      {collecting && <CollectDialog sample={collecting} onClose={() => { setCollecting(null); inputRef.current?.focus() }} />}
      {viewing !== null && <SampleDetailModal id={viewing} onClose={() => setViewing(null)} />}
      {settings && <LabelSettingsModal onClose={() => setSettings(false)} sample={data?.[0]} />}
    </div>
  )
}
