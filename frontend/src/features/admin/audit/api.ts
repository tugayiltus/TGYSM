import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { adminKeys } from '../shared/queryKeys';

export type AuditEventItem = components['schemas']['AuditEventItem'];

export interface AuditListParams {
  entityType: string | undefined;
  page: number;
  pageSize: number;
}

export function useAuditEvents(params: AuditListParams) {
  return useQuery({
    queryKey: [...adminKeys.audit, params],
    queryFn: () =>
      unwrap(api.GET('/api/v1/admin/audit-events', { params: { query: { entityType: params.entityType, page: params.page, pageSize: params.pageSize } } })),
    placeholderData: keepPreviousData,
  });
}
