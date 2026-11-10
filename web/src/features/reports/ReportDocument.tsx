import { Fragment, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDateTime } from '../../lib/format'
import { docLang, type DocLang, type ReportView, type SnapParameter } from './reportsApi'
import './report.css'

/**
 * The A4 laboratory report. It draws only what is in the frozen snapshot: nothing is looked up again, nothing is recalculated,
 * so what was previewed is what prints and what was issued never changes. English, Urdu (RTL) and bilingual variants use the
 * same layout; its own labels are translated by report language, independent of the interface language.
 */
export function ReportDocument({ view, hasLogo }: { view: ReportView; hasLogo: boolean }) {
  const { i18n } = useTranslation()
  const { snapshot: s, meta } = view
  const lang = docLang(s.language)
  const fixed = { en: i18n.getFixedT('en'), ur: i18n.getFixedT('ur') }
  const dateLang = lang === 'ur' ? 'ur' : 'en'

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

  const marks: string[] = []
  if (view.isPreview) marks.push('preview')
  else if (meta.status === 90) marks.push('superseded')
  else if (meta.isDuplicate) marks.push('duplicate')

  const approvers = s.signatories.filter((x) => x.approved)
  const verifiers = s.signatories.filter((x) => !x.approved)

  return (
    <article className={`report report-${lang}`} dir={lang === 'ur' ? 'rtl' : 'ltr'} lang={lang === 'ur' ? 'ur' : 'en'}>
      {marks.map((m) => <div key={m} className={`report-mark report-mark-${m}`} aria-hidden>{lang === 'ur' ? fixed.ur(`report.mark.${m}`) : fixed.en(`report.mark.${m}`)}</div>)}
      <table className="report-frame">
        <thead>
          <tr><td>
            <header className="rp-head">
              {hasLogo && <img className="rp-logo" src="/api/report-template/logo" alt="" onError={(e) => { e.currentTarget.style.display = 'none' }} />}
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
              <div className="rp-id">
                <strong className="num">{meta.reportNo}</strong>
                <span>{L('version', { n: meta.version })}</span>
              </div>
            </header>
          </td></tr>
        </thead>

        <tfoot>
          <tr><td>
            <footer className="rp-foot">
              {(lang === 'ur' ? lh.footerNoteUr ?? lh.footerNoteEn : lh.footerNoteEn) && <p dir="auto">{lang === 'ur' ? lh.footerNoteUr ?? lh.footerNoteEn : lh.footerNoteEn}</p>}
              <p className="rp-fp">
                <span className="num">{meta.reportNo}</span> · {L('version', { n: meta.version })} · <span>{L('fingerprint')}:</span> <span className="num mono">{meta.fingerprint}</span>
                <span className="rp-page" />
              </p>
            </footer>
          </td></tr>
        </tfoot>

        <tbody>
          <tr><td>
            <h2 className="rp-title">{L('title')}</h2>
            {s.status === 0 && <p className="rp-banner rp-interim">{L('interim')}</p>}
            {s.version > 1 && <p className="rp-banner rp-amended">{L('amended', { n: s.version })}{s.amendmentReason && <span dir="auto"> — {s.amendmentReason}</span>}</p>}
            {meta.status === 90 && <p className="rp-banner rp-superseded">{L('supersededBy', { n: meta.supersededByVersion ?? '' })}</p>}

            <section className="rp-patient">
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

            {s.sections.map((sec) => (
              <section key={sec.departmentNameEn} className="rp-section">
                <h3 className="rp-dept"><Name en={sec.departmentNameEn} ur={sec.departmentNameUr} /></h3>
                {sec.tests.map((t) => (
                  <div key={t.orderItemId} className="rp-test">
                    <h4><Name en={t.nameEn} ur={t.nameUr} /> {t.resultVersion > 1 && <span className="rp-ver">{L('resultVersion', { n: t.resultVersion })}</span>}</h4>
                    <table className="rp-table">
                      <thead>
                        <tr><th>{L('parameter')}</th><th className="res">{L('result')}</th><th className="flag">{L('flag')}</th><th>{L('unit')}</th><th>{L('referenceRange')}</th></tr>
                      </thead>
                      <tbody>
                        {t.parameters.map((p, i) => (
                          <Fragment key={p.code}>
                            {p.groupName && p.groupName !== t.parameters[i - 1]?.groupName && <tr className="grp"><td colSpan={5}>{p.groupName}</td></tr>}
                            <ParamRow p={p} lang={lang} fixed={fixed} Name={Name} />
                          </Fragment>
                        ))}
                      </tbody>
                    </table>
                    {t.comment && <p className="rp-comment" dir="auto"><strong>{L('comment')}:</strong> {t.comment}</p>}
                    {t.reportNotes && <p className="rp-notes" dir="auto">{t.reportNotes}</p>}
                  </div>
                ))}
              </section>
            ))}

            {s.pending.length > 0 && (
              <section className="rp-section rp-pending">
                <h3 className="rp-dept">{L('pending')}</h3>
                <ul>{s.pending.map((p) => <li key={p.code}><Name en={p.nameEn} ur={p.nameUr} /> <span className="num">({p.code})</span> — {L('awaited')}</li>)}</ul>
              </section>
            )}

            <section className="rp-signs">
              {[...approvers.map((x) => ({ x, role: 'approvedBy' })), ...verifiers.map((x) => ({ x, role: 'verifiedBy' }))].slice(0, 4).map(({ x, role }) => (
                <div key={x.userId} className="rp-sign">
                  {x.hasSignature ? <img src={`/api/reports/signatures/${x.userId}`} alt="" onError={(e) => { e.currentTarget.style.visibility = 'hidden' }} /> : <span className="rp-sign-space" />}
                  <strong><Name en={x.nameEn} ur={x.nameUr} /></strong>
                  {x.designation && <span dir="auto">{x.designation}</span>}
                  {x.registrationNo && <span className="num" dir="ltr">{L('regNo')} {x.registrationNo}</span>}
                  <em>{L(role)}</em>
                </div>
              ))}
            </section>

            {(lang === 'ur' ? lh.disclaimerUr ?? lh.disclaimerEn : lh.disclaimerEn) && <p className="rp-disclaimer" dir="auto">{lang === 'ur' ? lh.disclaimerUr ?? lh.disclaimerEn : lh.disclaimerEn}</p>}
            <p className="rp-end">{L('endOfReport')}</p>
          </td></tr>
        </tbody>
      </table>
    </article>
  )
}

