import { useQuery } from '@tanstack/react-query'
import { api } from '../../lib/api'

/* Typed contracts for /api/users, /api/roles and /api/organization (see Lims.Application DTOs). */

export interface Paged<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface UserListItem {
  id: number
  username: string
  displayNameEn: string
  displayNameUr: string | null
  designation: string | null
  email: string | null
  phone: string | null
  isActive: boolean
  isLocked: boolean
  lastLoginAtUtc: string | null
  allBranches: boolean
  roles: string[]
  branchCount: number
}

export interface UserDetail {
  id: number
  username: string
  displayNameEn: string
  displayNameUr: string | null
  designation: string | null
  email: string | null
  phone: string | null
  isActive: boolean
  isLocked: boolean
  lockoutEndUtc: string | null
  mustChangePassword: boolean
  lastLoginAtUtc: string | null
  preferredLanguage: 'en' | 'ur'
  allBranches: boolean
  defaultBranchId: number | null
  roleIds: number[]
  branchIds: number[]
  createdAtUtc: string
  rowVersion: string
  registrationNo: string | null
  hasSignature: boolean
}

export interface UserEdit {
  displayNameEn: string
  displayNameUr: string | null
  designation: string | null
  registrationNo: string | null
  email: string | null
  phone: string | null
  preferredLanguage: 'en' | 'ur'
  allBranches: boolean
  defaultBranchId: number | null
  roleIds: number[]
  branchIds: number[]
}

export interface LoginHistoryItem {
  id: number
  atUtc: string
  succeeded: boolean
  failureReason: string | null
  ipAddress: string | null
  userAgent: string | null
}

export interface RoleOption {
  id: number
  code: string
  nameEn: string
  nameUr: string | null
}

export interface RoleItem extends RoleOption {
  description: string | null
  isSystem: boolean
  isActive: boolean
  userCount: number
  permissions: string[]
  rowVersion: string
}

export interface PermissionItem {
  id: number
  key: string
  module: string
  descriptionEn: string
  descriptionUr: string | null
}

export interface OrganizationDto {
  id: number
  code: string
  nameEn: string
  nameUr: string | null
  phone: string | null
  email: string | null
  addressEn: string | null
  addressUr: string | null
  rowVersion: string
}

export interface BranchDto {
  id: number
  code: string
  nameEn: string
  nameUr: string | null
  phone: string | null
  email: string | null
  addressEn: string | null
  addressUr: string | null
  isMain: boolean
  isActive: boolean
  userCount: number
  rowVersion: string
}

export interface DepartmentDto {
  id: number
  code: string
  nameEn: string
  nameUr: string | null
  sortOrder: number
  isActive: boolean
  rowVersion: string
}

export const keys = {
  users: (params: object) => ['admin', 'users', params] as const,
  user: (id: number) => ['admin', 'user', id] as const,
  loginHistory: (id: number) => ['admin', 'user', id, 'logins'] as const,
  roles: ['admin', 'roles'] as const,
  roleOptions: ['admin', 'roleOptions'] as const,
  permissions: ['admin', 'permissions'] as const,
  organization: ['admin', 'organization'] as const,
  branches: ['admin', 'branches'] as const,
  departments: ['admin', 'departments'] as const,
}

export const useRoleOptions = (enabled = true) =>
  useQuery({ queryKey: keys.roleOptions, queryFn: () => api<RoleOption[]>('/api/roles/options'), enabled, staleTime: 60_000 })

export const useBranches = (enabled = true) =>
  useQuery({ queryKey: keys.branches, queryFn: () => api<BranchDto[]>('/api/organization/branches'), enabled, staleTime: 60_000 })

export const useDepartments = () =>
  useQuery({ queryKey: keys.departments, queryFn: () => api<DepartmentDto[]>('/api/organization/departments') })

/** Name in the active language (Urdu falls back to English when not entered). */
export function localName(x: { nameEn: string; nameUr: string | null }, lang: string) {
  return lang === 'ur' && x.nameUr ? x.nameUr : x.nameEn
}

/** Empty strings become null so optional fields are cleared rather than stored as "". */
export function nullIfBlank(s: string | null | undefined) {
  return s && s.trim() ? s.trim() : null
}
