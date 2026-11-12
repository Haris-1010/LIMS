import { Fragment, useCallback, useEffect, useLayoutEffect, useRef, type CSSProperties, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDateTime } from '../../lib/format'
import { CLASSIC, designClasses, designVars, gaugeScale, niceStep, rangeBar } from './reportDesign'
import { docLang, NO_IMAGES, type DocLang, type ReportBlock, type ReportDesign, type ReportImages, type ReportLanguage, type ReportView, type SnapParameter, type SnapHistorySeries } from './reportsApi'
import './report.css'

/**
 * The A4 laboratory report. It draws only what is in the frozen snapshot: nothing is looked up again, nothing is recalculated,
 * so what was previewed is what prints and what was issued never changes. English, Urdu (RTL) and bilingual variants use the
 * same layout; its own labels are translated by report language, independent of the interface language.
 *
 * The design changes only the look (style, colours, margins, order of the parts, header/footer images). With `onlyTests` a copy
 * shows some of the report's tests and says so on the page; the issued report itself is untouched.
 *
 * `language` draws a copy in another language than the one it was issued in: the snapshot keeps every name in English and
 * Urdu, so only the labels and the choice of names change, never the content.
 *
 * One test per page (owner, 2026-11-12): every test is printed on its own A4 page, and each page carries the full branding
 * (letterhead or header image, patient details, signatures, disclaimer, footer). A page never runs onto a second one: when its
 * content is taller than the page, everything on that page (text, tables, images) is scaled down evenly until it fits, and the
 * footer sits at the bottom. Nothing is cut off. Tests still awaited are listed on the last page.
 */
