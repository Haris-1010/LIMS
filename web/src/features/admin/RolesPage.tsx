import { useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, KeyRound, Lock, Plus, ShieldAlert, Trash2 } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { Switch, TextAreaField } from '../../components/ui/Controls'
import { EmptyState } from '../../components/ui/EmptyState'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import { useToast } from '../../components/ui/Toast'
import { keys, localName, nullIfBlank, type PermissionItem, type RoleItem } from './adminApi'
import './admin.css'

/** Permissions that release or change clinical results; flagged in the editor. */
const CLINICAL = new Set(['results.approve', 'results.amend', 'catalog.clinical'])

export function RolesPage() {
  const { t, i18n } = useTranslation()
  const roles = useQuery({ queryKey: keys.roles, queryFn: () => api<RoleItem[]>('/api/roles') })
  const permissions = useQuery({ queryKey: keys.permissions, queryFn: () => api<PermissionItem[]>('/api/roles/permissions'), staleTime: Infinity })
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [creating, setCreating] = useState(false)

  useEffect(() => {
    if (selectedId === null && roles.data?.length) setSelectedId(roles.data[0]!.id)
  }, [roles.data, selectedId])

  const selected = roles.data?.find((r) => r.id === selectedId)
  const total = permissions.data?.length ?? 0

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <h2>{t('rolesPage.title')}</h2>
          <p>{t('rolesPage.subtitle')}</p>
        </div>
        <Button variant="primary" icon={<Plus size={16} />} onClick={() => setCreating(true)}>
          {t('rolesPage.new')}
        </Button>
      </div>

      <div className="roles-layout">
        <section className="card roles-list" aria-label={t('rolesPage.title')}>
          {roles.isLoading
            ? Array.from({ length: 6 }, (_, i) => (
                <div key={i} className="role-item">
                  <Skeleton height={14} width="60%" />
                  <Skeleton height={10} width="40%" />
                </div>
              ))
            : roles.data?.map((r) => (
                <button
                  key={r.id}
                  type="button"
                  className={`role-item${r.id === selectedId ? ' is-active' : ''}`}
                  aria-current={r.id === selectedId || undefined}
                  onClick={() => setSelectedId(r.id)}
                >
                  <span className="role-item-top">
                    <span className="role-item-name">{localName(r, i18n.language)}</span>
                    {!r.isActive && <Badge>{t('common.inactive')}</Badge>}
                  </span>
                  <span className="role-item-meta">
                    {t('rolesPage.users', { count: r.userCount })} · {t('rolesPage.permissionsCount', { count: r.permissions.length, total })}
                    {r.isSystem && (
                      <span className="role-system">
                        <Lock size={11} aria-hidden /> {t('rolesPage.system')}
                      </span>
                    )}
                  </span>
                </button>
              ))}
        </section>

        <section className="card roles-editor">
          {selected && permissions.data ? (
            <RoleEditor key={`${selected.id}-${selected.rowVersion}`} role={selected} permissions={permissions.data} onDeleted={() => setSelectedId(null)} />
          ) : roles.isLoading || permissions.isLoading ? (
            <div className="card-body form-stack">
              <Skeleton height={40} />
              <Skeleton height={320} />
            </div>
          ) : (
            <EmptyState icon={<KeyRound size={22} />} title={t('rolesPage.title')} text={t('rolesPage.selectRole')} />
          )}
        </section>
      </div>

      {creating && (
        <CreateRoleModal
          onClose={() => setCreating(false)}
          onCreated={(id) => {
            setCreating(false)
            setSelectedId(id)
          }}
        />
      )}
    </div>
  )
}

