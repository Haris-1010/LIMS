import { useEffect, useMemo, useState } from 'react'
import type { ReportDesign, ReportView } from './reportsApi'

export interface GraphTestOption { code: string; nameEn: string; nameUr: string | null }

/** Tests on this report that have a frozen history of at least two results for one of their parameters, so a graph can be drawn. */
export function graphCapableTests(view: ReportView): GraphTestOption[] {
  const history = view.snapshot.history ?? []
  return view.snapshot.sections.flatMap((sec) => sec.tests)
    .filter((test) => history.some((h) => h.testCode === test.code && h.points.length > 1 && test.parameters.some((p) => p.code === h.parameterCode)))
    .map((test) => ({ code: test.code, nameEn: test.nameEn, nameUr: test.nameUr }))
}

export interface GraphChoice {
  capable: GraphTestOption[]
  on: boolean
  setOn: (on: boolean) => void
  chosen: string[]
  setChosen: (codes: string[]) => void
  /** Codes to print graphs for; empty when graphs are off. Always an explicit list, so the design's default never overrides a "no". */
  value: string[]
}

/**
 * The print-time choice of historical graphs. It starts from the design's list of graph tests (the special tests set up on the
 * report template page); staff can switch graphs off, or pick other tests, for this print only.
 */
export function useGraphChoice(view: ReportView, design: ReportDesign): GraphChoice {
  const capable = useMemo(() => graphCapableTests(view), [view])
  const defaults = useMemo(() => capable.map((x) => x.code).filter((c) => (design.graphTests ?? []).includes(c)), [capable, design.graphTests])
  const [on, setOn] = useState(defaults.length > 0)
  const [chosen, setChosen] = useState<string[]>(defaults)
  // Another design has another list: start again from it.
  useEffect(() => { setChosen(defaults); setOn(defaults.length > 0) }, [defaults])
  return { capable, on, setOn, chosen, setChosen, value: on ? chosen : [] }
}