export function ReportDocument({ view, images = NO_IMAGES, design = CLASSIC, onlyTests, onlyParameters, graphTests, language }: {
  view: ReportView
  /** Language of this copy; undefined = as issued. */
  language?: ReportLanguage
  images?: ReportImages
  design?: ReportDesign
  /** Order item ids to include; undefined = every test. */
  onlyTests?: number[]
  /** Parameter codes by order item id; undefined = every parameter. */
  onlyParameters?: Record<number, string[]>
  /** Codes of the tests that get a historical graph; undefined = the design's list. */
  graphTests?: string[]
}) {
  const { i18n } = useTranslation()
  const { snapshot: s, meta } = view
  const lang = docLang(language ?? s.language)
  const fixed = { en: i18n.getFixedT('en'), ur: i18n.getFixedT('ur') }
  const dateLang = lang === 'ur' ? 'ur' : 'en'
  const graphCodes = new Set(graphTests ?? design.graphTests ?? [])

  /** Label in the report language(s); bilingual shows both, English first. */
  const L = (key: string, opts?: Record<string, unknown>): ReactNode =>
    lang === 'both' ? (
      <>
        <span>{fixed.en(`report.${key}`, opts)}</span>
        <span className="ur" lang="ur" dir="rtl">{fixed.ur(`report.${key}`, opts)}</span>
      </>
    ) : lang === 'ur' ? <span className="ur" lang="ur">{fixed.ur(`report.${key}`, opts)}</span> : <span>{fixed.en(`report.${key}`, opts)}</span>

  const Name = ({ en, ur, className }: { en: string; ur: string | null; className?: string }) =>
    lang === 'en' ? <span className={className}>{en}</span>
      : lang === 'ur' ? <span className={`ur ${className ?? ''}`} lang="ur">{ur ?? en}</span>
        : <span className={className}>{en}{ur && <span className="ur" lang="ur" dir="rtl">{ur}</span>}</span>

  const when = (utc: string | null) => (utc ? formatDateTime(utc, dateLang) : '—')
  const lh = s.letterhead
  const age = (() => {
    const t = fixed[lang === 'ur' ? 'ur' : 'en']
    const p = s.patient
    if (p.ageYears === null) return '—'
    if (p.ageYears >= 5) return t('age.y', { count: p.ageYears })
    if (p.ageYears >= 1) return [t('age.y', { count: p.ageYears }), p.ageMonths ? t('age.m', { count: p.ageMonths }) : ''].filter(Boolean).join(' ')
    if ((p.ageMonths ?? 0) >= 1) return [t('age.m', { count: p.ageMonths ?? 0 }), p.ageDays ? t('age.d', { count: p.ageDays }) : ''].filter(Boolean).join(' ')
    return t('age.d', { count: p.ageDays ?? 0 })
  })()
  const sexWord = lang === 'both' ? `${fixed.en(`sex.${s.patient.sex}`)} / ${fixed.ur(`sex.${s.patient.sex}`)}` : fixed[lang === 'ur' ? 'ur' : 'en'](`sex.${s.patient.sex}`)

  // Reprints carry no "duplicate" watermark (owner's choice); unissued previews and superseded versions keep theirs.
  const marks: string[] = []
  if (view.isPreview) marks.push('preview')
  else if (meta.status === 90) marks.push('superseded')

  const approvers = s.signatories.filter((x) => x.approved)
  const verifiers = s.signatories.filter((x) => !x.approved)

  // A partial copy: only the chosen tests (sections left empty are dropped).
  const totalTests = s.sections.reduce((n, sec) => n + sec.tests.length, 0)
  const chosen = onlyTests ? new Set(onlyTests) : null
  const sections = (chosen
    ? s.sections.map((sec) => ({ ...sec, tests: sec.tests.filter((t) => chosen.has(t.orderItemId)) })).filter((sec) => sec.tests.length > 0)
    : s.sections).map((sec) => ({
      ...sec,
      tests: sec.tests.map((t) => {
        const selectedParameters = onlyParameters?.[t.orderItemId]
        return selectedParameters ? { ...t, parameters: t.parameters.filter((p) => selectedParameters.includes(p.code)) } : t
      }).filter((t) => t.parameters.length > 0),
    })).filter((sec) => sec.tests.length > 0)
  const shownTests = sections.reduce((n, sec) => n + sec.tests.length, 0)
  const partial = chosen !== null && shownTests < totalTests

  // One page per test, each with its department heading. A report with no released test yet still has one page.
  const pages: (typeof sections)[] = sections.flatMap((sec) => sec.tests.map((t) => [{ ...sec, tests: [t] }]))
  if (pages.length === 0) pages.push([])

  // Reports issued before schema 2 have no numbers for the bar, so they keep the five-column table.
  const bars = design.showRangeBars && s.schemaVersion >= 2
  const cols = bars ? 6 : 5
  const disclaimer = lang === 'ur' ? lh.disclaimerUr ?? lh.disclaimerEn : lh.disclaimerEn
  const v = encodeURIComponent(images.version)
  const headerImage = design.showLetterhead && design.useHeaderImage && images.header
  const footerImage = design.showFooterImage && images.footer
  const showLogo = images.logo && design.logoPosition !== 'none' && design.showLetterhead && !headerImage
  const reportId = (
    <div className="rp-id">
      <strong className="num">{meta.reportNo}</strong>
      <span>{L('version', { n: meta.version })}</span>
    </div>
  )

  /** The contents of one page; `refit` fits that page again once one of its images has arrived. */
  const pageContent = (pageSections: typeof sections, index: number, refit: () => void) => {
    const last = index === pages.length - 1
    const logo = showLogo
      ? <img className="rp-logo" src={`/api/report-template/logo?v=${v}`} alt="" onLoad={refit} onError={(e) => { e.currentTarget.style.display = 'none' }} />
      : null

    const blocks: Record<ReportBlock, ReactNode> = {
      patient: (
        <section key="patient" className="rp-patient">
          <dl>
            <div><dt>{L('patient')}</dt><dd><Name en={s.patient.nameEn} ur={s.patient.nameUr} className="strong" /></dd></div>
            <div><dt>{L('patientNo')}</dt><dd className="num">{s.patient.patientNo}</dd></div>
            <div><dt>{L('ageSex')}</dt><dd>{age} · {sexWord}</dd></div>
            {s.patient.mobile && <div><dt>{L('mobile')}</dt><dd className="num" dir="ltr">{s.patient.mobile}</dd></div>}
            {s.order.referringDoctor && <div><dt>{L('referredBy')}</dt><dd dir="auto">{s.order.referringDoctor}</dd></div>}
            <div><dt>{L('orderNo')}</dt><dd className="num">{s.order.orderNo}</dd></div>
            <div><dt>{L('registered')}</dt><dd>{when(s.order.registeredAtUtc)}</dd></div>
            <div><dt>{L('collected')}</dt><dd>{when(s.order.collectedAtUtc)}</dd></div>
            <div><dt>{L('received')}</dt><dd>{when(s.order.receivedAtUtc)}</dd></div>
            <div><dt>{L('reported')}</dt><dd>{when(s.issuedAtUtc)}</dd></div>
            <div><dt>{L('branch')}</dt><dd><Name en={s.order.branchNameEn} ur={s.order.branchNameUr} /></dd></div>
          </dl>
        </section>
      ),
      results: (
        <Fragment key="results">
          {pageSections.map((sec) => (
            <section key={sec.departmentNameEn} className="rp-section">
              <h3 className="rp-dept"><Name en={sec.departmentNameEn} ur={sec.departmentNameUr} /></h3>
              {sec.tests.map((t) => (
                <div key={t.orderItemId} className="rp-test">
                  <h4><Name en={t.nameEn} ur={t.nameUr} /> {t.resultVersion > 1 && <span className="rp-ver">{L('resultVersion', { n: t.resultVersion })}</span>}</h4>
                  {design.showSingleParameterCharts && t.parameters.length === 1 && t.parameters[0]?.resultType === 1 && (
                    <SingleParameterChart p={t.parameters[0]} lang={lang} fixed={fixed} L={L} Name={Name} />
                  )}
                  <table className="rp-table">
                    <thead>
                      <tr>
                        <th>{L('parameter')}</th><th className="res">{L('result')}</th><th className="flag">{L('flag')}</th><th>{L('unit')}</th><th>{L('referenceRange')}</th>
                        {bars && <th className="bar">{L('chart')}</th>}
                      </tr>
                    </thead>
                    <tbody>
                      {t.parameters.map((p, i) => (
                        <Fragment key={p.code}>
                          {p.groupName && p.groupName !== t.parameters[i - 1]?.groupName && <tr className="grp"><td colSpan={cols}>{p.groupName}</td></tr>}
                          <ParamRow p={p} lang={lang} fixed={fixed} Name={Name} bars={bars} flagWords={design.showFlagWords} />
                        </Fragment>
                      ))}
                    </tbody>
                  </table>
                  {t.comment && <p className="rp-comment" dir="auto"><strong>{L('comment')}:</strong> {t.comment}</p>}
                  {t.reportNotes && <p className="rp-notes" dir="auto">{t.reportNotes}</p>}
                  {graphCodes.has(t.code) && (s.history ?? []).filter((h) => h.testCode === t.code && h.points.length > 1 && t.parameters.some((p) => p.code === h.parameterCode)).map((h) => (
                    <HistoryChart key={`${h.testCode}-${h.parameterCode}`} series={h} Name={Name} t={fixed[lang === 'ur' ? 'ur' : 'en']} />
                  ))}
                </div>
              ))}
            </section>
          ))}
        </Fragment>
      ),
      // Tests still awaited are listed once, on the last page. A partial copy is about the chosen tests only, so it does not list them.
      pending: last && !partial && s.pending.length > 0 ? (
        <section key="pending" className="rp-section rp-pending">
          <h3 className="rp-dept">{L('pending')}</h3>
          <ul>{s.pending.map((p) => <li key={p.code}><Name en={p.nameEn} ur={p.nameUr} /> <span className="num">({p.code})</span> — {L('awaited')}</li>)}</ul>
        </section>
      ) : null,
      signatures: (
        <section key="signatures" className="rp-signs">
          {[...approvers.map((x) => ({ x, role: 'approvedBy' })), ...verifiers.map((x) => ({ x, role: 'verifiedBy' }))].slice(0, 4).map(({ x, role }) => (
            <div key={x.userId} className="rp-sign">
              {x.hasSignature ? <img src={`/api/reports/signatures/${x.userId}`} alt="" onLoad={refit} onError={(e) => { e.currentTarget.style.visibility = 'hidden' }} /> : <span className="rp-sign-space" />}
              <strong><Name en={x.nameEn} ur={x.nameUr} /></strong>
              {x.designation && <span dir="auto">{x.designation}</span>}
              {x.registrationNo && <span className="num" dir="ltr">{L('regNo')} {x.registrationNo}</span>}
              <em>{L(role)}</em>
            </div>
          ))}
          {/* Signatories from the report template (frozen at issue): their own designation only, never "approved by". */}
          {(s.fixedSignatories ?? []).map((x) => (
            <div key={`fixed-${x.id}`} className="rp-sign rp-sign-fixed">
              {x.hasSignature ? <img src={`/api/report-template/signatories/${x.id}/signature`} alt="" onLoad={refit} onError={(e) => { e.currentTarget.style.visibility = 'hidden' }} /> : <span className="rp-sign-space" />}
              <strong><Name en={x.nameEn} ur={x.nameUr} /></strong>
              {x.qualification && <span dir="auto">{x.qualification}</span>}
              {x.designation && <span dir="auto">{x.designation}</span>}
              {x.registrationNo && <span className="num" dir="ltr">{L('regNo')} {x.registrationNo}</span>}
            </div>
          ))}
        </section>
      ),
      disclaimer: disclaimer ? <p key="disclaimer" className="rp-disclaimer" dir="auto">{disclaimer}</p> : null,
    }

    return (
      <>
        {headerImage ? (
          <header className="rp-head-image">
            <img src={`/api/report-template/images/header?v=${v}`} alt="" onLoad={refit} onError={(e) => { e.currentTarget.style.display = 'none' }} />
            {reportId}
          </header>
        ) : design.showLetterhead ? (
          <header className="rp-head">
            {design.logoPosition !== 'end' && logo}
            <div className="rp-org">
              <h1><Name en={lh.organizationNameEn} ur={lh.organizationNameUr} /></h1>
              <p className="rp-lab"><Name en={lh.labNameEn} ur={lh.labNameUr} /></p>
              <p className="rp-contact">
                {(lang === 'ur' ? lh.addressUr ?? lh.addressEn : lh.addressEn) && <span dir="auto">{lang === 'ur' ? lh.addressUr ?? lh.addressEn : lh.addressEn}</span>}
                {lh.phone && <span dir="ltr" className="num">{lh.phone}</span>}
                {lh.email && <span dir="ltr">{lh.email}</span>}
              </p>
              {(lang === 'ur' ? lh.headerNoteUr ?? lh.headerNoteEn : lh.headerNoteEn) && <p className="rp-note" dir="auto">{lang === 'ur' ? lh.headerNoteUr ?? lh.headerNoteEn : lh.headerNoteEn}</p>}
            </div>
            {design.logoPosition === 'end' && logo}
            {reportId}
          </header>
        ) : (
          // Pre-printed letterhead paper: only the report number, the top margin leaves room for the printed header.
          <header className="rp-head rp-head-bare">{reportId}</header>
        )}

        <main className="rp-body">
          <h2 className="rp-title">{L('title')}</h2>
          {s.status === 0 && <p className="rp-banner rp-interim">{L('interim')}</p>}
          {s.version > 1 && <p className="rp-banner rp-amended">{L('amended', { n: s.version })}{s.amendmentReason && <span dir="auto"> — {s.amendmentReason}</span>}</p>}
          {meta.status === 90 && <p className="rp-banner rp-superseded">{L('supersededBy', { n: meta.supersededByVersion ?? '' })}</p>}
          {partial && <p className="rp-banner rp-partial">{L('partialPrint', { shown: shownTests, total: totalTests })}</p>}

          {design.blocks.map((b) => blocks[b])}

          {last && <p className="rp-end">{L('endOfReport')}</p>}
        </main>

        <footer className="rp-foot">
          <p className="rp-generated">{L('systemGenerated')}</p>
          {design.showLetterhead && !footerImage && (lang === 'ur' ? lh.footerNoteUr ?? lh.footerNoteEn : lh.footerNoteEn) && <p dir="auto">{lang === 'ur' ? lh.footerNoteUr ?? lh.footerNoteEn : lh.footerNoteEn}</p>}
          <p className="rp-fp">
            <span className="num">{meta.reportNo}</span> · {L('version', { n: meta.version })}
            {pages.length > 1 && <span className="rp-page-no">{L('pageOf', { n: index + 1, total: pages.length })}</span>}
          </p>
          {footerImage && <img className="rp-foot-image" src={`/api/report-template/images/footer?v=${v}`} alt="" onLoad={refit} onError={(e) => { e.currentTarget.style.display = 'none' }} />}
        </footer>
      </>
    )
  }

  const markText = (m: string) => (lang === 'ur' ? fixed.ur(`report.mark.${m}`) : fixed.en(`report.mark.${m}`))

  return (
    <div className="report-pages">
      {pages.map((pageSections, index) => (
        <FittedPage
          key={pageSections[0]?.tests[0]?.orderItemId ?? 'only'}
          className={`report report-${lang} ${designClasses(design)}`}
          style={designVars(design)}
          dir={lang === 'ur' ? 'rtl' : 'ltr'}
          lang={lang === 'ur' ? 'ur' : 'en'}
          marks={marks.map((m) => <div key={m} className={`report-mark report-mark-${m}`} aria-hidden>{markText(m)}</div>)}
        >
          {(refit) => pageContent(pageSections, index, refit)}
        </FittedPage>
      ))}
    </div>
  )
}

