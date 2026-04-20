import type { UserInfo } from '../types/api';

const ingestRoles = new Set(['Admin', 'PropertyManager', 'SecurityPersonnel']);

export function isAdmin(user: UserInfo | null): boolean {
  return user?.role === 'Admin';
}

export function canIngestReports(user: UserInfo | null): boolean {
  return user !== null && ingestRoles.has(user.role);
}

export function formatRoleLabel(role: string | undefined): string {
  switch (role) {
    case 'BoardMember':
      return 'Board Member';
    case 'PropertyManager':
      return 'Property Manager';
    case 'SecurityPersonnel':
      return 'Security Personnel';
    default:
      return role ?? '';
  }
}