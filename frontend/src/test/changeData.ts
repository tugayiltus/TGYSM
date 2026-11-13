import type { ChangeDetail, ChangeListItem, ChangeSummary } from '../features/changes/api';

import { ADMIN_ME } from './mockApi';
import { TEAMS } from './taskData';

export const CHANGE: ChangeDetail = {
  id: 'chg-1',
  number: 'CHG-2026-000001',
  title: 'Çekirdek anahtar yazılım güncellemesi',
  justification: 'Güvenlik yaması',
  affectedService: 'Ofis ağı',
  risk: 'high',
  impact: 'medium',
  windowStart: '2026-12-05T21:00:00Z',
  windowEnd: '2026-12-05T23:00:00Z',
  implementationPlan: 'Anahtarı sırayla güncelle',
  verificationPlan: 'Bağlantıları dene',
  backoutPlan: 'Eski imajı yükle',
  ownerTeamId: 'team-1',
  ownerTeamName: 'Servis Masası',
  ownerId: 'u-2',
  ownerName: 'Deniz Yıldız',
  status: 'draft',
  outcome: null,
  actualStart: null,
  actualEnd: null,
  backoutStartedAt: null,
  verificationNote: null,
  backoutNote: null,
  closingNote: null,
  cancelReason: null,
  createdById: 'u-9',
  createdByName: 'Can Demir',
  createdAt: '2026-11-13T08:00:00Z',
  updatedAt: '2026-11-13T08:00:00Z',
  closedAt: null,
  assets: [{ id: 'a-1', assetTag: 'DMR-000001', type: 'laptop', brand: 'Dell', model: 'Latitude', status: 'assigned' }],
  tasksByPhase: [
    {
      phase: 'implementation',
      tasks: [{ id: 'task-1', number: 'TSK-2026-000001', title: 'Anahtarı güncelle', status: 'open', phase: 'implementation', teamId: 'team-2', teamName: 'Saha Destek', assigneeName: null }],
    },
  ],
  approval: null,
  hasEligibleApprover: true,
  canWork: true,
  version: 3,
};

export const CHANGE_SUMMARY: ChangeSummary = {
  id: CHANGE.id,
  number: CHANGE.number,
  title: CHANGE.title,
  status: 'scheduled',
  windowStart: CHANGE.windowStart,
  windowEnd: CHANGE.windowEnd,
  risk: 'high',
  implementationPlan: 'Anahtarı sırayla güncelle',
  backoutPlan: null,
};

export const CHANGE_ROW: ChangeListItem = {
  id: CHANGE.id,
  number: CHANGE.number,
  title: CHANGE.title,
  status: 'draft',
  outcome: null,
  risk: 'high',
  windowStart: CHANGE.windowStart,
  windowEnd: CHANGE.windowEnd,
  ownerTeamName: CHANGE.ownerTeamName,
  ownerName: CHANGE.ownerName,
  updatedAt: CHANGE.updatedAt,
};

export const full = (detail: ChangeDetail) => ({ access: 'full', detail, summary: null });

export function changeRoutes(change: ChangeDetail = CHANGE, extra: Record<string, unknown> = {}) {
  return {
    'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
    [`GET /api/v1/changes/${change.number}`]: { status: 200, body: full(change) },
    [`GET /api/v1/changes/${change.number}/timeline`]: { status: 200, body: [] },
    'GET /api/v1/reference/teams': { status: 200, body: TEAMS },
    'GET /api/v1/assets': { status: 200, body: { items: [], page: 1, pageSize: 20, totalCount: 0 } },
    ...extra,
  };
}
