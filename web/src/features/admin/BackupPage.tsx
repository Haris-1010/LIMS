import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertTriangle, CheckCircle2, DatabaseBackup, RefreshCw } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { Badge, type Tone } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Skeleton } from '../../components/ui/Skeleton'

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

const KEY = ['backup-status'] as const
const JOBS = ['Full', 'Differential', 'Log', 'Offsite', 'Retention'] as const

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
 * Read-only view of the database backups made by the scheduled scripts (db/scripts). It cannot start, change or restore a backup:
 * the application's database account has no such rights by design (docs/BACKUP_RESTORE.md).
 */
export function BackupPage() {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const { data, isLoading, isFetching, error, refetch } = useQuery({
    queryKey: KEY, queryFn: () => api<BackupStatus>('/api/backup/status'), refetchInterval: 60_000,
  })

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

  if (!data.configured) {
    return (
      <div className="page">{head}
        <Card><EmptyState icon={<DatabaseBackup size={22} />} title={t('backup.notConfigured.title')} text={t('backup.notConfigured.text')} /></Card>
      </div>
    )
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
