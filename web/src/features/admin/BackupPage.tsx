import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import { AlertTriangle, CheckCircle2, DatabaseBackup, FolderOpen, RefreshCw } from 'lucide-react'
import { api, ApiError } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { Badge, type Tone } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Field } from '../../components/ui/Field'
import { SelectField, Switch } from '../../components/ui/Controls'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'

interface Problem { code: string; detail: string | null; whenUtc: string | null }
interface Job { name: string; lastSuccessUtc: string | null; lastDetail: string | null; lastFailureUtc: string | null; lastFailureDetail: string | null }
interface BackupFile { name: string; kind: string; stampUtc: string; sizeBytes: number }
interface Summary {
  fileCount: number; totalBytes: number; oldestFullUtc: string | null; newestFullUtc: string | null; newestLogUtc: string | null; recoverableToUtc: string | null
  fullMaxAgeHours: number; logMaxAgeMinutes: number; expectLogBackups: boolean; expectOffsite: boolean
}
interface BackupStatus {
  configured: boolean; folderReadable: boolean; folder: string | null; database: string; checkedAtUtc: string; healthy: boolean
  problems: Problem[]; jobs: Job[]; files: BackupFile[]; summary: Summary
}
interface BackupSettings {
  enabled: boolean; folder: string; offsiteFolder: string | null; fullFrequency: string; fullTime: string; logMinutes: number; retentionDays: number
  schedulerRunning: boolean; database: string; nextFullUtc: string | null; sqlServiceAccount: string | null; folderNote: string | null
}
interface BackupFolderBrowser { currentPath: string | null; parentPath: string | null; folders: { path: string; name: string }[] }

const KEY = ['backup-status'] as const
const SETTINGS_KEY = ['backup-settings'] as const
const JOBS = ['Full', 'Log', 'Offsite', 'Retention'] as const
const LOG_CHOICES = ['0', '5', '10', '15', '30', '60']

/** Backup errors carry the server's own explanation (folder, account, the command to run); show it under the translated text. */
function backupError(t: TFunction, e: unknown): string {
  const text = errorMessage(t, e)
  return e instanceof ApiError && e.code.startsWith('backup_') && e.message && e.message !== text ? text + ' ' + e.message : text
}

function formatSize(bytes: number, lang: string): string {
  const mb = bytes / (1024 * 1024)
  const nf = new Intl.NumberFormat(lang === 'ur' ? 'ur-PK' : 'en-GB', { maximumFractionDigits: mb >= 100 ? 0 : 1 })
  return mb >= 1024 ? `${nf.format(mb / 1024)} GB` : `${nf.format(mb)} MB`
}

function ago(utc: string, lang: string): string {
  const minutes = Math.round((new Date(utc).getTime() - Date.now()) / 60000)
  const rtf = new Intl.RelativeTimeFormat(lang === 'ur' ? 'ur-PK' : 'en', { numeric: 'auto' })
  if (Math.abs(minutes) < 90) return rtf.format(minutes, 'minute')
  if (Math.abs(minutes) < 60 * 36) return rtf.format(Math.round(minutes / 60), 'hour')
  return rtf.format(Math.round(minutes / 1440), 'day')
}

/**
 * Database backups: the automatic schedule (run by the server itself), a manual full backup, and how the backups are doing.
 */
