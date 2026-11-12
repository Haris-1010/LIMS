import { useEffect } from 'react'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
import { formatDateTime, formatMoney } from '../../lib/format'
import { useSession } from '../../app/session'
import { localName } from '../catalog/catalogApi'
import { patientName } from '../patients/patientsApi'
import type { OrderDetail, PaymentDto } from './ordersApi'

/**
 * Printable payment receipt (80 mm thermal width, also fine on A5). It is rendered into <body> and
 * printed immediately; the rest of the app is hidden by the print stylesheet. Amounts come from the
 * server's invoice, never recalculated here.
 */
export function Receipt({ order, payment, onDone }: { order: OrderDetail; payment: PaymentDto; onDone: () => void }) {
  const { t, i18n } = useTranslation()
  const { user } = useSession()
  const inv = order.invoice

  useEffect(() => {
    document.body.classList.add('printing-receipt')
    const logo = document.querySelector<HTMLImageElement>('.receipt-logo')
    let timer: number | undefined
    const print = () => {
      if (timer !== undefined) window.clearTimeout(timer)
      window.print()
    }
    const done = () => {
      document.body.classList.remove('printing-receipt')
      onDone()
    }
    window.addEventListener('afterprint', done, { once: true })
    if (logo && !logo.complete) {
      logo.addEventListener('load', print, { once: true })
      logo.addEventListener('error', print, { once: true })
      timer = window.setTimeout(print, 1000)
    } else {
      timer = window.setTimeout(print, 150)
    }
    return () => {
      if (timer !== undefined) window.clearTimeout(timer)
      logo?.removeEventListener('load', print)
      logo?.removeEventListener('error', print)
      window.removeEventListener('afterprint', done)
      document.body.classList.remove('printing-receipt')
    }
  }, [onDone])

  // Balance as it stood right after this payment (payments and refunds before and including it).
  const upTo = inv.payments.filter((p) => p.id <= payment.id).reduce((s, p) => s + p.amount, 0)
  const lines = inv.lines.filter((l) => !l.isCancelled)

  return createPortal(
    <div className="receipt" dir={i18n.dir()} lang={i18n.language}>
      <header>
        <img className="receipt-logo" src="/api/report-template/logo" alt="" onError={(e) => { e.currentTarget.style.display = 'none' }} />
        <strong>{i18n.language === 'ur' && user?.organizationNameUr ? user.organizationNameUr : user?.organizationNameEn}</strong>
        <span>{localName({ nameEn: order.branchName, nameUr: order.branchNameUr }, i18n.language)}</span>
        <span className="receipt-title">{t('orders.receiptTitle')}</span>
      </header>
      <dl>
        <div><dt>{t('payments.receiptNo')}</dt><dd className="num">{payment.receiptNo}</dd></div>
        <div><dt>{t('orders.orderNo')}</dt><dd className="num">{order.orderNo}</dd></div>
        <div><dt>{t('orders.invoice')}</dt><dd className="num">{inv.invoiceNo}</dd></div>
        <div><dt>{t('orders.patient')}</dt><dd dir="auto">{patientName(order.patient, i18n.language)}</dd></div>
        <div><dt>{t('patients.patientNo')}</dt><dd className="num">{order.patient.patientNo}</dd></div>
        <div><dt>{t('orders.date')}</dt><dd>{formatDateTime(payment.receivedAtUtc, i18n.language)}</dd></div>
      </dl>
      <table>
        <tbody>
          {lines.map((l) => (
            <tr key={l.id}><td dir="auto">{localName(l, i18n.language)}</td><td className="num">{formatMoney(l.price)}</td></tr>
          ))}
        </tbody>
        <tfoot>
          <tr><td>{t('orders.gross')}</td><td className="num">{formatMoney(inv.gross)}</td></tr>
          {inv.discountAmount > 0 && <tr><td>{t('orders.discount')}</td><td className="num">− {formatMoney(inv.discountAmount)}</td></tr>}
          <tr className="strong"><td>{t('orders.net')}</td><td className="num">{formatMoney(inv.net)}</td></tr>
        </tfoot>
      </table>
      <div className="receipt-paid">
        <span>{t(`paymentMethod.${payment.method}`)}{payment.reference ? ` · ${payment.reference}` : ''}</span>
        <strong className="num">{formatMoney(payment.amount)}</strong>
      </div>
      <div className="receipt-balance"><span>{t('orders.balanceDue')}</span><span className="num">{formatMoney(Math.max(inv.net - upTo, 0))}</span></div>
      <footer>
        {payment.receivedByName && <span>{t('orders.receivedBy')}: {payment.receivedByName}</span>}
        <span className="receipt-footer-instruction">{t('orders.receiptFooterInstruction')}</span>
        <span className="receipt-footer-generated">{t('orders.systemGenerated')}</span>
      </footer>
    </div>,
    document.body,
  )
}
