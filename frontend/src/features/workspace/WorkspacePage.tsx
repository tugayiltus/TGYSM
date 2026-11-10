/**
 * @input `queue` query parameter on the queue page.
 * @output The IT workspace (one card per queue) and the queue page (its ticket list).
 * @position `/workspace` and `/workspace/queue`, inside the app shell.
 */
import { Grid } from '@astryxdesign/core/Grid';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';

import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { WORKSPACE_QUEUES, type TicketQueue } from '../tickets/api';
import { TicketList } from '../tickets/TicketList';

import { QueueCard } from './QueueCard';
import { QUEUE_PARAM } from './queuePath';

const DEFAULT_QUEUE: TicketQueue = 'team';
/** Queue cards reflow from one column on phones to one row on wide screens. */
const QUEUE_COLUMNS = { minWidth: 200 } as const;

export function WorkspacePage() {
  const { t } = useTranslation('tickets');
  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.workspace.icon} titleOverride={t('workspace.title')} />
      <Grid columns={QUEUE_COLUMNS} gap={3}>
        {WORKSPACE_QUEUES.map((queue) => (
          <QueueCard key={queue} queue={queue} />
        ))}
      </Grid>
    </VStack>
  );
}

export function QueuePage() {
  const { t } = useTranslation('tickets');
  const [params] = useSearchParams();
  const requested = params.get(QUEUE_PARAM);
  const queue = WORKSPACE_QUEUES.find((q) => q === requested) ?? DEFAULT_QUEUE;
  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.workspaceQueue.icon} titleOverride={t(`workspace.${queue}`)} />
      <TicketList key={queue} queue={queue} showWorkColumns />
    </VStack>
  );
}
