/* Contract for /api/license (Lims.Application.Licensing.LicenseStatus). */

export type LicenseReason =
  | 'ok' | 'not_enforced' | 'missing' | 'identity' | 'folder' | 'too_large' | 'format' | 'signature'
  | 'unsupported' | 'product' | 'server' | 'issued' | 'expired' | 'save_failed' | 'no_file'

export interface LicenseStatus {
  isValid: boolean
  reason: LicenseReason
  /** 64 hex characters; sent to the licence issuer. Empty when the server identity could not be read. */
  installationId: string
  customerName: string | null
  licenseId: string | null
  /** Last valid day (UTC), or null for a perpetual licence. */
  expiresOnUtc: string | null
  message: string
}

export const licenseKey = ['license'] as const

/** The ID in groups of eight, easier to read out over the phone. */
export const groupId = (id: string) => id.match(/.{1,8}/g)?.join(' ') ?? id