export function BackupPage() {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const { data, isLoading, isFetching, error, refetch } = useQuery({
    queryKey: KEY, queryFn: () => api<BackupStatus>('/api/backup/status'), refetchInterval: 60_000,
  })
  const settingsQuery = useQuery({ queryKey: SETTINGS_KEY, queryFn: () => api<BackupSettings>('/api/backup/settings') })

  const head = (
    <div className="page-head">
      <div><h2>{t('backup.title')}</h2><p>{t('backup.subtitle')}</p></div>
      <div className="page-head-actions">
        <Button variant="secondary" icon={<RefreshCw size={16} />} loading={isFetching} onClick={() => void refetch()}>{t('backup.refresh')}</Button>
      </div>
    </div>
  )

  if (isLoading) return <div className="page">{head}<Card><Skeleton height={160} /></Card></div>
  if (error || !data) return <div className="page">{head}<Card><EmptyState icon={<AlertTriangle size={22} />} title={t('backup.loadFailed')} text={errorMessage(t, error)} /></Card></div>

  const setupCard = settingsQuery.data
    ? <ScheduleCard key={JSON.stringify(settingsQuery.data)} initial={settingsQuery.data} onChanged={() => void refetch()} />
    : settingsQuery.error
      ? <Card><EmptyState icon={<AlertTriangle size={22} />} title={t('backup.loadFailed')} text={errorMessage(t, settingsQuery.error)} /></Card>
      : <Card><Skeleton height={120} /></Card>

  if (!data.configured) {
    return <div className="page">{head}{setupCard}</div>
  }

  const job = (name: string) => data.jobs.find((j) => j.name === name)
  const columns: Column<BackupFile>[] = [
    { key: 'kind', header: t('backup.files.kind'), width: 130, render: (f) => <Badge tone={f.kind === 'Full' ? 'primary' : f.kind === 'Log' ? 'neutral' : 'info'}>{t(`backup.kind.${f.kind}`)}</Badge> },
    { key: 'time', header: t('backup.files.time'), render: (f) => <span className="num" dir="ltr">{formatDateTime(f.stampUtc, lang)}</span> },
    { key: 'name', header: t('backup.files.name'), render: (f) => <span className="num cell-sub" dir="ltr">{f.name}</span> },
    { key: 'size', header: t('backup.files.size'), align: 'end', render: (f) => <span className="num" dir="ltr">{formatSize(f.sizeBytes, lang)}</span> },
  ]

  return (
    <div className="page">
      {head}

      <section className={`backup-banner ${data.healthy ? 'is-ok' : 'is-bad'}`} role="status">
        <span className="backup-banner-icon" aria-hidden>{data.healthy ? <CheckCircle2 size={22} /> : <AlertTriangle size={22} />}</span>
        <div>
          <strong>{data.healthy ? t('backup.healthy') : t('backup.attention')}</strong>
          <span className="cell-sub">{t('backup.checkedAt', { time: formatDateTime(data.checkedAtUtc, lang) })}</span>
          {data.problems.length > 0 && (
            <ul className="backup-problems">
              {data.problems.map((p) => {
                const code = p.code.startsWith('jobFailed:') ? 'jobFailed' : p.code
                const jobName = p.code.startsWith('jobFailed:') ? t(`backup.job.${p.code.slice('jobFailed:'.length)}.name`, { defaultValue: p.code.slice('jobFailed:'.length) }) : ''
                return (
                  <li key={p.code}>
                    <span dir="auto">{t(`backup.problem.${code}`, { job: jobName, when: p.whenUtc ? formatDateTime(p.whenUtc, lang) : '', hours: data.summary.fullMaxAgeHours, minutes: data.summary.logMaxAgeMinutes })}</span>
                    {p.detail && <span className="cell-sub num" dir="auto">{p.detail}</span>}
                  </li>
                )
              })}
            </ul>
          )}
        </div>
      </section>

      {setupCard}

      {data.folderReadable && (
        <>
          <div className="backup-grid">
            <Card title={t('backup.recoverable.title')} subtitle={t('backup.recoverable.subtitle')}>
              {data.summary.recoverableToUtc ? (
                <>
                  <p className="backup-big num" dir="ltr">{formatDateTime(data.summary.recoverableToUtc, lang)}</p>
                  <p className="cell-sub">{ago(data.summary.recoverableToUtc, lang)}</p>
                </>
              ) : <p className="cell-sub">{t('backup.recoverable.none')}</p>}
              {data.summary.oldestFullUtc && <p className="field-hint">{t('backup.recoverable.since', { time: formatDateTime(data.summary.oldestFullUtc, lang) })}</p>}
            </Card>
            <Card title={t('backup.stored.title')} subtitle={data.folder ?? undefined}>
              <p className="backup-big num" dir="ltr">{formatSize(data.summary.totalBytes, lang)}</p>
              <p className="cell-sub">{t('backup.stored.files', { count: data.summary.fileCount })}</p>
            </Card>
          </div>

          <Card title={t('backup.jobs.title')} subtitle={t('backup.jobs.subtitle')}>
            <ul className="backup-jobs">
              {JOBS.map((name) => {
                const j = job(name)
                const failed = !!j?.lastFailureUtc && (!j.lastSuccessUtc || j.lastFailureUtc > j.lastSuccessUtc)
                const tone: Tone = !j ? 'neutral' : failed ? 'danger' : 'success'
                return (
                  <li key={name} className="backup-job">
                    <div>
                      <span className="cell-strong">{t(`backup.job.${name}.name`)}</span>
                      <span className="cell-sub">{t(`backup.job.${name}.about`)}</span>
                    </div>
                    <div className="backup-job-state">
                      <Badge tone={tone} dot>{!j ? t('backup.job.noReport') : failed ? t('backup.job.failed') : t('backup.job.ok')}</Badge>
                      {j?.lastSuccessUtc
                        ? <span className="cell-sub">{t('backup.job.lastSuccess', { when: ago(j.lastSuccessUtc, lang) })} <span className="num" dir="ltr">({formatDateTime(j.lastSuccessUtc, lang)})</span></span>
                        : j ? <span className="cell-sub">{t('backup.job.neverSucceeded')}</span> : null}
                      {failed && j?.lastFailureUtc && <span className="cell-sub backup-failure" dir="auto">{t('backup.job.lastFailure', { when: formatDateTime(j.lastFailureUtc, lang) })}{j.lastFailureDetail ? `: ${j.lastFailureDetail}` : ''}</span>}
                    </div>
                  </li>
                )
              })}
            </ul>
          </Card>

          <Card title={t('backup.files.title')} subtitle={t('backup.files.subtitle')} flush>
            <DataTable columns={columns} rows={data.files} rowKey={(f) => f.name} empty={<EmptyState icon={<DatabaseBackup size={22} />} title={t('backup.files.empty')} />} />
          </Card>
        </>
      )}

      <p className="field-hint backup-note">{t('backup.note')}</p>
    </div>
  )
}