/** One A4 sheet whose content is scaled to fit it (see useFitToPage). */
function FittedPage({ className, style, dir, lang, marks, children }: {
  className: string
  style: CSSProperties
  dir: 'ltr' | 'rtl'
  lang: string
  marks: ReactNode
  children: (refit: () => void) => ReactNode
}) {
  const fit = useFitToPage()
  return (
    <article className={className} style={style} dir={dir} lang={lang}>
      {marks}
      <div className="rp-sheet" ref={fit.sheet}>
        <div className="rp-fit" ref={fit.box}>
          <div className="rp-page" ref={fit.page}>
            {children(fit.refit)}
          </div>
        </div>
      </div>
    </article>
  )
}

/**
 * Scales the page content (with CSS zoom, which also shrinks the layout box, so print sees one page) until it fits the sheet.
 * The content is laid out wider by the same factor first, so a scaled report still uses the full page width.
 */
function useFitToPage() {
  const sheet = useRef<HTMLDivElement>(null)
  const box = useRef<HTMLDivElement>(null)
  const page = useRef<HTMLDivElement>(null)

  const refit = useCallback(() => {
    const sh = sheet.current, bx = box.current, pg = page.current
    if (!sh || !bx || !pg) return
    const width = sh.clientWidth
    const avail = sh.clientHeight
    if (width <= 0 || avail <= 0) return
    const heightAt = (scale: number) => {
      bx.style.zoom = '1'
      bx.style.width = `${width / scale}px`
      pg.style.minHeight = '0px'
      return pg.offsetHeight
    }
    let scale = 1
    const natural = heightAt(1)
    if (natural > avail) {
      // At avail/natural the wider layout is never taller, so it fits; search upwards for the largest scale that still fits.
      let lo = avail / natural
      let hi = 1
      for (let i = 0; i < 8; i++) {
        const mid = (lo + hi) / 2
        if (heightAt(mid) * mid <= avail) lo = mid
        else hi = mid
      }
      scale = Math.floor(lo * 1000) / 1000
    }
    bx.style.width = `${width / scale}px`
    bx.style.zoom = String(scale)
    pg.style.minHeight = `${Math.floor(avail / scale)}px`
  }, [])

  // Every render may change the content; fonts (Urdu) and images change it again once they arrive.
  useLayoutEffect(refit)
  useEffect(() => {
    const fonts = document.fonts
    fonts?.addEventListener?.('loadingdone', refit)
    void fonts?.ready?.then(refit)
    window.addEventListener('beforeprint', refit)
    return () => {
      fonts?.removeEventListener?.('loadingdone', refit)
      window.removeEventListener('beforeprint', refit)
    }
  }, [refit])

  return { sheet, box, page, refit }
}

