import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api'
import { sampleKeys, type RejectionReason } from './samplesApi'

/** Everything that might have changed after a sample action. */
export function useRefreshSamples() {
  const qc = useQueryClient()
  return async () => {
    await qc.invalidateQueries({ queryKey: sampleKeys.all })
    await qc.invalidateQueries({ queryKey: ['orders'] })
  }
}

export function useRejectionReasons(includeInactive = false) {
  return useQuery({
    queryKey: sampleKeys.reasons(includeInactive),
    queryFn: () => api<RejectionReason[]>('/api/samples/rejection-reasons', { query: { includeInactive } }),
    staleTime: 60_000,
  })
}
