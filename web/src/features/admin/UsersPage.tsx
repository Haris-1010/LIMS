import { useState } from 'react'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { KeyRound, LockOpen, MoreHorizontal, Pencil, Plus, Users as UsersIcon } from 'lucide-react'
import { api } from '../../lib/api'
import { errorMessage } from '../../lib/errors'
import { formatDateTime, initials } from '../../lib/format'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { SearchInput } from '../../components/ui/Controls'
import { DataTable, type Column, type SortState } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Menu } from '../../components/ui/Menu'
import { useToast } from '../../components/ui/Toast'
import { keys, localName, useRoleOptions, type Paged, type UserListItem } from './adminApi'
import { UserFormModal } from './UserFormModal'
import { ResetPasswordModal } from './ResetPasswordModal'

const PAGE_SIZE = 20

export function UsersPage() {
  const { t, i18n } = useTranslation()
  const qc = useQueryClient()
  const toast = useToast()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<'all' | 'active' | 'inactive'>('all')
  const [roleId, setRoleId] = useState<string>('')
  const [sort, setSort] = useState<SortState>({ sort: 'name' })
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<number | 'new' | null>(null)
  const [resetting, setResetting] = useState<UserListItem | null>(null)

  const params = { search, isActive: status === 'all' ? undefined : status === 'active', roleId: roleId || undefined, sort: sort.sort, desc: sort.desc, page, pageSize: PAGE_SIZE }
  const { data, isLoading, isFetching } = useQuery({
    queryKey: keys.users(params),
    queryFn: () => api<Paged<UserListItem>>('/api/users', { query: params }),
    placeholderData: keepPreviousData,
  })
  const roles = useRoleOptions()

  const filtered = !!search || status !== 'all' || !!roleId
  const resetFilters = () => {
    setSearch('')
    setStatus('all')
    setRoleId('')
    setPage(1)
  }

  const unlock = async (u: UserListItem) => {
    try {
      await api(`/api/users/${u.id}/unlock`, { method: 'POST' })
      toast.show({ tone: 'success', title: t('users.unlocked') })
      await qc.invalidateQueries({ queryKey: ['admin', 'users'] })
    } catch (e) {
      toast.show({ tone: 'danger', title: t('errors.title'), text: errorMessage(t, e) })
    }
  }

  const columns: Column<UserListItem>[] = [
    {
      key: 'name',
      header: t('users.user'),
      sortKey: 'name',
      render: (u) => (
        <div className="cell-user">
          <span className="avatar avatar-sm" aria-hidden>
            {initials(u.displayNameEn)}
          </span>
          <span>
            <span className="cell-strong" dir="auto">
              {i18n.language === 'ur' && u.displayNameUr ? u.displayNameUr : u.displayNameEn}
            </span>
            <span className="cell-sub num" style={{ textAlign: 'start' }}>
              {u.username}
              {u.designation ? ` · ${u.designation}` : ''}
            </span>
          </span>
        </div>
      ),
    },
    {
      key: 'roles',
      header: t('users.roles'),
      render: (u) => (
        <span className="badge-list">
          {u.roles.map((r) => (
            <Badge key={r} tone="primary">
              {r}
            </Badge>
          ))}
        </span>
      ),
    },
    {
      key: 'branches',
      header: t('users.branches'),
      render: (u) => (u.allBranches ? <Badge tone="accent">{t('users.allBranches')}</Badge> : t('users.branchCount', { count: u.branchCount })),
    },
    {
      key: 'status',
      header: t('common.status'),
      render: (u) =>
        !u.isActive ? (
          <Badge dot>{t('common.inactive')}</Badge>
        ) : u.isLocked ? (
          <Badge tone="danger" dot>
            {t('users.locked')}
          </Badge>
        ) : (
          <Badge tone="success" dot>
            {t('common.active')}
          </Badge>
        ),
    },
    {
      key: 'lastLogin',
      header: t('users.lastLogin'),
      sortKey: 'lastLogin',
      render: (u) => (u.lastLoginAtUtc ? formatDateTime(u.lastLoginAtUtc, i18n.language) : <span style={{ color: 'var(--subtle)' }}>{t('common.never')}</span>),
    },
    {
      key: 'actions',
      header: '',
      align: 'end',
      width: 56,
      render: (u) => (
        <Menu
          label={t('common.actions')}
          trigger={
            <span className="icon-btn">
              <MoreHorizontal size={18} />
            </span>
          }
          items={[
            { key: 'edit', label: t('common.edit'), icon: <Pencil size={15} />, onSelect: () => setEditing(u.id) },
            { key: 'reset', label: t('users.resetPassword'), icon: <KeyRound size={15} />, onSelect: () => setResetting(u) },
            ...(u.isLocked ? [{ key: 'unlock', label: t('users.unlock'), icon: <LockOpen size={15} />, onSelect: () => void unlock(u) }] : []),
          ]}
        />
      ),
    },
  ]

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <h2>{t('users.title')}</h2>
          <p>{t('users.subtitle')}</p>
        </div>
        <Button variant="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>
          {t('users.new')}
        </Button>
      </div>

      <section className="card">
        <div className="card-toolbar">
          <SearchInput value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder={t('users.searchPlaceholder')} />
          <select className="select select-sm" value={status} onChange={(e) => { setStatus(e.target.value as typeof status); setPage(1) }} aria-label={t('common.status')}>
            <option value="all">{t('common.all')}</option>
            <option value="active">{t('common.active')}</option>
            <option value="inactive">{t('common.inactive')}</option>
          </select>
          <select className="select select-sm" value={roleId} onChange={(e) => { setRoleId(e.target.value); setPage(1) }} aria-label={t('users.roles')}>
            <option value="">{t('users.anyRole')}</option>
            {roles.data?.map((r) => (
              <option key={r.id} value={r.id}>
                {localName(r, i18n.language)}
              </option>
            ))}
          </select>
          {filtered && (
            <Button variant="ghost" size="sm" onClick={resetFilters}>
              {t('table.clearFilters')}
            </Button>
          )}
          {isFetching && !isLoading && <span className="spinner" style={{ color: 'var(--primary-500)', marginInlineStart: 'auto' }} aria-label={t('common.loading')} />}
        </div>

        <DataTable
          columns={columns}
          rows={data?.items}
          rowKey={(u) => u.id}
          loading={isLoading}
          sort={sort}
          onSort={(s) => { setSort(s); setPage(1) }}
          onRowClick={(u) => setEditing(u.id)}
          page={page}
          pageSize={PAGE_SIZE}
          total={data?.total}
          onPage={setPage}
          empty={
            filtered ? undefined : (
              <EmptyState
                icon={<UsersIcon size={22} />}
                title={t('users.emptyTitle')}
                text={t('users.emptyText')}
                action={<Button variant="primary" icon={<Plus size={16} />} onClick={() => setEditing('new')}>{t('users.new')}</Button>}
              />
            )
          }
        />
      </section>

      {editing !== null && <UserFormModal userId={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      {resetting && <ResetPasswordModal user={resetting} onClose={() => setResetting(null)} />}
    </div>
  )
}
