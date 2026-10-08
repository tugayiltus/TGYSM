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
    <ol className="list-unstyled mb-0 d-flex flex-column gap-2 tgysm-timeline">
      {events.map((e) => (
        <li key={e.id} className="position-relative">
          <p className="mb-0 small">
            <span className="fw-medium">{t(`events.${e.kind}`)}</span>
            {e.visibility === 'internal' && <span className="badge bg-warning-subtle text-warning-emphasis ms-1">{t('detail.internal')}</span>}
          </p>
          <p className="mb-0 small text-body-secondary">
            <time dateTime={e.occurredAt}>{formatDateTime(e.occurredAt)}</time>
            {e.actorName && ` · ${e.actorName}`}
          </p>
        </li>
      ))}
    </ol>
  );
}
