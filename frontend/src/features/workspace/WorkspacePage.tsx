import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';

import { ROUTES } from '../../shared/routes';
import { WORKSPACE_QUEUES, type TicketQueue } from '../tickets/api';
import { TicketList } from '../tickets/TicketList';

import { QueueCard } from './QueueCard';
import { QUEUE_PARAM } from './queuePath';

const DEFAULT_QUEUE: TicketQueue = 'team';

/** IT home: one card per queue, each opening the filtered list. */
export function WorkspacePage() {
  const { t } = useTranslation('tickets');
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('workspace.title')}</h1>
      <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
        {WORKSPACE_QUEUES.map((queue) => (
          <li key={queue}>
            <QueueCard queue={queue} />
          </li>
        ))}
      </ul>
    </section>
  );
}

export function QueuePage() {
  const { t } = useTranslation('tickets');
  const [params] = useSearchParams();
  const requested = params.get(QUEUE_PARAM);
  const queue = WORKSPACE_QUEUES.find((q) => q === requested) ?? DEFAULT_QUEUE;
  return (
    <section className="flex flex-col gap-4">
      <Link to={ROUTES.workspace} className="text-sm text-brand-900 underline">
        {t('workspace.back')}
      </Link>
      <h1 className="text-2xl font-semibold">{t(`workspace.${queue}`)}</h1>
      <TicketList key={queue} queue={queue} showWorkColumns />
    </section>
  );
}
