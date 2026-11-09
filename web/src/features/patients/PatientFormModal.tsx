import { useEffect, useMemo, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { AlertCircle, AlertTriangle, CreditCard, EyeOff, Phone, Trash2 } from 'lucide-react'
import { api, ApiError } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { Button } from '../../components/ui/Button'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { FormSection, SelectField, TextAreaField } from '../../components/ui/Controls'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/Toast'
import { ageParts, formatAge, formatPhone, type DuplicateCandidate, type PatientDetail, type Sex } from './patientsApi'
import './patients.css'

interface Form {
  title: string
  fullName: string
  fullNameUr: string
  guardianName: string
  sex: Sex | ''
  ageMode: 'dob' | 'age'
  dateOfBirth: string
  ageYears: string
  ageMonths: string
  ageDays: string
  cnic: string
  mobile: string
  altPhone: string
  email: string
  address: string
  city: string
  notes: string
}

function toForm(p: PatientDetail | null): Form {
  return {
    title: p?.title ?? '',
    fullName: p?.fullName ?? '',
    fullNameUr: p?.fullNameUr ?? '',
    guardianName: p?.guardianName ?? '',
    sex: p ? p.sex : '',
    // Estimated birth dates are edited as an age; exact ones as a date.
    ageMode: p?.dateOfBirth && !p.isDateOfBirthEstimated ? 'dob' : p ? 'age' : 'dob',
    dateOfBirth: p?.dateOfBirth ?? '',
    ageYears: '',
    ageMonths: '',
    ageDays: '',
    cnic: p?.sensitiveVisible ? (p.cnic ?? '') : '',
    mobile: p?.mobile ?? '',
    altPhone: p?.altPhone ?? '',
    email: p?.sensitiveVisible ? (p.email ?? '') : '',
    address: p?.sensitiveVisible ? (p.address ?? '') : '',
    city: p?.city ?? '',
    notes: p?.notes ?? '',
  }
}

const TITLES = ['Mr.', 'Mrs.', 'Ms.', 'Miss', 'Master', 'Baby', 'Dr.']
const blank = (s: string) => (s.trim() ? s.trim() : null)
const int = (s: string) => (s.trim() === '' ? null : Number(s))

export function PatientFormModal({ patient, onClose, onSaved }: { patient: PatientDetail | null; onClose: () => void; onSaved?: (id: number) => void }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const isNew = patient === null
  // Receptionists register identity details but cannot later see or change them.
  const showSensitive = isNew || (patient?.sensitiveVisible ?? false)

  const initial = useMemo(() => {
    const f = toForm(patient)
    if (patient?.dateOfBirth && patient.isDateOfBirthEstimated) {
      // Show the stored estimate as today's age.
      const { y, m, d } = ageParts(patient.dateOfBirth)
      Object.assign(f, { ageYears: String(y), ageMonths: m ? String(m) : '', ageDays: d ? String(d) : '' })
    }
    return f
  }, [patient])
  const [f, setF] = useState<Form>(initial)
  const [errors, setErrors] = useState<Partial<Record<keyof Form | 'age', string>>>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [dupes, setDupes] = useState<DuplicateCandidate[]>([])
  const [mustConfirm, setMustConfirm] = useState(false)
  const [saving, setSaving] = useState(false)
  const [confirmDiscard, setConfirmDiscard] = useState(false)

  const dirty = JSON.stringify(f) !== JSON.stringify(initial)
  const set = <K extends keyof Form>(k: K, v: Form[K]) => {
    setF((x) => ({ ...x, [k]: v }))
    setErrors((e) => ({ ...e, [k]: undefined, ...(k.startsWith('age') || k === 'dateOfBirth' ? { age: undefined } : {}) }))
  }

  // Live duplicate check while registering (debounced).
  useEffect(() => {
    if (!isNew) return
    const name = f.fullName.trim()
    const mobileDigits = f.mobile.replace(/\D/g, '')
    const cnicDigits = f.cnic.replace(/\D/g, '')
    if (name.length < 3 && mobileDigits.length < 10 && cnicDigits.length !== 13) {
      setDupes([])
      return
    }
    const h = window.setTimeout(() => {
      api<DuplicateCandidate[]>('/api/patients/duplicates', {
        query: {
          fullName: name || undefined,
          mobile: mobileDigits.length >= 10 ? f.mobile : undefined,
          cnic: cnicDigits.length === 13 ? f.cnic : undefined,
          dateOfBirth: f.ageMode === 'dob' && f.dateOfBirth ? f.dateOfBirth : undefined,
        },
      })
        .then(setDupes)
        .catch(() => setDupes([]))
    }, 450)
    return () => window.clearTimeout(h)
  }, [isNew, f.fullName, f.mobile, f.cnic, f.dateOfBirth, f.ageMode])

  const validate = () => {
    const e: typeof errors = {}
    if (!f.fullName.trim()) e.fullName = t('errors.name_required')
    if (f.sex === '') e.sex = t('common.required')
    if (f.ageMode === 'dob') {
      if (!f.dateOfBirth) e.age = t('errors.age_required')
      else if (f.dateOfBirth > new Date().toISOString().slice(0, 10)) e.age = t('errors.dob_in_future')
    } else if (!f.ageYears && !f.ageMonths && !f.ageDays) e.age = t('errors.age_required')
    if (f.mobile && f.mobile.replace(/\D/g, '').length < 10) e.mobile = t('errors.mobile_invalid')
    if (showSensitive && f.cnic && f.cnic.replace(/\D/g, '').length !== 13) e.cnic = t('errors.cnic_invalid')
    if (showSensitive && f.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.email)) e.email = t('common.invalidEmail')
    setErrors(e)
    return Object.keys(e).length === 0
  }

  const save = async (confirmNotDuplicate = false) => {
    setFailure(null)
    if (!validate()) return
    setSaving(true)
    const body = {
      title: blank(f.title),
      fullName: f.fullName.trim(),
      fullNameUr: blank(f.fullNameUr),
      guardianName: blank(f.guardianName),
      sex: f.sex,
      dateOfBirth: f.ageMode === 'dob' ? f.dateOfBirth : null,
      ageYears: f.ageMode === 'age' ? int(f.ageYears) : null,
      ageMonths: f.ageMode === 'age' ? int(f.ageMonths) : null,
      ageDays: f.ageMode === 'age' ? int(f.ageDays) : null,
      mobile: blank(f.mobile),
      altPhone: blank(f.altPhone),
      city: blank(f.city),
      notes: blank(f.notes),
      ...(showSensitive ? { cnic: blank(f.cnic), email: blank(f.email), address: blank(f.address) } : {}),
    }
    try {
      const saved = isNew
        ? await api<PatientDetail>('/api/patients', { method: 'POST', body: { ...body, confirmNotDuplicate } })
        : await api<PatientDetail>(`/api/patients/${patient.id}`, { method: 'PUT', body: { ...body, rowVersion: patient.rowVersion } })
      toast.show(
        isNew
          ? { tone: 'success', title: t('patients.registeredToast'), text: t('patients.registeredText', { no: saved.patientNo }) }
          : { tone: 'success', title: t('common.saved') },
      )
      await qc.invalidateQueries({ queryKey: ['patients'] })
      onClose()
      onSaved?.(saved.id)
    } catch (e) {
      if (e instanceof ApiError && e.code === 'possible_duplicate') {
        setDupes((e.data.details as DuplicateCandidate[]) ?? [])
        setMustConfirm(true)
      } else {
        setFailure(errorMessage(t, e))
      }
    } finally {
      setSaving(false)
    }
  }

  const requestClose = () => (dirty ? setConfirmDiscard(true) : onClose())
  const ageNow = f.ageMode === 'dob' && f.dateOfBirth && f.dateOfBirth <= new Date().toISOString().slice(0, 10) ? formatAge(t, f.dateOfBirth) : null

  return (
    <>
      <Modal
        open
        size="lg"
        onClose={requestClose}
        title={isNew ? t('patients.createTitle') : t('patients.editTitle')}
        subtitle={patient ? `${patient.patientNo}` : undefined}
        footer={
          <>
            <Button onClick={requestClose}>{t('common.cancel')}</Button>
            {mustConfirm ? (
              <Button variant="primary" loading={saving} onClick={() => void save(true)}>
                {t('patients.registerAnyway')}
              </Button>
            ) : (
              <Button variant="primary" loading={saving} disabled={!isNew && !dirty} onClick={() => void save()}>
                {isNew ? t('patients.register') : t('common.save')}
              </Button>
            )}
          </>
        }
      >
        {failure && (
          <div className="banner tone-danger" role="alert" style={{ marginBottom: 16 }}>
            <AlertCircle size={16} aria-hidden style={{ flexShrink: 0 }} />
            <span>{failure}</span>
          </div>
        )}

        {isNew && dupes.length > 0 && (
          <div className={`dup-panel${mustConfirm ? ' is-blocking' : ''}`} role={mustConfirm ? 'alert' : 'status'}>
            <div className="dup-head">
              <AlertTriangle size={16} aria-hidden />
              <div>
                <strong>{t('patients.dupTitle')}</strong>
                <p>{t('patients.dupText')}</p>
              </div>
            </div>
            <ul>
              {dupes.map((d) => (
                <li key={d.id}>
                  <span className="num cell-strong">{d.patientNo}</span>
                  <span>
                    <span className="cell-strong" dir="auto">{d.fullName}</span>
                    {d.guardianName && <span className="cell-sub" dir="auto">{d.guardianName}</span>}
                  </span>
                  <span>{formatAge(t, d.dateOfBirth)} · {t(`sex.short${d.sex}`)}</span>
                  <span className="num">{formatPhone(d.mobile)}</span>
                  <span className="dup-reasons">{d.reasons.map((r) => t(`patients.dupReason.${r}`)).join(' · ')}</span>
                  <Link className="btn btn-secondary btn-sm" to={`/patients/${d.id}`} onClick={onClose}>
                    {t('patients.useExisting')}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        )}

        <form onSubmit={(e) => { e.preventDefault(); void save(mustConfirm) }} noValidate>
          <FormSection title={t('patients.sectionIdentity')}>
            <div className="form-grid patient-grid">
              <SelectField label={t('patients.titleLabel')} value={f.title} onChange={(e) => set('title', e.target.value)}>
                <option value="">—</option>
                {TITLES.map((x) => <option key={x} value={x}>{x}</option>)}
              </SelectField>
              <Field label={t('patients.fullName')} required autoFocus value={f.fullName} error={errors.fullName} maxLength={200} onChange={(e) => set('fullName', e.target.value)} />
              <Field label={t('patients.guardian')} value={f.guardianName} maxLength={200} onChange={(e) => set('guardianName', e.target.value)} />
              <Field label={t('patients.fullNameUr')} dir="rtl" lang="ur" className="urdu-input" value={f.fullNameUr} maxLength={200} onChange={(e) => set('fullNameUr', e.target.value)} />
            </div>
            <div className="field" style={{ marginTop: 16 }}>
              <span className="field-label">{t('patients.sexLabel')}<span className="req" aria-hidden>*</span></span>
              <div className="segmented" role="radiogroup" aria-label={t('patients.sexLabel')}>
                {([1, 2, 3] as Sex[]).map((s) => (
                  <button key={s} type="button" role="radio" aria-checked={f.sex === s} aria-pressed={f.sex === s} onClick={() => set('sex', s)}>
                    {t(`sex.${s}`)}
                  </button>
                ))}
              </div>
              {errors.sex && <span className="field-error" role="alert">{errors.sex}</span>}
            </div>
          </FormSection>

          <FormSection title={t('patients.sectionAge')}>
            <div className="field" style={{ marginBottom: 12 }}>
              <span className="field-label">{t('patients.ageMode')}</span>
              <div className="segmented" role="radiogroup" aria-label={t('patients.ageMode')}>
                {(['dob', 'age'] as const).map((m) => (
                  <button key={m} type="button" role="radio" aria-checked={f.ageMode === m} aria-pressed={f.ageMode === m} onClick={() => set('ageMode', m)}>
                    {m === 'dob' ? t('patients.byDob') : t('patients.byAge')}
                  </button>
                ))}
              </div>
            </div>
            {f.ageMode === 'dob' ? (
              <div className="form-grid">
                <Field label={t('patients.dob')} type="date" dir="ltr" required max={new Date().toISOString().slice(0, 10)} value={f.dateOfBirth} error={errors.age} hint={ageNow ? t('patients.ageNow', { age: ageNow }) : t('patients.ageHint')} onChange={(e) => set('dateOfBirth', e.target.value)} />
              </div>
            ) : (
              <>
                <div className="age-grid">
                  <Field label={t('patients.years')} type="number" inputMode="numeric" min={0} max={130} dir="ltr" value={f.ageYears} onChange={(e) => set('ageYears', e.target.value)} />
                  <Field label={t('patients.months')} type="number" inputMode="numeric" min={0} max={11} dir="ltr" value={f.ageMonths} onChange={(e) => set('ageMonths', e.target.value)} />
                  <Field label={t('patients.days')} type="number" inputMode="numeric" min={0} max={31} dir="ltr" value={f.ageDays} onChange={(e) => set('ageDays', e.target.value)} />
                </div>
                {errors.age ? <span className="field-error" role="alert">{errors.age}</span> : <span className="field-hint">{t('patients.ageHint')}</span>}
              </>
            )}
          </FormSection>

          <FormSection title={t('patients.sectionContact')}>
            <div className="form-grid">
              <Field label={t('patients.mobile')} type="tel" dir="ltr" icon={<Phone size={16} />} hint={t('patients.mobileHint')} value={f.mobile} error={errors.mobile} maxLength={20} onChange={(e) => set('mobile', e.target.value)} />
              <Field label={t('patients.altPhone')} type="tel" dir="ltr" value={f.altPhone} maxLength={20} onChange={(e) => set('altPhone', e.target.value)} />
              {showSensitive ? (
                <>
                  <Field label={t('patients.cnic')} dir="ltr" inputMode="numeric" icon={<CreditCard size={16} />} hint={t('patients.cnicHint')} value={f.cnic} error={errors.cnic} maxLength={20} onChange={(e) => set('cnic', e.target.value)} />
                  <Field label={t('common.email')} type="email" dir="ltr" value={f.email} error={errors.email} maxLength={256} onChange={(e) => set('email', e.target.value)} />
                  <Field label={t('patients.address')} value={f.address} maxLength={500} onChange={(e) => set('address', e.target.value)} />
                </>
              ) : (
                <div className="span-2 banner">
                  <EyeOff size={16} aria-hidden style={{ flexShrink: 0 }} />
                  <span>{t('patients.hiddenSensitive')}</span>
                </div>
              )}
              <Field label={t('patients.city')} value={f.city} maxLength={100} onChange={(e) => set('city', e.target.value)} />
              <div className="span-2">
                <TextAreaField label={t('patients.notes')} rows={2} value={f.notes} maxLength={1000} onChange={(e) => set('notes', e.target.value)} />
              </div>
            </div>
          </FormSection>
          <button type="submit" hidden aria-hidden tabIndex={-1} />
        </form>
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
