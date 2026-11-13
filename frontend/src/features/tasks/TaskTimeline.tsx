/**
 * @input A task.
 * @output The task history with notes, newest last, and a note box for people who work on the task.
 * @position Task detail page, main column.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { TextArea } from '@astryxdesign/core/TextArea';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { EmptyState, ErrorState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';

import { useAddTaskComment, useTaskTimeline, type TaskDetail, type TaskTimelineItem } from './api';

const META_SEPARATOR = ' · ';
const COMMENT_BODY_KEY = 'body';

/** The note text of a `commented` event; other events carry no text to show. */
function noteOf(event: TaskTimelineItem): string | null {
  if (event.kind !== 'commented' || typeof event.data !== 'object' || event.data === null) {
    return null;
  }
  const body: unknown = (event.data as Record<string, unknown>)[COMMENT_BODY_KEY];
  return typeof body === 'string' ? body : null;
}

function TimelineList({ events }: { events: readonly TaskTimelineItem[] }) {
  const { t } = useTranslation('tasks');
  if (events.length === 0) {
    return <EmptyState />;
  }
  return (
    <List listStyle="decimal" density="compact" aria-label={t('timeline.title')}>
      {events.map((event) => {
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

function NoteBox({ number }: { number: string }) {
  const { t } = useTranslation('tasks');
  const add = useAddTaskComment(number);
  const [body, setBody] = useState('');
  return (
    <form
      aria-label={t('timeline.noteForm')}
      onSubmit={(event) => {
        event.preventDefault();
        add.mutate(body.trim(), { onSuccess: () => setBody('') });
      }}
    >
      <VStack gap={3}>
        <FormAlert message={add.error ? describeError(add.error) : null} />
        {/* Locked while sending: the success handler clears the box, which would drop anything typed meanwhile. */}
        <TextArea label={t('timeline.note')} value={body} isDisabled={add.isPending} onChange={setBody} />
        <HStack>
          <Button type="submit" label={t('timeline.send')} variant="secondary" isDisabled={body.trim() === ''} isLoading={add.isPending} />
        </HStack>
      </VStack>
    </form>
  );
}

/** History is read by everyone who sees the task; only people who can work on it add notes. */
export function TaskTimeline({ task }: { task: TaskDetail }) {
  const timeline = useTaskTimeline(task.number);
  return (
    <VStack gap={4}>
      {timeline.isError && <ErrorState error={timeline.error} onRetry={() => void timeline.refetch()} />}
      {timeline.data && <TimelineList events={timeline.data} />}
      {task.canWork && <NoteBox number={task.number} />}
    </VStack>
  );
}
