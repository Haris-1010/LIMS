import { useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, AtSign, Mail, Phone, RefreshCw, Trash2 } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage, fieldErrors } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { CheckList, FormSection, SelectField, Switch, Tabs } from '../../components/ui/Controls'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { PasswordField } from '../../components/ui/PasswordField'
import { generatePassword, passwordProblem } from '../../lib/password'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import { keys, localName, nullIfBlank, useBranches, useRoleOptions, type LoginHistoryItem, type UserDetail } from './adminApi'

interface FormState {
  username: string
  password: string
  displayNameEn: string
  displayNameUr: string
  designation: string
  email: string
  phone: string
  preferredLanguage: 'en' | 'ur'
  allBranches: boolean
  defaultBranchId: string
  roleIds: number[]
  branchIds: number[]
  isActive: boolean
}

const empty: FormState = {
  username: '',
  password: '',
  displayNameEn: '',
  displayNameUr: '',
  designation: '',
  email: '',
  phone: '',
  preferredLanguage: 'en',
  allBranches: false,
  defaultBranchId: '',
  roleIds: [],
  branchIds: [],
  isActive: true,
}

function fromDetail(u: UserDetail): FormState {
  return {
    username: u.username,
    password: '',
    displayNameEn: u.displayNameEn,
    displayNameUr: u.displayNameUr ?? '',
    designation: u.designation ?? '',
    email: u.email ?? '',
    phone: u.phone ?? '',
    preferredLanguage: u.preferredLanguage,
    allBranches: u.allBranches,
    defaultBranchId: u.defaultBranchId?.toString() ?? '',
    roleIds: u.roleIds,
    branchIds: u.branchIds,
    isActive: u.isActive,
  }
}

