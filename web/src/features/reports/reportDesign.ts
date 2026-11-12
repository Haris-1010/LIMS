import type { CSSProperties } from 'react'
import type { DesignStyle, ReportBlock, ReportDesign, ReportLayoutDto, ReportView } from './reportsApi'

export const ALL_BLOCKS: ReportBlock[] = ['patient', 'results', 'pending', 'signatures', 'disclaimer']

/** The built-in design: the look reports had before designs existed (black and white). Same values as the server's default. */
export const CLASSIC: ReportDesign = {
  style: 'classic',
  accentColor: '#1f4e79',
  fontSizePt: 9.5,
  marginTopMm: 12,
  marginBottomMm: 14,
  marginSideMm: 12,
  showLetterhead: true,
  logoPosition: 'start',
  useHeaderImage: false,
  showFooterImage: false,
  showRangeBars: true,
  showSingleParameterCharts: true,
  showFlagWords: true,
  colourFlags: false,
  tableStyle: 'lines',
  blocks: [...ALL_BLOCKS],
}

/** Ready-made starting points. Choosing one keeps the margins, letterhead setting and part order already chosen. */
export const PRESETS: Record<DesignStyle, Partial<ReportDesign>> = {
  classic: { style: 'classic', accentColor: '#1f2937', fontSizePt: 9.5, tableStyle: 'lines', colourFlags: false, logoPosition: 'start' },
  modern: { style: 'modern', accentColor: '#0f766e', fontSizePt: 9.5, tableStyle: 'zebra', colourFlags: true, logoPosition: 'start' },
  compact: { style: 'compact', accentColor: '#334155', fontSizePt: 8.5, tableStyle: 'lines', colourFlags: false, logoPosition: 'start' },
  colourful: { style: 'colourful', accentColor: '#7c3aed', fontSizePt: 9.5, tableStyle: 'zebra', colourFlags: true, logoPosition: 'center' },
}

export const ACCENT_SWATCHES = ['#1f4e79', '#0f766e', '#15803d', '#b91c1c', '#c2410c', '#7c3aed', '#be185d', '#1f2937']

export const withPreset = (d: ReportDesign, style: DesignStyle): ReportDesign => ({ ...d, ...PRESETS[style] })

/** The design used when no layout is chosen: the organisation's default, else the built-in classic. */
export function defaultLayout(layouts: ReportLayoutDto[] | undefined): ReportLayoutDto | null {
  return layouts?.find((l) => l.isDefault) ?? null
}

export function designFor(layouts: ReportLayoutDto[] | undefined, id: number | null): ReportDesign {
  if (id === null) return CLASSIC
  return layouts?.find((l) => l.id === id)?.design ?? CLASSIC
}

/** CSS custom properties the report stylesheet reads. */
export function designVars(d: ReportDesign): CSSProperties {
  return {
    '--rp-accent': d.accentColor,
    '--rp-font': `${d.fontSizePt}pt`,
    '--rp-m-top': `${d.marginTopMm}mm`,
    '--rp-m-bottom': `${d.marginBottomMm}mm`,
    '--rp-m-side': `${d.marginSideMm}mm`,
  } as CSSProperties
}

export function designClasses(d: ReportDesign): string {
  return [
    `rp-style-${d.style}`,
    `rp-table-${d.tableStyle}`,
    `rp-logo-${d.logoPosition}`,
    d.colourFlags ? 'rp-colour-flags' : '',
    d.showLetterhead ? '' : 'rp-no-letterhead',
  ].filter(Boolean).join(' ')
}

export interface BarGeometry {
  /** Zone edges and the marker, as percentages of the bar width. */
  lowEnd: number
  highStart: number
  marker: number
  /** The value lies beyond the drawn scale; the marker sits at the edge. */
  clipped: boolean
}

/**
 * Where to draw a result on its range bar. Only for a number with at least one approved limit; anything else gets no bar.
 * Both limits: the normal zone takes the middle, with 60% of its width shown on either side. Only an upper limit ("< 200"):
 * the scale runs from 0 to 1.6 × the limit. Only a lower limit ("> 40"): from 0 to 2 × the limit.
 */
