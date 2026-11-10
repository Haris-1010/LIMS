import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { RotateCcw } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Switch } from '../../components/ui/Controls'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'

type SettingKind = 1 | 2
interface Setting {
  key: string; group: string; kind: SettingKind; value: string; defaultValue: string; isDefault: boolean; min: number | null; max: number | null
  descriptionEn: string; descriptionUr: string; rowVersion: string
}

const KEY = ['settings'] as const

/** The business settings the system acts on. Each has fixed limits; changes are audited and a stale page cannot overwrite a newer value. */
export function SettingsPage() {
  const { t, i18n } = useTranslation()
  const { data, isLoading } = useQuery({ queryKey: KEY, queryFn: () => api<Setting[]>('/api/settings') })
  const groups = [...new Set(data?.map((s) => s.group) ?? [])]

  return (
    <div className="page">
      <div className="page-head"><div><h2>{t('settings.title')}</h2><p>{t('settings.subtitle')}</p></div></div>
      {isLoading ? <section className="card"><div className="card-body"><Skeleton height={180} /></div></section> : groups.map((g) => (
        <section className="card" key={g}>
          <div className="card-head"><h3>{t(`settings.group.${g}`)}</h3></div>
          <ul className="settings-list">
            {data!.filter((s) => s.group === g).map((s) => <SettingRow key={`${s.key}-${s.rowVersion}`} setting={s} lang={i18n.language} />)}
          </ul>
        </section>
      ))}
    </div>
  )
}

function SettingRow({ setting: s, lang }: { setting: Setting; lang: string }) {
  const { t } = useTranslation()
  const toast = useToast()
  const qc = useQueryClient()
  const [text, setText] = useState(s.value)
  const save = useMutation({
    mutationFn: (value: string) => api<Setting>(`/api/settings/${encodeURIComponent(s.key)}`, { method: 'PUT', body: { value, rowVersion: s.rowVersion || null } }),
    onSuccess: async () => { toast.show({ tone: 'success', title: t('settings.saved') }); await qc.invalidateQueries({ queryKey: KEY }) },
    onError: async (e) => { toast.show({ tone: 'danger', title: t('settings.failed'), text: errorMessage(t, e) }); await qc.invalidateQueries({ queryKey: KEY }) },
  })
  const description = lang === 'ur' ? s.descriptionUr : s.descriptionEn
  const dirty = s.kind === 2 && text.trim() !== s.value
  const invalid = s.kind === 2 && !(/^\d+$/.test(text.trim()) && Number(text) >= (s.min ?? 0) && Number(text) <= (s.max ?? Number.MAX_SAFE_INTEGER))

  return (
    <li className="settings-row">
      <div className="settings-text">
        <span className="cell-strong" dir="auto">{description}</span>
        <span className="cell-sub num" dir="ltr">{s.key}</span>
      </div>
      <div className="settings-control">
        {s.kind === 1 ? (
          <Switch checked={s.value === 'true'} disabled={save.isPending} label={s.value === 'true' ? t('settings.on') : t('settings.off')} onChange={(v) => save.mutate(v ? 'true' : 'false')} />
        ) : (
          <>
            <input className="input" type="number" dir="ltr" style={{ width: 110 }} min={s.min ?? undefined} max={s.max ?? undefined} aria-label={s.key} value={text} onChange={(e) => setText(e.target.value)} />
            <Button size="sm" variant="primary" disabled={!dirty || invalid} loading={save.isPending} onClick={() => save.mutate(text.trim())}>{t('common.save')}</Button>
            {s.min !== null && s.max !== null && <span className="field-hint">{t('settings.range', { min: s.min, max: s.max })}</span>}
          </>
        )}
        {s.isDefault ? <Badge>{t('settings.default')}</Badge> : (
          <Button size="sm" variant="ghost" icon={<RotateCcw size={14} />} disabled={save.isPending} onClick={() => save.mutate(s.defaultValue)}>{t('settings.reset', { value: s.kind === 1 ? t(s.defaultValue === 'true' ? 'settings.on' : 'settings.off') : s.defaultValue })}</Button>
        )}
      </div>
    </li>
  )
}
