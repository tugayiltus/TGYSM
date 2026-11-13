/**
 * @input A change number.
 * @output The change history, newest last, with the reason or note an event carries.
 * @position Change detail page, Zaman çizgisi section (full access only).
 */
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';

import { useChangeTimeline, type ChangeTimelineItem } from './api';

const META_SEPARATOR = ' · ';
const TEXT_KEYS = ['reason', 'note'] as const;

/** The reason or note an event carries (rejections, returns, cancellations); other events carry no text. */
function noteOf(event: ChangeTimelineItem): string | null {
  if (typeof event.data !== 'object' || event.data === null) {
    return null;
  }

  const data = event.data as Record<string, unknown>;
  for (const key of TEXT_KEYS) {
    const value = data[key];
    if (typeof value === 'string' && value !== '') {
      return value;
    }
  }

  return null;
}

export function ChangeTimeline({ number }: { number: string }) {
  const { t } = useTranslation('changes');
  const timeline = useChangeTimeline(number, true);
  if (timeline.isPending) {
    return <LoadingState />;
  }

  if (timeline.isError) {
    return <ErrorState error={timeline.error} onRetry={() => void timeline.refetch()} />;
  }

  if (timeline.data.length === 0) {
    return <EmptyState message={t('detail.noTimeline')} />;
  }

  return (
    <List listStyle="decimal" density="compact" aria-label={t('detail.timelineSection')}>
      {timeline.data.map((event) => {
        const note = noteOf(event);
        const meta = [formatDateTime(event.occurredAt), event.actorName].filter(Boolean).join(META_SEPARATOR);
        return (
          <ListItem
            key={event.id}
            label={t(`timeline.events.${event.kind}`)}
            description={
              note === null ? (
                meta
              ) : (
                <VStack gap={1}>
                  <Text wordBreak="break-word">{note}</Text>
                  <Text type="supporting">{meta}</Text>
                </VStack>
              )
            }
          />
        );
      })}
    </List>
  );
}
