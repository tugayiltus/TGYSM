import { ROUTES } from '../../shared/routes';
import type { TicketQueue } from '../tickets/api';

export const QUEUE_PARAM = 'queue';

export const queuePath = (queue: TicketQueue) => `${ROUTES.workspaceQueue}?${QUEUE_PARAM}=${queue}`;
