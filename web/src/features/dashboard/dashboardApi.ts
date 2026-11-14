/* Contract of GET /api/dashboard/summary (see Lims.Application.Dashboard). Every figure comes from live data. */
import { api } from '../../lib/api'

export type DashboardRange = 'today' | 'yesterday' | 'd7' | 'd30' | 'this_month' | 'last_month' | 'all' | 'custom'

/** Order of the period menu in the toolbar. */
export const dashboardRanges: DashboardRange[] = ['today', 'yesterday', 'd7', 'd30', 'this_month', 'last_month', 'all', 'custom']
export type PipelineStage = 'awaitingCollection' | 'collected' | 'received' | 'inProcess' | 'completed' | 'rejected'
export type ActivityType = 'registration' | 'payment' | 'approval' | 'rejection' | 'critical'

/** A part of the page the caller has no permission for is empty and flagged false here, and the page hides it. */
export interface DashboardSections { patients: boolean; orders: boolean; samples: boolean; results: boolean; reports: boolean; money: boolean; corporate: boolean }

export interface DashboardSummary {
  range: DashboardRange
  generatedAtUtc: string
  branchId: number | null
  sections: DashboardSections
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
    resultId: number
    orderNo: string
    patient: string
    patientUr: string | null
    patientNo: string
    test: string
    testUr: string | null
    department: string
    departmentUr: string | null
    waitingMinutes: number
    flag: 'critical' | 'abnormal' | 'normal'
  }[]
  activity: { id: string; type: ActivityType; minutesAgo: number; params: Record<string, string> }[]
}

export const dashboardKeys = {
  summary: (range: DashboardRange, branchId: number | 'all', dates?: object) => ['dashboard', range, branchId, dates] as const,
}

export const fetchDashboardSummary = (range: DashboardRange, dates?: { from?: string; to?: string }) => api<DashboardSummary>('/api/dashboard/summary', { query: { range, ...dates } })
