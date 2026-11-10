import { useEffect } from 'react'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
import { formatShortDate } from '../../lib/format'
import { formatAge, patientName } from '../patients/patientsApi'
import { Barcode } from './Barcode'
import type { LabelSettings } from './labelSettings'
import { pick, type SampleListItem } from './samplesApi'

/** One specimen label. Sized by CSS variables so the same markup serves the on-screen preview and the printer. */
export function SampleLabel({ sample, settings }: { sample: SampleListItem; settings: LabelSettings }) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const name = patientName({ fullName: sample.patientName, fullNameUr: sample.patientNameUr }, lang)
  const type = pick(sample.sampleTypeNameEn, sample.sampleTypeNameUr, lang)
  return (
    <div className="sample-label" style={{ ['--label-w' as string]: `${settings.widthMm}mm`, ['--label-h' as string]: `${settings.heightMm}mm` }}>
      <div className="sl-top">
        <strong dir="auto" className="sl-name">{name}</strong>
        {sample.priority === 1 && <span className="sl-urgent">{t('orders.urgent')}</span>}
      </div>
      <div className="sl-meta num">
        {sample.patientNo} · {formatAge(t, sample.patientDateOfBirth)} · {t(`sex.short${sample.patientSex}`)}
      </div>
      <div className="sl-bars"><Barcode modules={sample.barcodeModules} label={sample.barcode} height={Math.max(20, settings.heightMm * 0.34 * 4)} /></div>
      <div className="sl-code num">{sample.barcode}</div>
      <div className="sl-foot">
        <span dir="auto">{type}{sample.capColour ? ` · ${sample.capColour}` : ''}</span>
        <span className="num">{sample.testCodes.slice(0, 4).join(' ')}{sample.testCodes.length > 4 ? ' …' : ''}</span>
      </div>
      <div className="sl-foot sl-date num">{formatShortDate(new Date(sample.collectedAtUtc ?? sample.createdAtUtc), lang)} · {sample.currentBranch}</div>
    </div>
  )
}

/** Renders the labels into <body>, prints them (the print stylesheet hides the app), then cleans up and records the print. */
export function PrintJob({ samples, settings, onDone }: { samples: SampleListItem[]; settings: LabelSettings; onDone: () => void }) {
  const { i18n } = useTranslation()
  useEffect(() => {
    const style = document.createElement('style')
    style.textContent = `@page { size: ${settings.widthMm}mm ${settings.heightMm}mm; margin: 0; }`
    document.head.appendChild(style)
    document.body.classList.add('printing-labels')
    let finished = false
    const finish = () => {
      if (finished) return
      finished = true
      style.remove()
      document.body.classList.remove('printing-labels')
      onDone()
    }
    window.addEventListener('afterprint', finish, { once: true })
    const h = window.setTimeout(() => window.print(), 200)
    return () => {
      window.clearTimeout(h)
      window.removeEventListener('afterprint', finish)
      finish()
    }
    // The job is created once per print request.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return createPortal(
    <div className="label-sheet" dir={i18n.dir()} lang={i18n.language}>
      {samples.flatMap((s) => Array.from({ length: settings.copies }, (_, i) => <SampleLabel key={`${s.id}-${i}`} sample={s} settings={settings} />))}
    </div>,
    document.body,
  )
}
