import { useQuery } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { adminKeys } from '../shared/queryKeys';

export type WorkerHeartbeatItem = components['schemas']['WorkerHeartbeatItem'];

/** Heartbeats move every job interval (30 s by default); re-read at the same pace so a stale job shows without a reload. */
const WORKERS_REFRESH_MS = 30_000;

export function useWorkerHeartbeats() {
  return useQuery({
    queryKey: [...adminKeys.system, 'workers'],
    queryFn: () => unwrap(api.GET('/api/v1/admin/system/workers')),
    refetchInterval: WORKERS_REFRESH_MS,
  });
}
