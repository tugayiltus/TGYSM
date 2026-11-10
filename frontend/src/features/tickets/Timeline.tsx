/**
 * @input The events of a ticket.
 * @output The ticket history, newest last, with internal events marked.
 * @position Ticket detail page.
 */
import { HStack } from '@astryxdesign/core/HStack';
import { List, ListItem } from '@astryxdesign/core/List';
import { Token } from '@astryxdesign/core/Token';
import { useTranslation } from 'react-i18next';

import { EmptyState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';

import type { TimelineItem } from './api';

const META_SEPARATOR = ' · ';

export function Timeline({ events }: { events: readonly TimelineItem[] }) {
  const { t } = useTranslation('tickets');
  if (events.length === 0) {
    return <EmptyState />;
  }
  return (
    <List listStyle="decimal" density="compact" aria-label={t('detail.timeline')}>
      {events.map((event) => (
        <ListItem
          key={event.id}
          label={
            <HStack gap={1} vAlign="center" wrap="wrap">
              {t(`events.${event.kind}`)}
              {event.visibility === 'internal' && <Token size="sm" color="yellow" label={t('detail.internal')} />}
            </HStack>
          }
          description={[formatDateTime(event.occurredAt), event.actorName].filter(Boolean).join(META_SEPARATOR)}
        />
      ))}
    </List>
  );
}