/** The automatic schedule. Saving also makes the folder ready on the server (created, SQL Server allowed to write to it). */
function ScheduleCard({ initial, onChanged }: { initial: BackupSettings; onChanged: () => void }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const [enabled, setEnabled] = useState(initial.enabled)
  const [folder, setFolder] = useState(initial.folder)
  const [offsite, setOffsite] = useState(initial.offsiteFolder ?? '')
  const [fullFrequency, setFullFrequency] = useState(initial.fullFrequency)
  const [fullTime, setFullTime] = useState(initial.fullTime)
  const [logMinutes, setLogMinutes] = useState(String(initial.logMinutes))
  const [retentionDays, setRetentionDays] = useState(String(initial.retentionDays))
  const [picker, setPicker] = useState<'backup' | 'offsite' | null>(null)
  const dirty = enabled !== initial.enabled || folder !== initial.folder || offsite !== (initial.offsiteFolder ?? '')
    || fullFrequency !== initial.fullFrequency || fullTime !== initial.fullTime
    || logMinutes !== String(initial.logMinutes) || retentionDays !== String(initial.retentionDays)
  const logChoices = LOG_CHOICES.includes(String(initial.logMinutes)) ? LOG_CHOICES : [...LOG_CHOICES, String(initial.logMinutes)]

  const save = useMutation({
    mutationFn: () => api<BackupSettings>('/api/backup/settings', {
      method: 'PUT',
      body: { enabled, folder: folder.trim(), offsiteFolder: offsite.trim() || null, fullFrequency, fullTime, logMinutes: Number(logMinutes), retentionDays: Number(retentionDays) },
    }),
    onSuccess: (saved) => {
      if (saved.folderNote) toast.show({ tone: 'info', title: t('backup.savedWithNote'), text: saved.folderNote })
      else toast.show({ tone: 'success', title: t('backup.saved') })
      qc.setQueryData(SETTINGS_KEY, { ...saved, folderNote: null, sqlServiceAccount: null })
      onChanged()
    },
    onError: (e) => toast.show({ tone: 'danger', title: t('backup.saveFailed'), text: backupError(t, e) }),
  })
  const runFull = useMutation({
    mutationFn: () => api('/api/backup/run', { method: 'POST' }),
    onSuccess: async () => {
      toast.show({ tone: 'success', title: t('backup.runDone') })
      await qc.invalidateQueries({ queryKey: SETTINGS_KEY })
      onChanged()
    },
    onError: (e) => toast.show({ tone: 'danger', title: t('backup.runFailed'), text: backupError(t, e) }),
  })

  const state = !initial.schedulerRunning ? t('backup.schedule.serverOff')
    : !initial.enabled ? t('backup.schedule.off')
    : initial.nextFullUtc ? t('backup.schedule.on', { time: formatDateTime(initial.nextFullUtc, i18n.language) })
    : t('backup.schedule.onNoTime')

  return (
    <Card title={t('backup.setupTitle')} subtitle={t('backup.setupSubtitle', { database: initial.database })}>
      <div className="form-stack">
        <Switch checked={enabled} onChange={setEnabled} label={t('backup.enabled')} hint={state} />
        <div className="form-grid">
          <FolderInput label={t('backup.folder')} hint={t('backup.folderHint')} value={folder} onChange={setFolder} onBrowse={() => setPicker('backup')} />
          <FolderInput label={t('backup.offsite')} hint={t('backup.offsiteHint')} value={offsite} onChange={setOffsite} onBrowse={() => setPicker('offsite')} placeholder="E:\LimsOffsite" />
          <SelectField label={t('backup.frequency')} value={fullFrequency} onChange={(e) => setFullFrequency(e.target.value)}>
            <option value="Daily">{t('backup.daily')}</option>
            <option value="Weekly">{t('backup.weekly')}</option>
          </SelectField>
          <Field label={t('backup.fullTime')} type="time" value={fullTime} onChange={(e) => setFullTime(e.target.value)} />
          <SelectField label={t('backup.logMinutes')} hint={t('backup.logHint')} value={logMinutes} onChange={(e) => setLogMinutes(e.target.value)}>
            {logChoices.map((m) => <option key={m} value={m}>{m === '0' ? t('backup.logOff') : t('backup.everyMinutes', { count: Number(m) })}</option>)}
          </SelectField>
          <Field label={t('backup.retentionDays')} type="number" min={1} max={365} value={retentionDays} onChange={(e) => setRetentionDays(e.target.value)} hint={t('backup.retentionHint')} />
        </div>
        <div className="page-head-actions">
          <Button loading={save.isPending} onClick={() => save.mutate()} disabled={!folder.trim() || !dirty}>{t('backup.save')}</Button>
          <Button variant="secondary" icon={<DatabaseBackup size={16} />} loading={runFull.isPending} disabled={dirty} onClick={() => runFull.mutate()}>{t('backup.runNow')}</Button>
        </div>
      </div>
      {picker && (
        <FolderPicker
          onClose={() => setPicker(null)}
          onChoose={(path) => { if (picker === 'backup') setFolder(path); else setOffsite(path); setPicker(null) }}
        />
      )}
    </Card>
  )
}