function ParamRow({ p, lang, fixed, Name, bars, flagWords }: {
  p: SnapParameter
  lang: DocLang
  fixed: { en: (k: string) => string; ur: (k: string) => string }
  Name: (props: { en: string; ur: string | null; className?: string }) => ReactNode
  bars: boolean
  flagWords: boolean
}) {
  const abnormal = p.flag >= 2
  const critical = p.flag === 4 || p.flag === 5
  const direction = p.flag === 2 || p.flag === 4 ? 'lo' : p.flag === 3 || p.flag === 5 ? 'hi' : p.flag === 6 ? 'hi' : ''
  const word = (l: 'en' | 'ur') => (p.flag >= 2 ? fixed[l](`report.flagWord.${p.flag}`) : '')
  const value = lang === 'ur' && p.displayUr ? p.displayUr : p.display
  const rowClass = [critical ? 'crit' : abnormal ? 'abn' : '', direction ? `dir-${direction}` : ''].filter(Boolean).join(' ') || undefined
  return (
    <tr className={rowClass}>
      <td><Name en={p.nameEn} ur={p.nameUr} /></td>
      <td className="res"><span className={`val${abnormal ? ' bold' : ''}`} dir={p.resultType === 1 ? 'ltr' : 'auto'}>{value}</span>{lang === 'both' && p.displayUr && p.resultType !== 1 && p.displayUr !== p.display && <span className="ur" lang="ur" dir="rtl"> / {p.displayUr}</span>}</td>
      <td className="flag">{p.flagCode && <span className="fl"><b>{p.flagCode}</b>{flagWords && <small>{lang === 'ur' ? word('ur') : lang === 'both' ? `${word('en')} / ${word('ur')}` : word('en')}</small>}</span>}</td>
      <td className="num" dir="ltr">{p.unit ?? ''}</td>
      <td className="num" dir="ltr">{p.referenceText ?? ''}</td>
      {bars && <td className="bar">{p.resultType === 1 && <RangeBar p={p} />}</td>}
    </tr>
  )
}

