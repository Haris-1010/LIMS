import { useQueryClient } from '@tanstack/react-query'

/** One place that refreshes everything a result action can change. */
export function useRefreshResults() {
  const qc = useQueryClient()
  return async () => {
    await qc.invalidateQueries({ queryKey: ['results'] })
    await qc.invalidateQueries({ queryKey: ['orders'] })
    await qc.invalidateQueries({ queryKey: ['samples'] })
  }
}
