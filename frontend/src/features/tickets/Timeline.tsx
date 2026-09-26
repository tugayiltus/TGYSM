import { useTranslation } from 'react-i18next';

import { formatDateTime } from '../../shared/lib/format';
import { EmptyState } from '../../shared/ui/States';

import type { TimelineItem } from './api';

export function Timeline({ events }: { events: readonly TimelineItem[] }) {
  const { t } = useTranslation('tickets');
  if (events.length === 0) {
    return <EmptyState />;
  }
  return (
    <ol className="flex flex-col gap-2 border-l-2 border-slate-200 pl-4">
      {events.map((e) => (
        <li key={e.id} className="relative">
          <span aria-hidden="true" className="absolute top-2 -left-[1.4rem] size-2.5 rounded-full bg-slate-400" />
          <p className="text-sm">
            <span className="font-medium">{t(`events.${e.kind}`)}</span>
            {e.visibility === 'internal' && <span className="ml-2 rounded bg-amber-100 px-1.5 text-xs text-amber-900">{t('detail.internal')}</span>}
          </p>
          <p className="text-xs text-slate-600">
            <time dateTime={e.occurredAt}>{formatDateTime(e.occurredAt)}</time>
            {e.actorName && ` · ${e.actorName}`}
          </p>
        </li>
      ))}
    </ol>
  );
}