/** Low / normal / high zones with the patient's value marked. Drawn from the released number and limits in the snapshot. */
function RangeBar({ p }: { p: SnapParameter }) {
  const g = rangeBar(p.value, p.low, p.high)
  if (!g) return null
  const tone = p.flag === 2 || p.flag === 4 ? 'lo' : p.flag === 3 || p.flag === 5 ? 'hi' : 'ok'
  return (
    // SVG coordinates do not follow the text direction, so the bar runs low to high left to right on Urdu reports too, like the numbers.
    <svg className={`rp-bar tone-${tone}`} viewBox="0 0 100 10" preserveAspectRatio="none" aria-hidden>
      {g.lowEnd > 0 && <rect className="z-lo" x="0" y="3" width={g.lowEnd} height="4" rx="1" />}
      <rect className="z-ok" x={g.lowEnd} y="3" width={Math.max(0, g.highStart - g.lowEnd)} height="4" />
      {g.highStart < 100 && <rect className="z-hi" x={g.highStart} y="3" width={100 - g.highStart} height="4" rx="1" />}
      <rect className="mk" x={g.marker - 1.2} y="0.5" width="2.4" height="9" rx="1" />
    </svg>
  )
}

/** A smooth curve through the points that never overshoots them (monotone cubic), so a curve cannot suggest a value that was not measured. */
function monotonePath(p: [number, number][]): string {
  const n = p.length
  if (n === 0) return ''
  if (n === 1) return `M ${p[0]![0]} ${p[0]![1]}`
  const dx: number[] = []
  const m: number[] = []
  for (let i = 0; i < n - 1; i++) { dx[i] = p[i + 1]![0] - p[i]![0]; m[i] = (p[i + 1]![1] - p[i]![1]) / dx[i]! }
  const t: number[] = new Array(n).fill(0)
  t[0] = m[0]!; t[n - 1] = m[n - 2]!
  for (let i = 1; i < n - 1; i++) t[i] = m[i - 1]! * m[i]! <= 0 ? 0 : (m[i - 1]! + m[i]!) / 2
  for (let i = 0; i < n - 1; i++) {
    if (m[i] === 0) { t[i] = 0; t[i + 1] = 0; continue }
    const a = t[i]! / m[i]!
    const b = t[i + 1]! / m[i]!
    const sq = a * a + b * b
    if (sq > 9) { const k = 3 / Math.sqrt(sq); t[i] = k * a * m[i]!; t[i + 1] = k * b * m[i]! }
  }
  let d = `M ${p[0]![0]} ${p[0]![1]}`
  for (let i = 0; i < n - 1; i++) {
    const h = dx[i]! / 3
    d += ` C ${p[i]![0] + h} ${p[i]![1] + t[i]! * h} ${p[i + 1]![0] - h} ${p[i + 1]![1] - t[i + 1]! * h} ${p[i + 1]![0]} ${p[i + 1]![1]}`
  }
  return d
}

