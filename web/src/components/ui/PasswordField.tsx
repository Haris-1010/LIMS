import { forwardRef, useState, type InputHTMLAttributes } from 'react'
import { useTranslation } from 'react-i18next'
import { Eye, EyeOff, Lock } from 'lucide-react'
import { Field } from './Field'
import { Button } from './Button'

interface PasswordFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label: string
  error?: string
  hint?: string
}

/** Password input with show/hide toggle. */
export const PasswordField = forwardRef<HTMLInputElement, PasswordFieldProps>(function PasswordField(props, ref) {
  const { t } = useTranslation()
  const [show, setShow] = useState(false)
  return (
    <Field
      ref={ref}
      type={show ? 'text' : 'password'}
      icon={<Lock size={16} />}
      spellCheck={false}
      autoCapitalize="off"
      action={
        <Button variant="ghost" size="sm" iconOnly aria-label={show ? t('password.hide') : t('password.show')} aria-pressed={show} onClick={() => setShow((s) => !s)}>
          {show ? <EyeOff size={16} /> : <Eye size={16} />}
        </Button>
      }
      {...props}
    />
  )
})
