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
  const jobRef = useRef(job)
  const counter = useRef(0)

  useEffect(() => {
    const onRequest = (e: Event) => {
      const next = { id: ++counter.current, samples: (e as CustomEvent<SampleListItem[]>).detail, settings: loadLabelSettings() }
      jobRef.current = next
      setJob(next)
    }
    window.addEventListener(PRINT_LABELS_EVENT, onRequest)
    return () => window.removeEventListener(PRINT_LABELS_EVENT, onRequest)
  }, [])

  const done = useCallback(() => {
    // The recording lives outside the state updater: StrictMode invokes updaters
    // twice in development, which would send the print record twice.
    const current = jobRef.current
    jobRef.current = null
    setJob(null)
    if (current) {
      // Reprints carry a reason so the audit trail shows why a second label exists.
      void Promise.allSettled(current.samples.map((s) => api(`/api/samples/${s.id}/labels/printed`, { method: 'POST', body: { reason: s.labelPrintCount > 0 ? 'reprint' : null } })))
        .then(() => refresh())
    }
  }, [refresh])

  return job ? <PrintJob key={job.id} samples={job.samples} settings={job.settings} onDone={done} /> : null
}