const toneOf = (flag: number) => (flag === 4 || flag === 5 ? 'crit' : flag === 2 || flag === 3 || flag === 6 ? 'warn' : 'ok')
const fmtNum = (v: number, step: number) => v.toFixed(Math.min(3, Math.max(0, -Math.floor(Math.log10(step) + 1e-9))))

/**
 * Historical graph of one parameter: the patient's approved results over time on a smooth, shaded curve, with the latest
 * reference range as a green band, value labels, abnormal results coloured, and the change since the previous result. Drawn only
 * from the snapshot's frozen history (the last 12 results); evenly spaced in order, dated on the axis.
 */
function HistoryChart({ series, Name, t }: {
  series: SnapHistorySeries
  Name: (props: { en: string; ur: string | null; className?: string }) => ReactNode
  t: (key: string, opts?: Record<string, unknown>) => string
}) {
  const points = series.points.slice(-12)
  const n = points.length
  const W = 760, H = 250, L = 52, R = 28, T = 44, B = 46
  const last = points[n - 1]!
  const prev = points[n - 2]
  const lo = last.low, hi = last.high
  const nums = [...points.map((p) => p.value), ...(lo !== null ? [lo] : []), ...(hi !== null ? [hi] : [])]
  let rawMin = Math.min(...nums), rawMax = Math.max(...nums)
  if (rawMin === rawMax) { rawMin -= Math.max(Math.abs(rawMin) * 0.1, 1); rawMax += Math.max(Math.abs(rawMax) * 0.1, 1) }
  const pad = (rawMax - rawMin) * 0.12
  const step = niceStep((rawMax - rawMin + pad * 2) / 4)
  let min = Math.floor((rawMin - pad) / step) * step
  if (min < 0 && rawMin >= 0) min = 0
  const max = Math.ceil((rawMax + pad) / step) * step
  const ticks: number[] = []
  for (let v = min; v <= max + step * 1e-6; v += step) ticks.push(v)
  const plotH = H - T - B
  const x = (i: number) => L + (n === 1 ? (W - L - R) / 2 : (i * (W - L - R)) / (n - 1))
  const y = (v: number) => T + ((max - v) * plotH) / (max - min)
  const coords = points.map((p, i) => [x(i), y(p.value)] as [number, number])
  const line = monotonePath(coords)
  const baseY = y(min)
  const area = `${line} L ${x(n - 1)} ${baseY} L ${x(0)} ${baseY} Z`
  const bandTop = hi !== null ? y(Math.min(hi, max)) : T
  const bandBottom = lo !== null ? y(Math.max(lo, min)) : baseY
  const hasBand = (lo !== null || hi !== null) && bandBottom > bandTop
  const gid = `hg-${series.testCode}-${series.parameterCode}`.replace(/[^A-Za-z0-9_-]/g, '')
  const labelAll = n <= 8
  const values = points.map((p) => p.value)
  const extremes = new Set([0, n - 1, values.indexOf(Math.max(...values)), values.indexOf(Math.min(...values))])
  const diff = prev ? last.value - prev.value : 0
  const pct = prev && prev.value !== 0 ? (diff / Math.abs(prev.value)) * 100 : null
  const dir = Math.abs(diff) < step * 1e-3 ? 'same' : diff > 0 ? 'up' : 'down'
  const tone = toneOf(last.flag)
  const date = (iso: string) => new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: '2-digit' })
  const unit = series.unit ? ` ${series.unit}` : ''
  const every = n > 9 ? 2 : 1
  const rangeText = lo !== null && hi !== null ? `${fmtNum(lo, step)} – ${fmtNum(hi, step)}` : hi !== null ? `< ${fmtNum(hi, step)}` : lo !== null ? `> ${fmtNum(lo, step)}` : ''

  return (
    <figure className={`rp-history tone-${tone}`} aria-label={`${series.nameEn}: ${t('report.history')}`}>
      <figcaption>
        <div className="rp-history-title">
          <strong><Name en={series.nameEn} ur={series.nameUr} /></strong>
          <small>{t('report.history')}{series.unit ? ` · ${series.unit}` : ''} · {t('report.resultsCount', { count: n })}</small>
        </div>
        {prev && (
          <div className="rp-history-delta" dir="ltr">
            <span className="rp-delta-arrow">{dir === 'up' ? '▲' : dir === 'down' ? '▼' : '■'}</span>
            <span className="num">{dir === 'same' ? t('report.noChange') : `${diff > 0 ? '+' : '−'}${fmtNum(Math.abs(diff), step / 10)}${unit}${pct !== null ? ` (${Math.abs(pct).toFixed(1)}%)` : ''}`}</span>
            <small>{t('report.vsPrevious')}</small>
          </div>
        )}
      </figcaption>
      <svg viewBox={`0 0 ${W} ${H}`} role="img" aria-label={series.nameEn} direction="ltr">
        <defs>
          <linearGradient id={gid} x1="0" x2="0" y1="0" y2="1">
            <stop offset="0" className="h-grad-top" />
            <stop offset="1" className="h-grad-bottom" />
          </linearGradient>
        </defs>
        {hasBand && (
          <g>
            <rect className="h-band" x={L} y={bandTop} width={W - L - R} height={bandBottom - bandTop} />
            {hi !== null && hi <= max && <line className="h-band-edge" x1={L} x2={W - R} y1={bandTop} y2={bandTop} />}
            {lo !== null && lo >= min && <line className="h-band-edge" x1={L} x2={W - R} y1={bandBottom} y2={bandBottom} />}
            {bandBottom - bandTop > 20 && <text className="h-band-label" x={W - R - 6} y={bandTop + 13} textAnchor="end">{t('report.normal')} · {rangeText}</text>}
          </g>
        )}
        {ticks.map((v) => (
          <g key={v}>
            <line className="h-grid" x1={L} x2={W - R} y1={y(v)} y2={y(v)} />
            <text className="h-tick" x={L - 9} y={y(v) + 3.5} textAnchor="end">{fmtNum(v, step)}</text>
          </g>
        ))}
        <path className="h-area" d={area} fill={`url(#${gid})`} />
        <path className="h-line" d={line} />
        {points.map((pt, i) => {
          const isLast = i === n - 1
          const text = fmtNum(pt.value, step / 10)
          const showLabel = labelAll || extremes.has(i)
          const w = Math.max(38, text.length * 7.6 + 16)
          return (
            <g key={`${pt.dateUtc}-${i}`} className={`h-pt pt-${toneOf(pt.flag)}`}>
              {isLast && <circle className="h-halo" cx={x(i)} cy={y(pt.value)} r={13} />}
              <circle className="h-dot" cx={x(i)} cy={y(pt.value)} r={isLast ? 7 : 5}>
                <title>{`${text}${unit} · ${date(pt.dateUtc)}`}</title>
              </circle>
              {showLabel && (isLast
                ? (
                  <g className="h-last">
                    <rect x={x(i) - w / 2} y={y(pt.value) - 36} width={w} height={21} rx={6} />
                    <path d={`M ${x(i) - 5} ${y(pt.value) - 16} L ${x(i)} ${y(pt.value) - 10} L ${x(i) + 5} ${y(pt.value) - 16} Z`} />
                    <text x={x(i)} y={y(pt.value) - 21.5} textAnchor="middle">{text}</text>
                  </g>
                )
                : <text className="h-val" x={x(i)} y={y(pt.value) - 12} textAnchor="middle">{text}</text>)}
              {((n - 1 - i) % every === 0) && (
                <text className={`h-date${isLast ? ' is-last' : ''}`} x={x(i)} y={H - B + 22} textAnchor="middle">{date(pt.dateUtc)}</text>
              )}
            </g>
          )
        })}
        <line className="h-axis" x1={L} x2={W - R} y1={baseY} y2={baseY} />
      </svg>
    </figure>
  )
}

