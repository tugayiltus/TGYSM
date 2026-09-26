import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';

import { ROUTES } from '../../shared/routes';
import { WORKSPACE_QUEUES, useTickets, type TicketQueue } from '../tickets/api';
import { TicketList } from '../tickets/TicketList';

const QUEUE_PARAM = 'queue';
const DEFAULT_QUEUE: TicketQueue = 'team';
const COUNT_PAGE_SIZE = 1;
const FIRST_PAGE = 1;
const CARD_CLASS = 'block rounded-2xl border border-slate-200 bg-white p-5 shadow-sm hover:border-brand-300';

const queuePath = (queue: TicketQueue) => `${ROUTES.workspaceQueue}?${QUEUE_PARAM}=${queue}`;

function QueueCard({ queue }: { queue: TicketQueue }) {
  const { t } = useTranslation('tickets');
  const count = useTickets({ queue, status: undefined, kind: undefined, priority: undefined, search: '', page: FIRST_PAGE, pageSize: COUNT_PAGE_SIZE });
  return (
    <Link to={queuePath(queue)} className={CARD_CLASS}>
      <span className="block font-medium">{t(`workspace.${queue}`)}</span>
      <span className="mt-1 block text-2xl font-semibold text-brand-900">{count.data ? t('workspace.count', { count: count.data.totalCount }) : '…'}</span>
    </Link>
  );
}

/** IT home: one card per queue, each opening the filtered list. */
export function WorkspacePage() {
  const { t } = useTranslation('tickets');
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('workspace.title')}</h1>
      <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
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
