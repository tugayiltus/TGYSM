import { ROUTES } from '../../shared/routes';
import type { TicketQueue } from '../tickets/api';

export const QUEUE_PARAM = 'queue';

export const queuePath = (queue: TicketQueue) => `${ROUTES.workspaceQueue}?${QUEUE_PARAM}=${queue}`;

/** Queue value for the staff ticket list without a queue filter: every ticket the viewer may see. */
export const ALL_QUEUE = 'all';

export const allTicketsPath = (search: string) => `${ROUTES.workspaceQueue}?${new URLSearchParams({ [QUEUE_PARAM]: ALL_QUEUE, q: search }).toString()}`;
