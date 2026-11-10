import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, ArrowRightLeft, FlaskConical, MoreHorizontal, Pencil } from 'lucide-react'
import { api, ApiError } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatNumber } from '../../lib/format'
import { useSession } from '../../app/session'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { EmptyState } from '../../components/ui/EmptyState'
import { Menu, type MenuItem } from '../../components/ui/Menu'
import { Skeleton } from '../../components/ui/Skeleton'
import { ItemFormModal, MovementModal, ProfileModal, TransferModal } from './InventoryParts'
import { expiryTone, inventoryKeys, itemName, type Batch, type ItemDetail, type ManualMovement } from './inventoryApi'

/** One stock item: its batches per branch (with expiry), the tests that use it, and the actions on a batch. */
export function StockItemPage() {
  const { t, i18n } = useTranslation()
  const { id } = useParams()
  const itemId = Number(id)
  const { can } = useSession()
  const [editing, setEditing] = useState(false)
  const [profile, setProfile] = useState(false)
  const [move, setMove] = useState<{ batch: Batch; kind: ManualMovement } | null>(null)
  const [transfer, setTransfer] = useState<Batch | null>(null)
  const item = useQuery({ queryKey: inventoryKeys.item(itemId), queryFn: () => api<ItemDetail>(`/api/inventory/items/${itemId}`), enabled: Number.isFinite(itemId) })

  if (item.isLoading) return <div className="page"><Skeleton height={200} /></div>
  if (!item.data) {
    return <div className="page"><section className="card"><EmptyState icon={<FlaskConical size={22} />} title={t('common.notFound')} text={item.error instanceof ApiError && item.error.status === 404 ? t('errors.not_found') : errorMessage(t, item.error)} /></section></div>
  }
  const d = item.data
  const total = d.batches.reduce((s, b) => s + b.quantityOnHand, 0)
  const canAdjust = can('inventory.adjust')

  const actions = (b: Batch): MenuItem[] => [
    { key: 'issue', label: t('inventory.manual1'), onSelect: () => setMove({ batch: b, kind: 1 }) },
    { key: 'in', label: t('inventory.manual2'), onSelect: () => setMove({ batch: b, kind: 2 }) },
    { key: 'out', label: t('inventory.manual3'), onSelect: () => setMove({ batch: b, kind: 3 }) },
    { key: 'off', label: t('inventory.manual4'), danger: true, onSelect: () => setMove({ batch: b, kind: 4 }) },
    { key: 'transfer', label: t('inventory.transfer'), icon: <ArrowRightLeft size={14} />, onSelect: () => setTransfer(b) },
  ]

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <Link to="/inventory/items" className="back-link"><ArrowLeft size={14} className="flip-rtl" /> {t('inventory.backToItems')}</Link>
          <h2 dir="auto">{itemName(d, i18n.language)} <span className="num field-hint">{d.code}</span> {!d.isActive && <Badge>{t('common.inactive')}</Badge>}</h2>
          <p>{t(`inventory.kind${d.kind}`)} · <span className="num">{formatNumber(total)}</span> {d.unitName} {t('inventory.inStock')}{d.reorderLevel > 0 && <> · {t('inventory.reorderLevel')}: <span className="num">{formatNumber(d.reorderLevel)}</span></>}</p>
        </div>
        {can('inventory.manage') && <Button icon={<Pencil size={16} />} onClick={() => setEditing(true)}>{t('common.edit')}</Button>}
      </div>

      <section className="card">
        <div className="card-head"><h3>{t('inventory.batches')}</h3></div>
        {d.batches.length === 0 ? <EmptyState icon={<FlaskConical size={22} />} title={t('inventory.noBatchesTitle')} text={t('inventory.noBatchesText')} /> : (
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>{t('inventory.batchNo')}</th><th>{t('common.branch')}</th><th>{t('inventory.expiryDate')}</th><th style={{ textAlign: 'end' }}>{t('inventory.onHand')}</th><th style={{ textAlign: 'end' }}>{t('inventory.unitCost')}</th><th /></tr></thead>
              <tbody>
                {d.batches.map((b) => (
                  <tr key={b.id}>
                    <td className="num cell-strong">{b.batchNo === 'UNBATCHED' ? t('inventory.unbatched') : b.batchNo}</td>
                    <td>{b.branchName}</td>
                    <td>{b.expiryDate ? <span><span className="num">{b.expiryDate}</span> {b.expiry !== 0 && <Badge tone={expiryTone(b.expiry)}>{t(b.expiry === 2 ? 'inventory.expired' : 'inventory.expiringSoon')}</Badge>}</span> : '—'}</td>
                    <td className="num" style={{ textAlign: 'end' }}>{formatNumber(b.quantityOnHand)} {d.unitName}</td>
                    <td className="num" style={{ textAlign: 'end' }}>{b.unitCost === null ? '—' : formatNumber(b.unitCost)}</td>
                    <td style={{ textAlign: 'end' }}>{canAdjust && <Menu label={t('common.actions')} items={actions(b)} trigger={<MoreHorizontal size={16} />} />}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="card">
        <div className="card-head">
          <h3>{t('inventory.profileTitle')}</h3>
          {can('inventory.manage') && <Button size="sm" onClick={() => setProfile(true)}>{t('inventory.editProfile')}</Button>}
        </div>
        <p className="field-hint" style={{ padding: '0 16px' }}>{t('inventory.profileCardHint')}</p>
        {d.profile.length === 0 ? <p className="field-hint" style={{ padding: 16 }}>{t('inventory.noProfile')}</p> : (
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>{t('inventory.test')}</th><th style={{ textAlign: 'end' }}>{t('inventory.perTest', { unit: d.unitName })}</th></tr></thead>
              <tbody>{d.profile.map((p) => <tr key={p.testId}><td><span dir="auto">{i18n.language === 'ur' && p.testNameUr ? p.testNameUr : p.testNameEn}</span><span className="cell-sub num">{p.testCode}</span></td><td className="num" style={{ textAlign: 'end' }}>{formatNumber(p.quantityPerTest)}</td></tr>)}</tbody>
            </table>
          </div>
        )}
      </section>

      {editing && <ItemFormModal item={d} onClose={() => setEditing(false)} />}
      {profile && <ProfileModal item={d} onClose={() => setProfile(false)} />}
      {move && <MovementModal batch={move.batch} item={d} kind={move.kind} onClose={() => setMove(null)} />}
      {transfer && <TransferModal batch={transfer} item={d} onClose={() => setTransfer(null)} />}
    </div>
  )
}
