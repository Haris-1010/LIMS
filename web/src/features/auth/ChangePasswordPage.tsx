import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate } from 'react-router-dom'
import { AlertCircle, Info, LogOut, ShieldCheck } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { PasswordField } from '../../components/ui/PasswordField'
import { passwordProblem } from '../../lib/password'
import { useToast } from '../../components/ui/Toast'
import { useSession } from '../../app/session'
import { errorMessage } from '../../lib/errors'
import { AuthLayout } from './AuthLayout'

/** Shown after a reset or first sign-in (required), and from the account menu (optional). */
export function ChangePasswordPage() {
  const { t } = useTranslation()
  const { user, loading, changePassword, signOut } = useSession()
  const toast = useToast()
  const navigate = useNavigate()
  const [failure, setFailure] = useState<string | null>(null)

  const schema = z
    .object({
      current: z.string().min(1, 'login.errors.passwordRequired'),
      next: z.string(),
      confirm: z.string(),
    })
    .superRefine((v, ctx) => {
      const problem = passwordProblem(v.next, user?.username)
      if (problem) ctx.addIssue({ code: 'custom', path: ['next'], message: `errors.${problem}` })
      if (v.next !== v.confirm) ctx.addIssue({ code: 'custom', path: ['confirm'], message: 'password.mismatch' })
    })
  type Form = z.infer<typeof schema>

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<Form>({ resolver: zodResolver(schema), defaultValues: { current: '', next: '', confirm: '' } })

  if (loading) return null
  if (!user) return <Navigate to="/login" replace />
  const required = user.mustChangePassword

  const onSubmit = async (v: Form) => {
    setFailure(null)
    try {
      await changePassword(v.current, v.next)
      toast.show({ tone: 'success', title: t('password.changed'), text: t('password.changedText') })
      navigate('/', { replace: true })
    } catch (e) {
      setFailure(errorMessage(t, e))
    }
  }

  return (
    <AuthLayout>
      <h1>{t('password.title')}</h1>
      <p className="lead">{required ? t('password.firstTimeLead') : t('password.lead')}</p>

      {failure && (
        <div className="banner tone-danger" role="alert" style={{ marginBottom: 20 }}>
          <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
          <span>{failure}</span>
        </div>
      )}

      <form className="login-form" onSubmit={handleSubmit(onSubmit)} noValidate>
        {/* Hidden username helps password managers save the right account. */}
        <input type="text" name="username" autoComplete="username" value={user.username} readOnly hidden />
        <PasswordField label={t('password.current')} autoComplete="current-password" autoFocus required error={errors.current && t(errors.current.message!)} {...register('current')} />
        <PasswordField label={t('password.new')} autoComplete="new-password" required error={errors.next && t(errors.next.message!)} {...register('next')} />
        <PasswordField label={t('password.confirm')} autoComplete="new-password" required error={errors.confirm && t(errors.confirm.message!)} {...register('confirm')} />

        <div className="banner">
          <Info size={16} aria-hidden style={{ flexShrink: 0 }} />
          <span>{t('password.rules')}</span>
        </div>

        <Button type="submit" variant="primary" size="lg" block loading={isSubmitting}>
          {t('password.submit')}
        </Button>
        {required ? (
          <Button variant="ghost" block icon={<LogOut size={16} className="flip-rtl" />} onClick={() => void signOut().then(() => navigate('/login', { replace: true }))}>
            {t('header.signOut')}
          </Button>
        ) : (
          <Button variant="ghost" block onClick={() => navigate(-1)}>
            {t('common.cancel')}
          </Button>
        )}
      </form>

      <p className="login-secure">
        <ShieldCheck size={14} aria-hidden />
        {t('login.secure')}
      </p>
    </AuthLayout>
  )
}