function ParamRow({ p, lang, fixed, Name }: {
  p: SnapParameter
  lang: DocLang
  fixed: { en: (k: string) => string; ur: (k: string) => string }
  Name: (props: { en: string; ur: string | null; className?: string }) => ReactNode
}) {
  const abnormal = p.flag >= 2
  const critical = p.flag === 4 || p.flag === 5
  const word = (l: 'en' | 'ur') => (p.flag >= 2 ? fixed[l](`report.flagWord.${p.flag}`) : '')
  const value = lang === 'ur' && p.displayUr ? p.displayUr : p.display
  return (
    <tr className={critical ? 'crit' : abnormal ? 'abn' : undefined}>
      <td><Name en={p.nameEn} ur={p.nameUr} /></td>
      <td className="res"><span className={`val${abnormal ? ' bold' : ''}`} dir={p.resultType === 1 ? 'ltr' : 'auto'}>{value}</span>{lang === 'both' && p.displayUr && p.resultType !== 1 && p.displayUr !== p.display && <span className="ur" lang="ur" dir="rtl"> / {p.displayUr}</span>}</td>
      <td className="flag">{p.flagCode && <span className="fl"><b>{p.flagCode}</b><small>{lang === 'ur' ? word('ur') : lang === 'both' ? `${word('en')} / ${word('ur')}` : word('en')}</small></span>}</td>
      <td className="num" dir="ltr">{p.unit ?? ''}</td>
      <td className="num" dir="ltr">{p.referenceText ?? ''}</td>
    </tr>
  )
}
