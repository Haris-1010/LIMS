import type { SampleListItem } from './samplesApi'

export const PRINT_LABELS_EVENT = 'lims:print-labels'

/**
 * Asks the app-wide label host to print labels. The host lives in the app shell, so printing still happens
 * when the dialog that asked for it closes straight afterwards (e.g. "collect, then print").
 */
export function printLabels(samples: SampleListItem[]) {
  if (samples.length === 0) return
  window.dispatchEvent(new CustomEvent<SampleListItem[]>(PRINT_LABELS_EVENT, { detail: samples }))
}
