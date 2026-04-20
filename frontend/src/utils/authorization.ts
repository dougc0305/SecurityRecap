import type { UserInfo } from '../types/api';

const ingestRoles = new Set(['Admin', 'Manager']);

export function isAdmin(user: UserInfo | null): boolean {
  return user?.role === 'Admin';
}

export function canIngestReports(user: UserInfo | null): boolean {
  return user !== null && ingestRoles.has(user.role);
}

export function formatRoleLabel(role: string | undefined): string {
  switch (role) {
    case 'Admin':
      return 'Admin';
    case 'Manager':
      return 'Manager';
    case 'Viewer':
      return 'Viewer';
    default:
      return role ?? '';
  }
}

export const ASSIGNABLE_ROLES = ['Admin', 'Manager', 'Viewer'] as const;
export type AssignableRole = (typeof ASSIGNABLE_ROLES)[number];
