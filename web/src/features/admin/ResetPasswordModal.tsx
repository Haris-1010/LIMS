import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, RefreshCw } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Button } from '../../components/ui/Button'
import { Modal } from '../../components/ui/Modal'
import { PasswordField } from '../../components/ui/PasswordField'
import { generatePassword, passwordProblem } from '../../lib/password'
import { useToast } from '../../components/ui/Toast'
import type { UserListItem } from './adminApi'

/** Admin sets a temporary password; the user is signed out everywhere and must change it at next sign-in. */
export function ResetPasswordModal({ user, onClose }: { user: UserListItem; onClose: () => void }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const [password, setPassword] = useState(generatePassword)
  const [error, setError] = useState<string>()
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const submit = async () => {
    const problem = passwordProblem(password, user.username)
    if (problem) return setError(t(`errors.${problem}`))
    setSaving(true)
    setFailure(null)
    try {
      await api(`/api/users/${user.id}/reset-password`, { method: 'POST', body: { newPassword: password } })
      toast.show({ tone: 'success', title: t('users.resetDone'), text: t('users.resetDoneText', { name: user.displayNameEn }) })
      await qc.invalidateQueries({ queryKey: ['admin', 'users'] })
      onClose()
    } catch (e) {
      setFailure(errorMessage(t, e))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal
      open
      size="sm"
      onClose={onClose}
      title={t('users.resetTitle', { name: user.displayNameEn })}
      subtitle={t('users.resetText')}
      footer={
        <>
          <Button onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={saving} onClick={() => void submit()}>
            {t('users.resetPassword')}
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
      <form
        className="form-stack"
        onSubmit={(e) => {
          e.preventDefault()
          void submit()
        }}
      >
        <input type="text" autoComplete="username" value={user.username} readOnly hidden />
        <div className="field-with-action">
          <PasswordField
            label={t('users.initialPassword')}
            hint={t('users.initialPasswordHint')}
            dir="ltr"
            autoComplete="new-password"
            value={password}
            error={error}
            onChange={(e) => {
              setPassword(e.target.value)
              setError(undefined)
            }}
          />
          <Button size="sm" variant="ghost" icon={<RefreshCw size={14} />} onClick={() => setPassword(generatePassword())}>
            {t('password.generate')}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