export function rangeBar(value: number | null | undefined, low: number | null | undefined, high: number | null | undefined): BarGeometry | null {
  if (value === null || value === undefined || !Number.isFinite(value)) return null
  const lo = low ?? null
  const hi = high ?? null
  let min: number
  let max: number
  if (lo !== null && hi !== null) {
    if (hi < lo) return null
    const span = hi - lo || Math.abs(hi) * 0.1 || 1
    min = lo - span * 0.6
    max = hi + span * 0.6
  } else if (hi !== null) {
    if (hi <= 0) return null
    min = 0
    max = hi * 1.6
  } else if (lo !== null) {
    if (lo <= 0) return null
    min = 0
    max = lo * 2
  } else return null

  const pct = (x: number) => ((x - min) / (max - min)) * 100
  const raw = pct(value)
  return {
    lowEnd: lo === null ? 0 : pct(lo),
    highStart: hi === null ? 100 : pct(hi),
    marker: Math.min(98, Math.max(2, raw)),
    clipped: raw < 0 || raw > 100,
  }
}

const P = (code: string, nameEn: string, display: string, unit: string | null, flag: 0 | 1 | 2 | 3 | 4 | 5 | 6, ref: string | null, value?: number, low?: number | null, high?: number | null, groupName: string | null = null) => ({
  code, nameEn, nameUr: null, groupName, resultType: 1 as const, display, displayUr: null, unit, flag,
  flagCode: flag === 2 ? 'L' : flag === 3 ? 'H' : flag === 4 ? 'LL' : flag === 5 ? 'HH' : flag === 6 ? 'A' : '',
  referenceText: ref, value, low, high,
})

/** A made-up report used only to preview designs (never stored, never printed as a real report). */
export function sampleView(): ReportView {
  const now = new Date().toISOString()
  return {
    // Not a preview of a real order: no "preview" watermark on the design pages.
    isPreview: false,
    meta: { id: null, reportNo: 'REP-SAMPLE', version: 1, status: 1, language: 0, issuedAtUtc: now, issuedByName: 'Sample', printCount: 0, isDuplicate: false, isOutdated: false, fingerprint: '0123456789AB', supersededByVersion: null },
    snapshot: {
      schemaVersion: 2, reportNo: 'REP-SAMPLE', version: 1, language: 0, status: 1, issuedAtUtc: now, issuedByName: 'Sample', issuedByNameUr: null, amendmentReason: null, replacesReportNo: null,
      letterhead: {
        organizationNameEn: 'Your Diagnostic Laboratory', organizationNameUr: null, labNameEn: 'Main Laboratory', labNameUr: null,
        addressEn: '12 Main Road, City', addressUr: null, phone: '042-111-000-000', email: 'lab@example.test',
        headerNoteEn: 'Open 24 hours · Home sampling available', headerNoteUr: null, footerNoteEn: 'Results relate only to the sample received.', footerNoteUr: null,
        disclaimerEn: 'This report is not a diagnosis. Please discuss the results with your doctor.', disclaimerUr: null,
      },
      patient: { id: 0, patientNo: 'P-000123', nameEn: 'Sample Patient', nameUr: null, sex: 2, dateOfBirth: null, isDateOfBirthEstimated: false, ageYears: 34, ageMonths: 0, ageDays: 0, mobile: '0300 1234567' },
      order: { orderId: 0, orderNo: 'ORD-0001', priority: 0, branchNameEn: 'Main Laboratory', branchNameUr: null, referringDoctor: 'Dr. Ahmed', registeredAtUtc: now, collectedAtUtc: now, receivedAtUtc: now },
      sections: [
        {
          departmentNameEn: 'Haematology', departmentNameUr: null, tests: [{
            orderItemId: 1, code: 'CBC', nameEn: 'Complete Blood Count', nameUr: null, reportNotes: null, resultVersion: 1, approvedAtUtc: now, comment: null,
            parameters: [
              P('HB', 'Haemoglobin', '10.8', 'g/dL', 2, '12.0 – 15.0', 10.8, 12, 15),
              P('WBC', 'Total leucocyte count', '7.4', 'x10³/µL', 1, '4.0 – 11.0', 7.4, 4, 11),
              P('NEUT', 'Neutrophils', '62', '%', 1, '40 – 80', 62, 40, 80, 'Differential count'),
              P('LYMPH', 'Lymphocytes', '44', '%', 3, '20 – 40', 44, 20, 40, 'Differential count'),
              P('PLT', 'Platelet count', '265', 'x10³/µL', 1, '150 – 410', 265, 150, 410),
            ],
          }],
        },
        {
          departmentNameEn: 'Clinical Chemistry', departmentNameUr: null, tests: [{
            orderItemId: 2, code: 'LIPID', nameEn: 'Lipid Profile', nameUr: null, reportNotes: 'Fasting sample (12 hours).', resultVersion: 1, approvedAtUtc: now, comment: null,
            parameters: [
              P('TCHOL', 'Total cholesterol', '232', 'mg/dL', 3, '< 200', 232, null, 200),
              P('HDL', 'HDL cholesterol', '58', 'mg/dL', 1, '> 50', 58, 50, null),
              P('TG', 'Triglycerides', '128', 'mg/dL', 1, '< 150', 128, null, 150),
            ],
          }],
        },
      ],
      pending: [{ code: 'VITD', nameEn: 'Vitamin D (25-OH)', nameUr: null }],
      signatories: [
        { userId: 0, nameEn: 'Dr. Sample Pathologist', nameUr: null, designation: 'Consultant Pathologist', registrationNo: 'PMDC 12345', hasSignature: false, approved: true },
        { userId: -1, nameEn: 'Sample Technologist', nameUr: null, designation: 'Medical Technologist', registrationNo: null, hasSignature: false, approved: false },
      ],
    },
  }
}

