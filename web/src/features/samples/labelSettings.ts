/** Label size and copies are per workstation (different printers, different rolls), so they live in this browser only. */
export interface LabelSettings { widthMm: number; heightMm: number; copies: number }
export const DEFAULT_LABEL: LabelSettings = { widthMm: 50, heightMm: 25, copies: 1 }
const KEY = 'lims.label-settings'

export function loadLabelSettings(): LabelSettings {
  try {
    const raw = JSON.parse(localStorage.getItem(KEY) ?? 'null') as Partial<LabelSettings> | null
    return normalise({ ...DEFAULT_LABEL, ...raw })
  } catch {
    return DEFAULT_LABEL
  }
}

export function saveLabelSettings(s: LabelSettings) {
  try {
    localStorage.setItem(KEY, JSON.stringify(normalise(s)))
  } catch {
    /* storage unavailable: the setting just applies to this session */
  }
}

const clamp = (n: number, min: number, max: number, fallback: number) => (Number.isFinite(n) ? Math.min(max, Math.max(min, Math.round(n))) : fallback)
function normalise(s: LabelSettings): LabelSettings {
  return {
    widthMm: clamp(s.widthMm, 30, 120, DEFAULT_LABEL.widthMm),
    heightMm: clamp(s.heightMm, 15, 100, DEFAULT_LABEL.heightMm),
    copies: clamp(s.copies, 1, 10, 1),
  }
}

