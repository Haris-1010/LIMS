import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useTranslation } from 'react-i18next'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { AlertCircle, FlaskConical, KeyRound, Languages, ShieldCheck, User, WifiOff } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { Field } from '../../components/ui/Field'
import { PasswordField } from '../../components/ui/PasswordField'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { useToast } from '../../components/ui/Toast'
import { useSession } from '../../app/session'
import { ApiError } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { AuthLayout } from './AuthLayout'

// Messages are translation keys so validation text follows the active language.
const schema = z.object({
  username: z.string().trim().min(1, 'login.errors.usernameRequired'),
  password: z.string().min(1, 'login.errors.passwordRequired'),
  remember: z.boolean(),
})
type LoginForm = z.infer<typeof schema>

export function LoginPage() {
  const { t, i18n } = useTranslation()
  const { user, signIn } = useSession()
  const toast = useToast()
  const navigate = useNavigate()
  const location = useLocation()
  const [forgotOpen, setForgotOpen] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    setFocus,
    resetField,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({ resolver: zodResolver(schema), defaultValues: { username: '', password: '', remember: false } })

  if (user) return <Navigate to={user.mustChangePassword ? '/change-password' : '/'} replace />

  const onSubmit = async (data: LoginForm) => {
    setFailure(null)
    try {
      const me = await signIn(data.username, data.password, data.remember)
      if (me.mustChangePassword) {
        navigate('/change-password', { replace: true })
        return
      }
      toast.show({ tone: 'success', title: t('toast.signedIn'), text: t('toast.signedInText') })
      const from = (location.state as { from?: string } | null)?.from
      navigate(from && from !== '/login' ? from : '/', { replace: true })
    } catch (e) {
      const until = e instanceof ApiError ? e.data.lockedUntilUtc : undefined
      if (e instanceof ApiError && e.code === 'locked_out' && typeof until === 'string') {
        const time = new Date(until).toLocaleTimeString(i18n.language === 'ur' ? 'ur-PK' : 'en-GB', { hour: '2-digit', minute: '2-digit' })
        setFailure(t('login.lockedUntil', { time }))
      } else {
        setFailure(errorMessage(t, e))
      }
      resetField('password')
      setFocus('password')
    }
  }

  return (
    <AuthLayout
      hero={
        <>
          <span className="hero-eyebrow">
            <FlaskConical size={13} aria-hidden />
            {t('login.hero.eyebrow')}
          </span>
          <h2>{t('login.hero.title')}</h2>
          <p>{t('login.hero.text')}</p>
          <div className="hero-features">
            <div className="hero-feature">
              <WifiOff size={18} aria-hidden />
              <strong>{t('login.hero.f1')}</strong>
              <span>{t('login.hero.f1Text')}</span>
            </div>
            <div className="hero-feature">
              <Languages size={18} aria-hidden />
              <strong>{t('login.hero.f2')}</strong>
              <span>{t('login.hero.f2Text')}</span>
            </div>
            <div className="hero-feature">
              <ShieldCheck size={18} aria-hidden />
              <strong>{t('login.hero.f3')}</strong>
              <span>{t('login.hero.f3Text')}</span>
            </div>
          </div>
        </>
      }
    >
      <h1>{t('login.title')}</h1>
      <p className="lead">{t('login.lead')}</p>

      {failure && (
        <div className="banner tone-danger" role="alert" style={{ marginBottom: 20 }}>
          <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
          <span>{failure}</span>
        </div>
      )}

      <form className="login-form" onSubmit={handleSubmit(onSubmit)} noValidate>
        <Field
          label={t('login.username')}
          placeholder={t('login.usernamePlaceholder')}
          autoComplete="username"
          autoCapitalize="off"
          spellCheck={false}
          autoFocus
          required
          dir="ltr"
          icon={<User size={16} />}
          error={errors.username && t(errors.username.message!)}
          {...register('username')}
        />
        <PasswordField
          label={t('login.password')}
          placeholder={t('login.passwordPlaceholder')}
          autoComplete="current-password"
          required
          error={errors.password && t(errors.password.message!)}
          {...register('password')}
        />

        <div className="login-row">
          <label className="checkbox">
            <input type="checkbox" {...register('remember')} />
            {t('login.remember')}
          </label>
          <a
            href="#forgot"
            onClick={(e) => {
              e.preventDefault()
              setForgotOpen(true)
            }}
          >
            {t('login.forgot')}
          </a>
        </div>

        <Button type="submit" variant="primary" size="lg" block loading={isSubmitting}>
          {isSubmitting ? t('login.submitting') : t('login.submit')}
        </Button>
      </form>

      <p className="login-secure">
        <ShieldCheck size={14} aria-hidden />
        {t('login.secure')}
      </p>

      <ConfirmDialog
        open={forgotOpen}
        icon={<KeyRound size={20} />}
        title={t('login.forgotTitle')}
        text={t('login.forgotText')}
        confirmLabel={t('common.close')}
        hideCancel
        onConfirm={() => setForgotOpen(false)}
        onCancel={() => setForgotOpen(false)}
      />
    </AuthLayout>
  )
}