export function UserFormModal({ userId, onClose }: { userId: number | null; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const { user: me } = useSession()
  const isNew = userId === null
  const [tab, setTab] = useState<'details' | 'history'>('details')

  const detail = useQuery({ queryKey: keys.user(userId ?? 0), queryFn: () => api<UserDetail>(`/api/users/${userId}`), enabled: !isNew })
  const roles = useRoleOptions()
  const branches = useBranches()

  const initial = useMemo(() => (detail.data ? fromDetail(detail.data) : { ...empty, password: generatePassword() }), [detail.data])
  const [form, setForm] = useState<FormState>(initial)
  useEffect(() => setForm(initial), [initial])

  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [confirmDiscard, setConfirmDiscard] = useState(false)

  const dirty = JSON.stringify(form) !== JSON.stringify(initial)
  const set = <K extends keyof FormState>(k: K, v: FormState[K]) => {
    setForm((f) => ({ ...f, [k]: v }))
    setErrors((e) => ({ ...e, [k]: '' }))
  }
  const requestClose = () => (dirty ? setConfirmDiscard(true) : onClose())

  const validate = (): boolean => {
    const e: Record<string, string> = {}
    if (!form.displayNameEn.trim()) e.displayNameEn = t('common.required')
    if (isNew) {
      if (!/^[a-z0-9][a-z0-9._-]{2,63}$/.test(form.username.trim().toLowerCase())) e.username = t('errors.username_invalid')
      const p = passwordProblem(form.password, form.username)
      if (p) e.password = t(`errors.${p}`)
    }
    if (form.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) e.email = t('common.invalidEmail')
    if (form.roleIds.length === 0) e.roleIds = t('errors.role_required')
    if (!form.allBranches && form.branchIds.length === 0) e.branchIds = t('errors.branch_required')
    setErrors(e)
    return Object.keys(e).length === 0
  }

  const save = async () => {
    setFailure(null)
    if (!validate()) return
    setSaving(true)
    const body = {
      displayNameEn: form.displayNameEn.trim(),
      displayNameUr: nullIfBlank(form.displayNameUr),
      designation: nullIfBlank(form.designation),
      email: nullIfBlank(form.email),
      phone: nullIfBlank(form.phone),
      preferredLanguage: form.preferredLanguage,
      allBranches: form.allBranches,
      defaultBranchId: form.defaultBranchId ? Number(form.defaultBranchId) : null,
      roleIds: form.roleIds,
      branchIds: form.allBranches ? [] : form.branchIds,
    }
    try {
      if (isNew) {
        await api('/api/users', { method: 'POST', body: { ...body, username: form.username.trim().toLowerCase(), password: form.password } })
        toast.show({ tone: 'success', title: t('common.created'), text: t('users.createdText', { name: body.displayNameEn }) })
      } else {
        await api(`/api/users/${userId}`, { method: 'PUT', body: { ...body, isActive: form.isActive, rowVersion: detail.data!.rowVersion } })
        toast.show({ tone: 'success', title: t('common.saved') })
      }
      await qc.invalidateQueries({ queryKey: ['admin'] })
      onClose()
    } catch (e) {
      setFailure(errorMessage(t, e))
      setErrors((prev) => ({ ...prev, ...fieldErrors(e) }))
    } finally {
      setSaving(false)
    }
  }

  const loading = !isNew && detail.isLoading
  const activeBranches = branches.data?.filter((b) => b.isActive) ?? []
  const defaultOptions = form.allBranches ? activeBranches : activeBranches.filter((b) => form.branchIds.includes(b.id))
  const isSelf = me?.userId === userId

  return (
    <>
      <Modal
        open
        size="lg"
        onClose={requestClose}
        title={isNew ? t('users.createTitle') : t('users.editTitle')}
        subtitle={!isNew && detail.data ? `${detail.data.username} · ${t('users.lastLogin')}: ${detail.data.lastLoginAtUtc ? formatDateTime(detail.data.lastLoginAtUtc, i18n.language) : t('common.never')}` : undefined}
        toolbar={
          !isNew && (
            <Tabs
              value={tab}
              onChange={setTab}
              tabs={[
                { key: 'details', label: t('users.tabDetails') },
                { key: 'history', label: t('users.tabHistory') },
              ]}
            />
          )
        }
        footer={
          tab === 'details' && (
            <>
              <Button onClick={requestClose}>{t('common.cancel')}</Button>
              <Button variant="primary" loading={saving} disabled={loading || (!isNew && !dirty)} onClick={() => void save()}>
                {saving ? t('common.saving') : isNew ? t('common.create') : t('common.save')}
              </Button>
            </>
          )
        }
      >
        {failure && (
          <div className="banner tone-danger" role="alert" style={{ marginBottom: 20 }}>
            <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
            <span>{failure}</span>
          </div>
        )}

        {tab === 'history' && userId !== null ? (
          <LoginHistory userId={userId} />
        ) : loading ? (
          <div className="form-stack">
            {Array.from({ length: 6 }, (_, i) => (
              <Skeleton key={i} height={40} />
            ))}
          </div>
        ) : (
          <form
            onSubmit={(e) => {
              e.preventDefault()
              void save()
            }}
            noValidate
          >
            <FormSection title={t('users.sectionProfile')}>
              <div className="form-grid">
                <Field label={t('users.displayNameEn')} required value={form.displayNameEn} error={errors.displayNameEn} onChange={(e) => set('displayNameEn', e.target.value)} maxLength={200} />
                <Field label={t('users.displayNameUr')} value={form.displayNameUr} dir="rtl" lang="ur" className="urdu-input" onChange={(e) => set('displayNameUr', e.target.value)} maxLength={200} />
                <Field label={t('users.designation')} hint={t('users.designationHint')} value={form.designation} onChange={(e) => set('designation', e.target.value)} maxLength={200} />
                <SelectField label={t('users.language')} value={form.preferredLanguage} onChange={(e) => set('preferredLanguage', e.target.value as 'en' | 'ur')}>
                  <option value="en">English</option>
                  <option value="ur">اردو</option>
                </SelectField>
                <Field label={t('common.email')} type="email" dir="ltr" icon={<Mail size={16} />} value={form.email} error={errors.email} onChange={(e) => set('email', e.target.value)} maxLength={256} />
                <Field label={t('common.phone')} type="tel" dir="ltr" icon={<Phone size={16} />} value={form.phone} onChange={(e) => set('phone', e.target.value)} maxLength={30} />
              </div>
            </FormSection>

            {isNew && (
              <FormSection title={t('users.sectionAccount')}>
                <div className="form-grid">
                  <Field
                    label={t('users.username')}
                    hint={t('users.usernameHint')}
                    required
                    dir="ltr"
                    autoComplete="off"
                    autoCapitalize="off"
                    spellCheck={false}
                    icon={<AtSign size={16} />}
                    value={form.username}
                    error={errors.username}
                    onChange={(e) => set('username', e.target.value.toLowerCase())}
                    maxLength={64}
                  />
                  <div className="field-with-action">
                    <PasswordField
                      label={t('users.initialPassword')}
                      hint={t('users.initialPasswordHint')}
                      required
                      dir="ltr"
                      autoComplete="new-password"
                      value={form.password}
                      error={errors.password}
                      onChange={(e) => set('password', e.target.value)}
                    />
                    <Button size="sm" variant="ghost" icon={<RefreshCw size={14} />} onClick={() => set('password', generatePassword())}>
                      {t('password.generate')}
                    </Button>
                  </div>
                </div>
              </FormSection>
            )}

            <FormSection title={t('users.sectionAccess')}>
              <div className="form-stack">
                <div className="field">
                  <span className="field-label">
                    {t('users.roles')}
                    <span className="req" aria-hidden>*</span>
                  </span>
                  <span className="field-hint">{t('users.rolesHint')}</span>
                  {roles.isLoading ? (
                    <Skeleton height={80} />
                  ) : (
                    <CheckList
                      columns={3}
                      items={roles.data?.map((r) => ({ id: r.id, label: localName(r, i18n.language) })) ?? []}
                      selected={form.roleIds}
                      onChange={(v) => set('roleIds', v as number[])}
                    />
                  )}
                  {errors.roleIds && <span className="field-error" role="alert">{errors.roleIds}</span>}
                </div>

                <Switch checked={form.allBranches} onChange={(v) => set('allBranches', v)} label={t('users.allBranchesLabel')} hint={t('users.allBranchesHint')} />

                {!form.allBranches && (
                  <div className="field">
                    <span className="field-label">
                      {t('users.branchAccess')}
                      <span className="req" aria-hidden>*</span>
                    </span>
                    <CheckList
                      columns={2}
                      items={activeBranches.map((b) => ({ id: b.id, label: localName(b, i18n.language), sub: b.isMain ? t('org.main') : b.code }))}
                      selected={form.branchIds}
                      onChange={(v) => {
                        set('branchIds', v as number[])
                        if (form.defaultBranchId && !v.includes(Number(form.defaultBranchId))) set('defaultBranchId', '')
                      }}
                    />
                    {errors.branchIds && <span className="field-error" role="alert">{errors.branchIds}</span>}
                  </div>
                )}

                <div style={{ maxWidth: 360 }}>
                  <SelectField label={t('users.defaultBranch')} value={form.defaultBranchId} onChange={(e) => set('defaultBranchId', e.target.value)}>
                    <option value="">{t('users.noDefault')}</option>
                    {defaultOptions.map((b) => (
                      <option key={b.id} value={b.id}>
                        {localName(b, i18n.language)}
                      </option>
                    ))}
                  </SelectField>
                </div>

                {!isNew && (
                  <Switch
                    checked={form.isActive}
                    disabled={isSelf}
                    onChange={(v) => set('isActive', v)}
                    label={t('users.activeLabel')}
                    hint={isSelf ? t('errors.cannot_deactivate_self') : t('users.activeHint')}
                  />
                )}
                {!isNew && detail.data?.mustChangePassword && <Badge tone="warning">{t('users.mustChange')}</Badge>}
              </div>
            </FormSection>
            <button type="submit" hidden aria-hidden tabIndex={-1} />
          </form>
        )}
      </Modal>

      <ConfirmDialog
        open={confirmDiscard}
        tone="danger"
        icon={<Trash2 size={20} />}
        title={t('common.unsaved')}
        text={t('common.unsavedText')}
        confirmLabel={t('common.discard')}
        onCancel={() => setConfirmDiscard(false)}
        onConfirm={onClose}
      />
    </>
  )
}

function LoginHistory({ userId }: { userId: number }) {
  const { t, i18n } = useTranslation()
  const { data, isLoading } = useQuery({
    queryKey: keys.loginHistory(userId),
    queryFn: () => api<LoginHistoryItem[]>(`/api/users/${userId}/login-history`),
  })
  if (isLoading) return <Skeleton height={200} />
  if (!data?.length) return <p className="field-hint">{t('users.historyEmpty')}</p>
  return (
    <div className="table-wrap" style={{ border: '1px solid var(--border)', borderRadius: 8 }}>
      <table className="table">
        <thead>
          <tr>
            <th>{t('users.historyWhen')}</th>
            <th>{t('users.historyResult')}</th>
            <th>{t('users.historyAddress')}</th>
          </tr>
        </thead>
        <tbody>
          {data.map((h) => (
            <tr key={h.id}>
              <td>{formatDateTime(h.atUtc, i18n.language)}</td>
              <td>
                {h.succeeded ? (
                  <Badge tone="success" dot>{t('users.historySuccess')}</Badge>
                ) : (
                  <Badge tone="danger" dot>{t(`users.failure.${h.failureReason}`, { defaultValue: h.failureReason ?? '' })}</Badge>
                )}
              </td>
              <td className="num">{h.ipAddress ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
