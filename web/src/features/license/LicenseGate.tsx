import { useEffect, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api, LICENSE_EVENT } from '../../lib/api'
import { LicenseActivationPage } from './LicenseActivationPage'
import { licenseKey, type LicenseStatus } from './licenseApi'

/**
 * Shows the activation screen instead of the application until the server reports a valid licence (first run, expired
 * licence, server moved). The server refuses every other API call meanwhile, so this is only the friendly face of that rule.
 * If the licence state cannot be read (network), the application loads and shows its usual errors.
 */
export function LicenseGate({ children }: { children: ReactNode }) {
  const qc = useQueryClient()
  const { data, isLoading } = useQuery({
    queryKey: licenseKey,
    queryFn: () => api<LicenseStatus>('/api/license'),
    staleTime: 5 * 60_000,
    retry: 1,
  })

  // Any API call refused for the licence (for example it expired while the app was open) brings the screen back.
  useEffect(() => {
    const recheck = () => { void qc.invalidateQueries({ queryKey: licenseKey }) }
    window.addEventListener(LICENSE_EVENT, recheck)
    return () => window.removeEventListener(LICENSE_EVENT, recheck)
  }, [qc])

  if (isLoading) return null
  if (data && !data.isValid) {
    return (
      <LicenseActivationPage
        status={data}
        onActivated={async () => {
          // Everything fetched while unlicensed failed; load it again now.
          await qc.invalidateQueries()
        }}
      />
    )
  }
  return <>{children}</>
}
