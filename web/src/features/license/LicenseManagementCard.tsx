import { useRef, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, Check, Copy, FileKey2, Upload } from 'lucide-react'
import { api, ApiError } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { useToast } from '../../components/ui/Toast'
import { groupId, licenseKey, type LicenseReason, type LicenseStatus } from './licenseApi'

const MAX_BYTES = 16 * 1024

export function LicenseManagementCard() {
  const { t, i18n } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const inputRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [busy, setBusy] = useState(false)
  const [copied, setCopied] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const { data: status } = useQuery({
    queryKey: licenseKey,
    queryFn: () => api<LicenseStatus>('/api/license'),
    staleTime: 5 * 60_000,
  })

  const expiry = status?.expiresOnUtc
    ? new Intl.DateTimeFormat(i18n.language === 'ur' ? 'ur-PK' : 'en-GB', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
      timeZone: 'UTC',
    }).format(new Date(status.expiresOnUtc))
    : null

  const pick = (next: File | undefined) => {
    setFailure(null)
    if (inputRef.current) inputRef.current.value = ''
    if (!next) return
    if (next.size > MAX_BYTES) {
      setFile(null)
      setFailure(t('license.reason.too_large'))
      return
    }
    setFile(next)
  }

  const copyId = async () => {
    if (!status?.installationId) return
    try {
      await navigator.clipboard.writeText(status.installationId)
    } catch {
      setFailure(t('license.copyFailed'))
      return
    }
    setCopied(true)
    window.setTimeout(() => setCopied(false), 2000)
  }

  const install = async () => {
    if (!file) return
    setBusy(true)
    setFailure(null)
    try {
      const body = new FormData()
      body.append('file', file)
      const result = await api<LicenseStatus>('/api/license/install', { method: 'POST', body })
      qc.setQueryData(licenseKey, result)
      setFile(null)
      toast.show({ tone: 'success', title: t('license.installed') })
      await qc.invalidateQueries({ queryKey: licenseKey })
    } catch (e) {
      const reason = e instanceof ApiError && typeof e.data.reason === 'string' ? e.data.reason as LicenseReason : null
      setFailure(reason === 'expired' ? t('license.uploadExpired') : reason ? t(`license.reason.${reason}`) : errorMessage(t, e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card title={t('license.manageTitle')} subtitle={t('license.manageText')}>
      <dl className="license-details">
        <div><dt>{t('license.customer')}</dt><dd dir="auto">{status?.customerName || '—'}</dd></div>
        <div><dt>{t('license.expires')}</dt><dd>{expiry || (status?.isValid ? t('license.perpetual') : '—')}</dd></div>
      </dl>
      {status?.installationId && (
        <div className="license-id">
          <FileKey2 size={16} aria-hidden />
          <code dir="ltr">{groupId(status.installationId)}</code>
          <Button size="sm" icon={copied ? <Check size={14} /> : <Copy size={14} />} onClick={() => void copyId()}>
            {copied ? t('license.copied') : t('license.copy')}
          </Button>
        </div>
      )}
      <p className="field-hint license-renewal-hint">{t('license.renewalHint')}</p>
      <div className="license-upload">
        <input ref={inputRef} type="file" accept=".json,application/json" hidden onChange={(e) => pick(e.target.files?.[0])} />
        <Button variant="secondary" icon={<Upload size={15} />} onClick={() => inputRef.current?.click()} disabled={busy}>
          {file ? file.name : t('license.chooseFile')}
        </Button>
        <Button variant="primary" icon={<Check size={15} />} loading={busy} disabled={!file} onClick={() => void install()}>
          {t('license.installRenewal')}
        </Button>
      </div>
      {failure && (
        <div className="banner tone-danger license-renewal-error" role="alert">
          <AlertCircle size={16} aria-hidden />
          <span>{failure}</span>
        </div>
      )}
    </Card>
  )
}