function RoleEditor({ role, permissions, onDeleted }: { role: RoleItem; permissions: PermissionItem[]; onDeleted: () => void }) {
  const { t, i18n } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const [nameEn, setNameEn] = useState(role.nameEn)
  const [nameUr, setNameUr] = useState(role.nameUr ?? '')
  const [description, setDescription] = useState(role.description ?? '')
  const [isActive, setIsActive] = useState(role.isActive)
  const [granted, setGranted] = useState<Set<string>>(() => new Set(role.permissions))
  const [saving, setSaving] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const [confirmDelete, setConfirmDelete] = useState(false)

  const modules = useMemo(() => {
    const map = new Map<string, PermissionItem[]>()
    for (const p of permissions) map.set(p.module, [...(map.get(p.module) ?? []), p])
    return [...map.entries()]
  }, [permissions])

  const dirty =
    nameEn !== role.nameEn ||
    nameUr !== (role.nameUr ?? '') ||
    description !== (role.description ?? '') ||
    isActive !== role.isActive ||
    granted.size !== role.permissions.length ||
    role.permissions.some((p) => !granted.has(p))

  const toggle = (key: string, on: boolean) =>
    setGranted((g) => {
      const next = new Set(g)
      if (on) next.add(key)
      else next.delete(key)
      return next
    })

  const save = async () => {
    if (!nameEn.trim()) return setFailure(t('common.required'))
    setSaving(true)
    setFailure(null)
    try {
      await api(`/api/roles/${role.id}`, {
        method: 'PUT',
        body: { nameEn: nameEn.trim(), nameUr: nullIfBlank(nameUr), description: nullIfBlank(description), isActive, permissions: [...granted], rowVersion: role.rowVersion },
      })
      toast.show({ tone: 'success', title: t('common.saved') })
      await qc.invalidateQueries({ queryKey: ['admin'] })
      await qc.invalidateQueries({ queryKey: ['session'] })
    } catch (e) {
      setFailure(errorMessage(t, e))
    } finally {
      setSaving(false)
    }
  }

  const remove = async () => {
    try {
      await api(`/api/roles/${role.id}`, { method: 'DELETE' })
      toast.show({ tone: 'success', title: t('rolesPage.deleted') })
      onDeleted()
      await qc.invalidateQueries({ queryKey: ['admin'] })
    } catch (e) {
      setFailure(errorMessage(t, e))
    } finally {
      setConfirmDelete(false)
    }
  }

  const canDelete = !role.isSystem && role.userCount === 0
  const clinicalGranted = [...granted].some((k) => CLINICAL.has(k))

  return (
    <>
      <header className="card-header">
        <div>
          <h3 className="card-title">
            {localName(role, i18n.language)} <span className="role-code num">{role.code}</span>
          </h3>
          <p className="card-subtitle">{t('rolesPage.permissionsCount', { count: granted.size, total: permissions.length })}</p>
        </div>
        <div className="toolbar">
          {canDelete && (
            <Button variant="ghost" icon={<Trash2 size={15} />} onClick={() => setConfirmDelete(true)}>
              {t('common.delete')}
            </Button>
          )}
          <Button variant="primary" loading={saving} disabled={!dirty} onClick={() => void save()}>
            {t('common.save')}
          </Button>
        </div>
      </header>

      <div className="card-body">
        {failure && (
          <div className="banner tone-danger" role="alert" style={{ marginBottom: 16 }}>
            <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
            <span>{failure}</span>
          </div>
        )}

        <div className="form-grid" style={{ marginBottom: 16 }}>
          <Field label={t('common.nameEn')} required value={nameEn} onChange={(e) => setNameEn(e.target.value)} maxLength={200} />
          <Field label={t('common.nameUr')} dir="rtl" lang="ur" className="urdu-input" value={nameUr} onChange={(e) => setNameUr(e.target.value)} maxLength={200} />
          <div className="span-2">
            <TextAreaField label={t('common.description')} rows={2} value={description} onChange={(e) => setDescription(e.target.value)} maxLength={500} />
          </div>
          <div className="span-2">
            <Switch checked={isActive} onChange={setIsActive} label={t('rolesPage.activeLabel')} hint={t('rolesPage.activeHint')} />
          </div>
        </div>

        {clinicalGranted && (
          <div className="banner tone-warning" style={{ marginBottom: 16 }}>
            <ShieldAlert size={16} aria-hidden style={{ flexShrink: 0 }} />
            <span>{t('rolesPage.clinicalNote')}</span>
          </div>
        )}

        <div className="perm-modules">
          {modules.map(([module, items]) => {
            const on = items.filter((p) => granted.has(p.key)).length
            return (
              <fieldset key={module} className="perm-module">
                <legend className="perm-module-head">
                  <span className="perm-module-title">{t(`rolesPage.modules.${module}`, { defaultValue: module })}</span>
                  <span className="perm-module-count num">
                    {on}/{items.length}
                  </span>
                  <button
                    type="button"
                    className="link-btn"
                    onClick={() => setGranted((g) => {
                      const next = new Set(g)
                      items.forEach((p) => (on === items.length ? next.delete(p.key) : next.add(p.key)))
                      return next
                    })}
                  >
                    {on === items.length ? t('rolesPage.clearAll') : t('rolesPage.selectAll')}
                  </button>
                </legend>
                {items.map((p) => (
                  <label key={p.key} className={`perm-row${granted.has(p.key) ? ' is-on' : ''}`}>
                    <input type="checkbox" checked={granted.has(p.key)} onChange={(e) => toggle(p.key, e.target.checked)} />
                    <span>
                      <span className="perm-desc">
                        {i18n.language === 'ur' && p.descriptionUr ? p.descriptionUr : p.descriptionEn}
                        {CLINICAL.has(p.key) && <ShieldAlert size={13} className="perm-clinical" aria-hidden />}
                      </span>
                      <span className="perm-key num">{p.key}</span>
                    </span>
                  </label>
                ))}
              </fieldset>
            )
          })}
        </div>
      </div>

      <ConfirmDialog
        open={confirmDelete}
        tone="danger"
        icon={<Trash2 size={20} />}
        title={t('rolesPage.deleteTitle', { name: localName(role, i18n.language) })}
        text={t('rolesPage.deleteText')}
        confirmLabel={t('common.delete')}
        onCancel={() => setConfirmDelete(false)}
        onConfirm={() => void remove()}
      />
    </>
  )
}

function CreateRoleModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: number) => void }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const [code, setCode] = useState('')
  const [nameEn, setNameEn] = useState('')
  const [nameUr, setNameUr] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const submit = async () => {
    const e: Record<string, string> = {}
    if (!/^[A-Za-z0-9_-]{1,20}$/.test(code)) e.code = t('common.codeHint')
    if (!nameEn.trim()) e.nameEn = t('common.required')
    setErrors(e)
    if (Object.keys(e).length) return
    setSaving(true)
    setFailure(null)
    try {
      const res = await api<{ id: number }>('/api/roles', { method: 'POST', body: { code, nameEn: nameEn.trim(), nameUr: nullIfBlank(nameUr), permissions: [] } })
      toast.show({ tone: 'success', title: t('common.created') })
      await qc.invalidateQueries({ queryKey: ['admin'] })
      onCreated(res.id)
    } catch (err) {
      setFailure(errorMessage(t, err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal
      open
      size="sm"
      onClose={onClose}
      title={t('rolesPage.createTitle')}
      footer={
        <>
          <Button onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={saving} onClick={() => void submit()}>
            {t('common.create')}
          </Button>
        </>
      }
    >
      {failure && (
        <div className="banner tone-danger" role="alert" style={{ marginBottom: 16 }}>
          <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
          <span>{failure}</span>
        </div>
      )}
      <form className="form-stack" onSubmit={(e) => { e.preventDefault(); void submit() }} noValidate>
        <Field label={t('rolesPage.codeLabel')} hint={t('common.codeHint')} required dir="ltr" value={code} error={errors.code} maxLength={20} onChange={(e) => setCode(e.target.value.toUpperCase())} />
        <Field label={t('common.nameEn')} required value={nameEn} error={errors.nameEn} maxLength={200} onChange={(e) => setNameEn(e.target.value)} />
        <Field label={t('common.nameUr')} dir="rtl" lang="ur" className="urdu-input" value={nameUr} maxLength={200} onChange={(e) => setNameUr(e.target.value)} />
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Modal>
  )
}