function FolderInput({ label, hint, value, onChange, onBrowse, placeholder }: {
  label: string; hint?: string; value: string; onChange: (v: string) => void; onBrowse: () => void; placeholder?: string
}) {
  const { t } = useTranslation()
  return (
    <div className="field">
      <span className="field-label">{label}</span>
      <div className="input-wrap">
        <input className="input has-action" dir="ltr" value={value} onChange={(e) => onChange(e.target.value)} placeholder={placeholder} aria-label={label} />
        <span className="input-action"><Button type="button" variant="ghost" icon={<FolderOpen size={16} />} onClick={onBrowse}>{t('backup.browse')}</Button></span>
      </div>
      {hint && <span className="field-hint">{hint}</span>}
    </div>
  )
}

/** Browses folders on the LIMS server (not on this PC). */
function FolderPicker({ onClose, onChoose }: { onClose: () => void; onChoose: (path: string) => void }) {
  const { t } = useTranslation()
  const [path, setPath] = useState<string | null>(null)
  const query = useQuery({
    queryKey: ['backup-folders', path],
    queryFn: () => api<BackupFolderBrowser>(`/api/backup/folders${path ? `?path=${encodeURIComponent(path)}` : ''}`),
  })
  const browse = query.data
  return (
    <Modal open title={t('backup.folderPicker.title')} subtitle={t('backup.folderPicker.subtitle')} onClose={onClose} size="md"
      footer={<Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>}>
      {query.isLoading ? <Skeleton height={120} /> : query.error ? <EmptyState icon={<AlertTriangle size={22} />} title={t('backup.folderPicker.failed')} text={errorMessage(t, query.error)} /> : browse && (
        <div className="form-stack">
          <div className="cell-sub num" dir="ltr">{browse.currentPath ?? t('backup.folderPicker.drives')}</div>
          <div className="page-head-actions">
            {browse.currentPath && <Button variant="secondary" onClick={() => setPath(browse.parentPath)}>{t('backup.folderPicker.up')}</Button>}
            {browse.currentPath && <Button onClick={() => onChoose(browse.currentPath!)}>{t('backup.folderPicker.choose')}</Button>}
          </div>
          <div className="form-stack">
            {browse.folders.map((f) => <Button key={f.path} variant="ghost" icon={<FolderOpen size={16} />} onClick={() => setPath(f.path)}>{f.name}</Button>)}
            {browse.folders.length === 0 && <span className="cell-sub">{t('backup.folderPicker.empty')}</span>}
          </div>
        </div>
      )}
    </Modal>
  )
}