/**
 * The large graph for a test with one numeric parameter: the patient's value on a scale with its low, normal and high zones,
 * round-numbered axis, the approved limits and a callout with the value. Drawn only from the snapshot (value and limits).
 */
function SingleParameterChart({ p, lang, fixed, L, Name }: {
  p: SnapParameter
  lang: DocLang
  fixed: { en: (k: string) => string; ur: (k: string) => string }
  L: (key: string) => ReactNode
  Name: (props: { en: string; ur: string | null; className?: string }) => ReactNode
}) {
  const sc = gaugeScale(p.value, p.low, p.high)
  if (!sc || p.value === null || p.value === undefined) return null
  const tone = p.flag === 2 || p.flag === 4 ? 'lo' : p.flag === 3 || p.flag === 5 ? 'hi' : 'ok'
  const critical = p.flag === 4 || p.flag === 5
  const value = lang === 'ur' && p.displayUr ? p.displayUr : p.display
  const zoneWord = (k: string) => (lang === 'ur' ? fixed.ur(`report.${k}`) : fixed.en(`report.${k}`))
  const status = p.flag >= 2 ? `flagWord.${p.flag}` : 'normal'

  // SVG units: 1000 wide. The scale runs left to right (low to high) on Urdu reports too, like the numbers.
  const W = 1000, X0 = 14, X1 = 986
  const x = (v: number) => X0 + (sc.pos(v) / 100) * (X1 - X0)
  const lo = p.low ?? null, hi = p.high ?? null
  const xLo = lo === null ? X0 : x(lo)
  const xHi = hi === null ? X1 : x(hi)
  const xv = x(p.value)
  const BAND_Y = 66, BAND_H = 44, AXIS_Y = 136
  const bubble = `${p.display}${p.unit ? ` ${p.unit}` : ''}`
  const bw = Math.max(96, bubble.length * 14.5 + 36)
  const bx = Math.min(W - bw, Math.max(0, xv - bw / 2))
  const limits = [lo, hi].filter((v): v is number => v !== null)
  // Axis numbers too close to a limit would overlap its (bold) label.
  const ticks = sc.ticks.filter((t) => limits.every((l) => Math.abs(x(t.at) - x(l)) > 48))
  const zones = [
    { k: 'lo', from: X0, to: xLo, word: zoneWord('flagWord.2') },
    { k: 'ok', from: xLo, to: xHi, word: zoneWord('normal') },
    { k: 'hi', from: xHi, to: X1, word: zoneWord('flagWord.3') },
  ].filter((z) => z.to - z.from > 0.5)
  const clipId = `rpg-${p.code.replace(/[^A-Za-z0-9_-]/g, '')}`

  return (
    <figure className={`rp-gauge tone-${tone}${critical ? ' crit' : ''}`} aria-label={`${p.nameEn}: ${p.display}${p.unit ? ` ${p.unit}` : ''}`}>
      <div className="rp-gauge-head">
        <div className="rp-gauge-name">
          <Name en={p.nameEn} ur={p.nameUr} />
          {p.referenceText && <small>{L('referenceRange')}: <span className="num" dir="ltr">{p.referenceText}{p.unit ? ` ${p.unit}` : ''}</span></small>}
        </div>
        <div className="rp-gauge-result">
          <span className="rp-gauge-num" dir="ltr">
            <span className="rp-gauge-value num" dir={p.resultType === 1 ? 'ltr' : 'auto'}>{value}</span>
            {p.unit && <span className="rp-gauge-unit">{p.unit}</span>}
          </span>
          <span className="rp-gauge-pill">{L(status)}</span>
        </div>
      </div>
      <svg className="rp-gauge-plot" viewBox={`0 0 ${W} 170`} direction="ltr" aria-hidden>
        <defs>
          <clipPath id={clipId}><rect x={X0} y={BAND_Y} width={X1 - X0} height={BAND_H} rx={BAND_H / 2} /></clipPath>
        </defs>
        {/* Grid lines from the band down to the axis. */}
        {ticks.map((t) => <line key={`g${t.at}`} className="g-grid" x1={x(t.at)} x2={x(t.at)} y1={BAND_Y + BAND_H + 2} y2={AXIS_Y} />)}
        <g clipPath={`url(#${clipId})`}>
          {zones.map((z) => <rect key={z.k} className={`z-${z.k}`} x={z.from} y={BAND_Y} width={z.to - z.from} height={BAND_H} />)}
          <rect className="g-gloss" x={X0} y={BAND_Y} width={X1 - X0} height={BAND_H / 2} />
          {lo !== null && <line className="g-edge" x1={xLo} x2={xLo} y1={BAND_Y} y2={BAND_Y + BAND_H} />}
          {hi !== null && <line className="g-edge" x1={xHi} x2={xHi} y1={BAND_Y} y2={BAND_Y + BAND_H} />}
        </g>
        <rect className="g-outline" x={X0} y={BAND_Y} width={X1 - X0} height={BAND_H} rx={BAND_H / 2} />
        {zones.filter((z) => z.to - z.from > 110).map((z) => {
          // Keep the zone's name clear of the value's dot: move it to the wider side of the zone.
          let cx = (z.from + z.to) / 2
          if (Math.abs(cx - xv) < 75) cx = xv - z.from > z.to - xv ? (z.from + xv) / 2 : (xv + z.to) / 2
          if (Math.abs(cx - xv) < 55) return null
          return <text key={`t${z.k}`} className={`g-zone g-zone-${z.k}${lang === 'ur' ? ' ur' : ''}`} x={cx} y={BAND_Y + BAND_H / 2 + 6} textAnchor="middle">{z.word}</text>
        })}

        {/* Axis */}
        <line className="g-axis" x1={X0} x2={X1} y1={AXIS_Y} y2={AXIS_Y} />
        {ticks.map((t) => (
          <g key={`t${t.at}`}>
            <line className="g-tick" x1={x(t.at)} x2={x(t.at)} y1={AXIS_Y} y2={AXIS_Y + 5} />
            <text className="g-tick-label" x={x(t.at)} y={AXIS_Y + 24} textAnchor="middle">{t.label}</text>
          </g>
        ))}
        {limits.map((l) => (
          <g key={`l${l}`}>
            <line className="g-limit" x1={x(l)} x2={x(l)} y1={BAND_Y + BAND_H} y2={AXIS_Y + 6} />
            <text className="g-limit-label" x={x(l)} y={AXIS_Y + 26} textAnchor="middle">{l}</text>
          </g>
        ))}

        {/* The patient's value: callout, pointer and a dot on the band. */}
        <g className="g-marker">
          <rect className="g-bubble" x={bx} y={4} width={bw} height={40} rx={8} />
          <path className="g-bubble" d={`M ${xv - 10} 43 L ${xv} 55 L ${xv + 10} 43 Z`} />
          <text className="g-bubble-text" x={bx + bw / 2} y={31} textAnchor="middle">{sc.clipped ? (p.value > sc.max ? `${bubble} ▸` : `◂ ${bubble}`) : bubble}</text>
          <line className="g-needle" x1={xv} x2={xv} y1={55} y2={BAND_Y + BAND_H + 6} />
          <circle className="g-dot" cx={xv} cy={BAND_Y + BAND_H / 2} r={12} />
        </g>
      </svg>
    </figure>
  )
}
