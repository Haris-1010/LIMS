import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, ChevronsUpDown, SearchX } from 'lucide-react'
import { Skeleton } from './Skeleton'
import { EmptyState } from './EmptyState'

export interface Column<T> {
  key: string
  header: string
  render: (row: T) => ReactNode
  /** Server sort key; omit for unsortable columns. */
  sortKey?: string
  align?: 'start' | 'end' | 'center'
  width?: number | string
}

export interface SortState {
  sort?: string
  desc?: boolean
}

interface DataTableProps<T> {
  columns: Column<T>[]
  rows: T[] | undefined
  rowKey: (row: T) => string | number
  loading?: boolean
  sort?: SortState
  onSort?: (s: SortState) => void
  onRowClick?: (row: T) => void
  empty?: ReactNode
  page?: number
  pageSize?: number
  total?: number
  onPage?: (page: number) => void
  summary?: ReactNode
}

/** Standard table: sortable headers, skeleton rows while loading, empty state, and server pagination. */
export function DataTable<T>({
  columns,
  rows,
  rowKey,
  loading,
  sort,
  onSort,
  onRowClick,
  empty,
  page,
  pageSize,
  total,
  onPage,
  summary,
}: DataTableProps<T>) {
  const { t } = useTranslation()

  return (
    <>
      <div className="table-wrap">
        <table className="table">
          <thead>
            <tr>
              {columns.map((c) => {
                const active = sort?.sort === c.sortKey
                const ariaSort = !c.sortKey ? undefined : active ? (sort?.desc ? 'descending' : 'ascending') : 'none'
                return (
                  <th key={c.key} style={{ width: c.width, textAlign: c.align ?? 'start' }} aria-sort={ariaSort}>
                    {c.sortKey && onSort ? (
                      <button
                        type="button"
                        className={`th-sort${active ? ' is-active' : ''}`}
                        onClick={() => onSort({ sort: c.sortKey, desc: active ? !sort?.desc : false })}
                        aria-label={t('table.sortBy', { column: c.header })}
                      >
                        {c.header}
                        {active ? sort?.desc ? <ArrowDown size={12} /> : <ArrowUp size={12} /> : <ChevronsUpDown size={12} />}
                      </button>
                    ) : (
                      c.header
                    )}
                  </th>
                )
              })}
            </tr>
          </thead>
          <tbody>
            {loading && !rows
              ? Array.from({ length: 6 }, (_, i) => (
                  <tr key={i}>
                    {columns.map((c, j) => (
                      <td key={c.key}>
                        <Skeleton height={14} width={j === 0 ? '70%' : '50%'} />
                      </td>
                    ))}
                  </tr>
                ))
              : rows?.map((row) => (
                  <tr
                    key={rowKey(row)}
                    className={onRowClick ? 'is-clickable' : undefined}
                    onClick={onRowClick ? (e) => !(e.target as HTMLElement).closest('button, a, input') && onRowClick(row) : undefined}
                  >
                    {columns.map((c) => (
                      <td key={c.key} style={{ textAlign: c.align ?? 'start' }}>
                        {c.render(row)}
                      </td>
                    ))}
                  </tr>
                ))}
          </tbody>
        </table>
        {!loading && rows?.length === 0 &&
          (empty ?? <EmptyState icon={<SearchX size={22} />} title={t('table.noResults')} text={t('table.noResultsText')} />)}
      </div>
      {summary}

      {onPage && page && pageSize && total !== undefined && total > 0 && (
        <div className="pagination">
          <span className="pagination-info num">
            {t('table.showing', { from: (page - 1) * pageSize + 1, to: Math.min(page * pageSize, total), total })}
          </span>
          <div className="pagination-buttons">
            <button type="button" className="btn btn-secondary btn-sm btn-icon" disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label={t('table.previous')}>
              <ChevronLeft size={16} className="flip-rtl" />
            </button>
            <span className="pagination-page num" aria-current="page">
              {page} / {Math.max(1, Math.ceil(total / pageSize))}
            </span>
            <button type="button" className="btn btn-secondary btn-sm btn-icon" disabled={page * pageSize >= total} onClick={() => onPage(page + 1)} aria-label={t('table.next')}>
              <ChevronRight size={16} className="flip-rtl" />
            </button>
          </div>
        </div>
      )}
    </>
  )
}
