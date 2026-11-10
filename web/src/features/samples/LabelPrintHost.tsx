import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from '../../lib/api'
import { PrintJob } from './labels'
import { PRINT_LABELS_EVENT } from './labelBus'
import { loadLabelSettings, type LabelSettings } from './labelSettings'
import { useRefreshSamples } from './sampleHooks'
import type { SampleListItem } from './samplesApi'

/** Mounted once in the app shell: prints label requests and records each print on the sample. */
export function LabelPrintHost() {
  const refresh = useRefreshSamples()
  const [job, setJob] = useState<{ id: number; samples: SampleListItem[]; settings: LabelSettings } | null>(null)
  const counter = useRef(0)

  useEffect(() => {
    const onRequest = (e: Event) => setJob({ id: ++counter.current, samples: (e as CustomEvent<SampleListItem[]>).detail, settings: loadLabelSettings() })
    window.addEventListener(PRINT_LABELS_EVENT, onRequest)
    return () => window.removeEventListener(PRINT_LABELS_EVENT, onRequest)
  }, [])

  const done = useCallback(() => {
    setJob((current) => {
      if (current) {
        // Reprints carry a reason so the audit trail shows why a second label exists.
        void Promise.allSettled(current.samples.map((s) => api(`/api/samples/${s.id}/labels/printed`, { method: 'POST', body: { reason: s.labelPrintCount > 0 ? 'reprint' : null } })))
          .then(() => refresh())
      }
      return null
    })
  }, [refresh])

  return job ? <PrintJob key={job.id} samples={job.samples} settings={job.settings} onDone={done} /> : null
}