export interface GaugeScale {
  /** Drawn scale, in result units. */
  min: number
  max: number
  /** Axis ticks (round numbers inside the scale), with their labels. */
  ticks: { at: number; label: string }[]
  /** Position (0–100 % of the width) of a number on the scale, clamped to the edges. */
  pos: (x: number) => number
  /** The value lies beyond the drawn scale; its pointer sits at the edge. */
  clipped: boolean
}

/**
 * The scale for the large single-parameter graph. The same zones as `rangeBar`, but the scale is widened to show a value that
 * lies a little outside it (up to one more scale width), and it gets round-numbered axis ticks.
 */
export function gaugeScale(value: number | null | undefined, low: number | null | undefined, high: number | null | undefined): GaugeScale | null {
  const g = rangeBar(value, low, high)
  if (!g || value === null || value === undefined) return null
  const lo = low ?? null
  const hi = high ?? null
  let min: number
  let max: number
  if (lo !== null && hi !== null) {
    const span = hi - lo || Math.abs(hi) * 0.1 || 1
    min = lo - span * 0.6
    max = hi + span * 0.6
  } else if (hi !== null) { min = 0; max = hi * 1.6 }
  else { min = 0; max = (lo as number) * 2 }
  // A scale that starts below zero only for symmetry looks wrong on a lab report when the limits are positive.
  if (min < 0 && (lo === null || lo >= 0)) min = 0

  const width = max - min
  let clipped = false
  if (value > max) {
    if (value <= max + width) max = value + width * 0.08
    else { max = max + width; clipped = true }
  } else if (value < min) {
    if (value >= min - width) min = value - width * 0.08
    else { min = min - width; clipped = true }
  }

  const step = niceStep((max - min) / 6)
  const decimals = Math.max(0, -Math.floor(Math.log10(step) + 1e-9))
  const ticks: { at: number; label: string }[] = []
  for (let t = Math.ceil(min / step - 1e-9) * step; t <= max + step * 1e-6; t += step) {
    ticks.push({ at: t, label: (Math.abs(t) < step * 1e-6 ? 0 : t).toFixed(decimals) })
  }
  const pos = (x: number) => Math.min(100, Math.max(0, ((x - min) / (max - min)) * 100))
  return { min, max, ticks, pos, clipped }
}

/** 1, 2, 2.5 or 5 × a power of ten, at least `raw`. */
function niceStep(raw: number): number {
  if (!(raw > 0) || !Number.isFinite(raw)) return 1
  const p = Math.pow(10, Math.floor(Math.log10(raw)))
  const f = raw / p
  return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10) * p
}
