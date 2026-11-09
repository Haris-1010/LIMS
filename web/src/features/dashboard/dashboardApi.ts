/**
 * Dashboard data contract + PROTOTYPE data source.
 *
 * The types below are the contract the real endpoint (GET /api/dashboard/summary, Phase 12) will
 * return. Until then `fetchDashboardSummary` generates deterministic sample figures so the layout
 * can be reviewed; the page shows a "Preview data" banner while `isPreview` is true.
 * Nothing here is clinical or financial truth.
 */
export type DashboardRange = 'today' | 'd7' | 'd30'

export interface DashboardSummary {
  isPreview: boolean
  stats: {
    totalPatients: number
    registrations: number
    registrationsDelta: number
    orders: number
    ordersDelta: number
    pendingSamples: number
    overdueSamples: number
    testsInProgress: number
    departments: number
    awaitingApproval: number
    oldestWaitingMinutes: number
    completedReports: number
    readyToPrint: number
    cashCollected: number
    cashDelta: number
    corporateOutstanding: number
    corporateClients: number
  }
  trend: { date: string; cash: number; credit: number }[]
  pipeline: { key: PipelineStage; count: number }[]
  approvals: {
    orderNo: string
    patient: string
    patientNo: string
    test: string
    department: string
    waitingMinutes: number
    flag: 'critical' | 'abnormal' | 'normal'
  }[]
  activity: { id: number; type: ActivityType; minutesAgo: number; params: Record<string, string> }[]
}

export type PipelineStage = 'awaitingCollection' | 'collected' | 'received' | 'inProcess' | 'completed' | 'rejected'
export type ActivityType = 'registration' | 'payment' | 'approval' | 'sample' | 'rejection' | 'critical'

/** Small seeded PRNG so preview numbers stay stable for a given filter. */
function rng(seed: number) {
  let s = seed >>> 0
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0
    return s / 2 ** 32
  }
}

export async function fetchDashboardSummary(range: DashboardRange, branchId: number | 'all'): Promise<DashboardSummary> {
  await new Promise((r) => setTimeout(r, 650)) // simulate network so skeletons are visible

  const days = range === 'today' ? 1 : range === 'd7' ? 7 : 30
  const branchFactor = branchId === 'all' ? 1 : branchId === 1 ? 0.55 : 0.22
  const rand = rng(days * 97 + (branchId === 'all' ? 0 : branchId) * 31)
  const scale = (n: number) => Math.round(n * branchFactor)
  const between = (a: number, b: number) => a + Math.round(rand() * (b - a))

  const trendDays = range === 'd30' ? 30 : 14
  const today = new Date()
  const trend = Array.from({ length: trendDays }, (_, i) => {
    const d = new Date(today)
    d.setDate(today.getDate() - (trendDays - 1 - i))
    const weekend = d.getDay() === 0
    return {
      date: d.toISOString().slice(0, 10),
      cash: scale(between(weekend ? 60_000 : 140_000, weekend ? 110_000 : 260_000)),
      credit: scale(between(weekend ? 15_000 : 40_000, weekend ? 40_000 : 120_000)),
    }
  })
  const inRange = trend.slice(-days)
  const cashCollected = inRange.reduce((s, d) => s + d.cash, 0)

  const names = ['Ayesha Siddiqui', 'Muhammad Bilal', 'Fatima Noor', 'Hamza Tariq', 'Zainab Ali', 'Usman Farooq', 'Sana Javed']
  const tests = [
    ['Complete Blood Count', 'Haematology'],
    ['Lipid Profile', 'Chemistry'],
    ['HbA1c', 'Chemistry'],
    ['Thyroid Profile', 'Immunology'],
    ['Urine Routine', 'Clinical Pathology'],
    ['Serum Electrolytes', 'Chemistry'],
  ] as const
  const flags = ['critical', 'abnormal', 'normal', 'normal', 'abnormal', 'normal'] as const

  return {
    isPreview: true,
    stats: {
      totalPatients: scale(18_642),
      registrations: scale(between(38, 64) * days),
      registrationsDelta: 4.2 + rand() * 6,
      orders: scale(between(70, 110) * days),
      ordersDelta: -2 + rand() * 9,
      pendingSamples: scale(between(18, 34)),
      overdueSamples: scale(between(2, 6)),
      testsInProgress: scale(between(60, 120)),
      departments: 5,
      awaitingApproval: scale(between(14, 28)),
      oldestWaitingMinutes: between(35, 140),
      completedReports: scale(between(60, 95) * days),
      readyToPrint: scale(between(8, 20)),
      cashCollected,
      cashDelta: -3 + rand() * 12,
      corporateOutstanding: scale(2_486_500),
      corporateClients: branchId === 'all' ? 14 : branchId === 1 ? 9 : 3,
    },
    trend,
    pipeline: [
      { key: 'awaitingCollection', count: scale(between(12, 22)) },
      { key: 'collected', count: scale(between(20, 35)) },
      { key: 'received', count: scale(between(25, 40)) },
      { key: 'inProcess', count: scale(between(40, 70)) },
      { key: 'completed', count: scale(between(90, 140)) },
      { key: 'rejected', count: scale(between(2, 6)) },
    ],
    approvals: tests.map(([test, department], i) => ({
      orderNo: `ORD-2611-${String(4210 + i * 7).padStart(5, '0')}`,
      patient: names[i]!,
      patientNo: `P-${String(18_000 + i * 113).padStart(6, '0')}`,
      test,
      department,
      waitingMinutes: 140 - i * 19,
      flag: flags[i]!,
    })),
    activity: [
      { id: 1, type: 'critical', minutesAgo: 3, params: { test: 'Serum Potassium', name: 'Hamza Tariq' } },
      { id: 2, type: 'payment', minutesAgo: 7, params: { amount: 'Rs 4,850', order: 'ORD-2611-04252' } },
      { id: 3, type: 'registration', minutesAgo: 12, params: { name: 'Sana Javed' } },
      { id: 4, type: 'sample', minutesAgo: 18, params: { barcode: 'S2611-008421', dept: 'Haematology' } },
      { id: 5, type: 'approval', minutesAgo: 26, params: { test: 'Lipid Profile', name: 'Muhammad Bilal' } },
      { id: 6, type: 'rejection', minutesAgo: 41, params: { barcode: 'S2611-008377' } },
    ],
  }
}
