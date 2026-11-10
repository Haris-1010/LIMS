import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../../components/ui/Button'
import { Field } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { SampleLabel } from './labels'
import { DEFAULT_LABEL, loadLabelSettings, saveLabelSettings, type LabelSettings } from './labelSettings'
import type { SampleListItem } from './samplesApi'

/** Per-workstation label size and copies, with a live preview at the chosen size. */
export function LabelSettingsModal({ onClose, sample }: { onClose: () => void; sample?: SampleListItem }) {
  const { t } = useTranslation()
  const [s, setS] = useState<LabelSettings>(loadLabelSettings)
  const set = (k: keyof LabelSettings) => (e: React.ChangeEvent<HTMLInputElement>) => setS({ ...s, [k]: Number(e.target.value) })

  const demo: SampleListItem = sample ?? {
    id: 0, barcode: 'S-261109-0001', barcodeModules: '', status: 0, priority: 0, orderId: 0, orderNo: 'ORD-2026-00001', patientId: 0, patientNo: 'P-000001',
    patientName: 'Sample Patient', patientNameUr: null, patientSex: 1, patientDateOfBirth: '1990-01-01', sampleTypeId: 0, sampleTypeNameEn: 'Serum', sampleTypeNameUr: null,
    capColour: 'Red', container: null, testCodes: ['CBC', 'LFT'], originBranch: '', currentBranch: '', destinationBranch: null,
    createdAtUtc: new Date().toISOString(), collectedAtUtc: null, isRecollection: false, labelPrintCount: 0,
  }

  return (
    <Modal open title={t('samples.labelSettings')} subtitle={t('samples.labelSettingsHint')} onClose={onClose} size="md"
      footer={<>
        <Button variant="ghost" onClick={() => setS(DEFAULT_LABEL)}>{t('samples.resetLabel')}</Button>
        <Button variant="primary" onClick={() => { saveLabelSettings(s); onClose() }}>{t('common.save')}</Button>
      </>}>
      <div className="form-stack">
        <div className="form-grid">
          <Field label={t('samples.labelWidth')} type="number" min={30} max={120} value={s.widthMm} onChange={set('widthMm')} />
          <Field label={t('samples.labelHeight')} type="number" min={15} max={100} value={s.heightMm} onChange={set('heightMm')} />
          <Field label={t('samples.labelCopies')} type="number" min={1} max={10} value={s.copies} onChange={set('copies')} />
        </div>
        <div className="label-preview" aria-label={t('samples.preview')}>
          <SampleLabel sample={demo} settings={{ widthMm: Math.min(120, Math.max(30, s.widthMm || 50)), heightMm: Math.min(100, Math.max(15, s.heightMm || 25)), copies: 1 }} />
        </div>
        <p className="field-hint">{t('samples.labelPrinterHint')}</p>
      </div>
    </Modal>
  )
}
