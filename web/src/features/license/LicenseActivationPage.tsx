import { useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, Check, Copy, FileKey2, KeyRound, ServerCog, ShieldCheck, Upload, WifiOff } from 'lucide-react'
import { api, ApiError } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Button } from '../../components/ui/Button'
import { AuthLayout } from '../auth/AuthLayout'
import { groupId, licenseKey, type LicenseReason, type LicenseStatus } from './licenseApi'
import './license.css'

const MAX_BYTES = 16 * 1024

/**
 * First-run (and expired-licence) screen: shows this server's installation ID to send to the licence issuer, and installs the
 * signed file they send back. Nothing leaves the server: the file is checked against the public key built into LIMS.
 */
export function LicenseActivationPage({ status, onActivated }: { status: LicenseStatus; onActivated: () => Promise<void> }) {
  const { t, i18n } = useTranslation()
  const qc = useQueryClient()
  const fileRef = useRef<HTMLInputElement>(null)
  const idRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [copied, setCopied] = useState(false)
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<{ reason: LicenseReason | null; text: string } | null>(null)

  // The expiry is a calendar day in UTC; show that day, whatever the PC's time zone.
  const expiry = status.expiresOnUtc
    ? new Intl.DateTimeFormat(i18n.language === 'ur' ? 'ur-PK' : 'en-GB', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' }).format(new Date(status.expiresOnUtc))
    : ''
  const reasonText = (r: LicenseReason) => t(`license.reason.${r}`, { date: expiry })

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(status.installationId)
    } catch {
      // Plain http on the LAN has no clipboard API: select the text and copy the old way.
      idRef.current?.select()
      document.execCommand('copy')
    }
    setCopied(true)
    window.setTimeout(() => setCopied(false), 2000)
  }

  const pick = (f: File | undefined) => {
    setFailure(null)
    if (fileRef.current) fileRef.current.value = ''
    if (!f) return
    if (f.size > MAX_BYTES) { setFile(null); setFailure({ reason: 'too_large', text: reasonText('too_large') }); return }
    setFile(f)
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
      await onActivated()
    } catch (e) {
      const reason = e instanceof ApiError && typeof e.data.reason === 'string' ? (e.data.reason as LicenseReason) : null
      setFailure({ reason, text: reason ? reasonText(reason) : errorMessage(t, e) })
    } finally {
      setBusy(false)
    }
  }

  const hasId = status.installationId.length > 0

  return (
    <AuthLayout
      hero={
        <>
          <span className="hero-eyebrow"><KeyRound size={13} aria-hidden />{t('license.hero.eyebrow')}</span>
          <h2>{t('license.hero.title')}</h2>
          <p>{t('license.hero.text')}</p>
          <div className="hero-features">
            <div className="hero-feature"><ServerCog size={18} aria-hidden /><strong>{t('license.hero.f1')}</strong><span>{t('license.hero.f1Text')}</span></div>
            <div className="hero-feature"><FileKey2 size={18} aria-hidden /><strong>{t('license.hero.f2')}</strong><span>{t('license.hero.f2Text')}</span></div>
            <div className="hero-feature"><WifiOff size={18} aria-hidden /><strong>{t('license.hero.f3')}</strong><span>{t('license.hero.f3Text')}</span></div>
          </div>
        </>
      }
    >
      <h1>{status.reason === 'expired' ? t('license.titleExpired') : t('license.title')}</h1>
      <p className="lead">{t('license.lead')}</p>

      <div className={`banner${status.reason === 'missing' ? '' : ' tone-warning'}`} role="status" style={{ marginBottom: 20 }}>
        <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
        <span>{reasonText(status.reason)}{status.customerName && <> · <span dir="auto">{status.customerName}</span></>}</span>
      </div>

      <ol className="license-steps">
        <li>
          <strong>{t('license.step1')}</strong>
          <span className="field-hint">{t('license.step1Hint')}</span>
          {hasId ? (
            <div className="license-id">
              <input ref={idRef} readOnly value={status.installationId} aria-label={t('license.installationId')} className="sr-select" />
              <code dir="ltr">{groupId(status.installationId)}</code>
              <Button size="sm" icon={copied ? <Check size={14} /> : <Copy size={14} />} onClick={() => void copy()}>{copied ? t('license.copied') : t('license.copy')}</Button>
            </div>
          ) : <p className="license-warning">{t('license.noId')}</p>}
        </li>
        <li>
          <strong>{t('license.step2')}</strong>
          <span className="field-hint">{t('license.step2Hint')}</span>
          <input ref={fileRef} type="file" accept=".json,application/json" hidden onChange={(e) => pick(e.target.files?.[0])} />
          <button type="button" className={`license-drop${file ? ' has-file' : ''}`} onClick={() => fileRef.current?.click()}
            onDragOver={(e) => e.preventDefault()} onDrop={(e) => { e.preventDefault(); pick(e.dataTransfer.files?.[0]) }}>
            <Upload size={18} aria-hidden />
            <span dir="auto">{file ? file.name : t('license.chooseFile')}</span>
          </button>
        </li>
      </ol>

      {failure && (
        <div className="banner tone-danger" role="alert" style={{ margin: '16px 0 0' }}>
          <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
          <span>{failure.text}</span>
        </div>
      )}

      <Button variant="primary" block icon={<ShieldCheck size={16} />} loading={busy} disabled={!file} onClick={() => void install()} style={{ marginTop: 20 }}>
        {t('license.activate')}
      </Button>
      <p className="login-secure"><WifiOff size={13} aria-hidden /> {t('license.offline')}</p>
    </AuthLayout>
  )
}
