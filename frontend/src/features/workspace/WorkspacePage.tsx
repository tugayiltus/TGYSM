import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';

import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { WORKSPACE_QUEUES, type TicketQueue } from '../tickets/api';
import { TicketList } from '../tickets/TicketList';

import { QueueCard } from './QueueCard';
import { QUEUE_PARAM } from './queuePath';

const DEFAULT_QUEUE: TicketQueue = 'team';

/** IT home: one card per queue, each opening the filtered list. */
export function WorkspacePage() {
  const { t } = useTranslation('tickets');
  return (
    <>
      <PageHeader icon={ROUTE_META.workspace.icon} titleOverride={t('workspace.title')} />
      <ul className="list-unstyled row g-3 mb-0">
        {WORKSPACE_QUEUES.map((queue) => (
          <li key={queue} className="col-12 col-sm-6 col-xl">
            <QueueCard queue={queue} />
          </li>
        ))}
      </ul>
    </>
  );
}

export function QueuePage() {
  const { t } = useTranslation('tickets');
  const [params] = useSearchParams();
  const requested = params.get(QUEUE_PARAM);
  const queue = WORKSPACE_QUEUES.find((q) => q === requested) ?? DEFAULT_QUEUE;
  return (
    <>
      <PageHeader icon={ROUTE_META.workspaceQueue.icon} titleOverride={t(`workspace.${queue}`)} />
      <TicketList key={queue} queue={queue} showWorkColumns />
    </>
  );
}
