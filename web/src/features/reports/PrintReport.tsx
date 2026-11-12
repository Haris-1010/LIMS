import { useEffect, useRef } from 'react'
import { createPortal } from 'react-dom'
import { ReportDocument } from './ReportDocument'
import { CLASSIC } from './reportDesign'
import { NO_IMAGES, type ReportDesign, type ReportImages, type ReportLanguage, type ReportView } from './reportsApi'

/**
 * Prints one report: renders it into <body>, waits for the fonts (so Urdu is drawn with the local Nastaliq font, not a fallback),
 * opens the print dialog on A4 and cleans up afterwards. The print stylesheet hides the rest of the app. "Save as PDF" in the
 * print dialog produces the PDF with the same layout. The design's margins become the page margins. Each test is its own page,
 * fitted to one sheet, so the print waits for fonts and images (logo, header, footer, signatures) before opening the dialog.
 */
export function PrintReport({ view, images = NO_IMAGES, design = CLASSIC, onlyTests, onlyParameters, graphTests, language, onDone }: {
  view: ReportView
  images?: ReportImages
  design?: ReportDesign
  onlyTests?: number[]
  onlyParameters?: Record<number, string[]>
  graphTests?: string[]
  language?: ReportLanguage
  onDone: () => void
}) {
  const root = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const style = document.createElement('style')
    style.textContent = `@page { size: A4; margin: ${design.marginTopMm}mm ${design.marginSideMm}mm ${design.marginBottomMm}mm; }`
    document.head.appendChild(style)
    document.body.classList.add('printing-report')
    let finished = false
    const finish = (printed: boolean) => {
      if (finished) return
      finished = true
      style.remove()
      document.body.classList.remove('printing-report')
      if (printed) onDone()
    }
    const onAfter = () => finish(true)
    window.addEventListener('afterprint', onAfter, { once: true })
    let timer = 0
    // Fonts load on first use, so ask for the Urdu faces explicitly (and wait) before the print dialog opens; otherwise the first
    // print of an Urdu report could be drawn with a fallback font.
    const needsUrdu = (language ?? view.snapshot.language) !== 0
    const loads = needsUrdu && document.fonts
      ? Promise.all([document.fonts.load('400 16px "Noto Nastaliq Urdu"', 'اردو'), document.fonts.load('600 16px "Noto Nastaliq Urdu"', 'اردو')]).catch(() => [])
      : Promise.resolve([])
    const imagesLoaded = () => Promise.all([...(root.current?.querySelectorAll('img') ?? [])].map((img) =>
      img.complete ? Promise.resolve() : new Promise<void>((done) => { img.addEventListener('load', () => done(), { once: true }); img.addEventListener('error', () => done(), { once: true }) })))
    void loads.then(() => (document.fonts?.ready ?? Promise.resolve())).then(imagesLoaded).then(() => { timer = window.setTimeout(() => window.print(), 250) })
    return () => {
      window.clearTimeout(timer)
      window.removeEventListener('afterprint', onAfter)
      finish(false)
    }
    // One print job per mount.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return createPortal(<div className="report-print-root" ref={root}><ReportDocument view={view} images={images} design={design} onlyTests={onlyTests} onlyParameters={onlyParameters} graphTests={graphTests} language={language} /></div>, document.body)
}
