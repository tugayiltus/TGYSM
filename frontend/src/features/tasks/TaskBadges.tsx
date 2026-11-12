/**
 * @input A task status, or whether a task is overdue.
 * @output A colored token with the translated name; color is never the only cue.
 * @position Task lists, task detail and the work list.
 */
import { Token } from '@astryxdesign/core/Token';
import type { ComponentProps } from 'react';
import { useTranslation } from 'react-i18next';

import type { TaskStatus } from './api';

type TokenColor = NonNullable<ComponentProps<typeof Token>['color']>;

/** Same colors as ticket statuses, so one state reads the same everywhere. */
const STATUS_COLOR: Record<TaskStatus, TokenColor> = {
  open: 'blue',
  in_progress: 'cyan',
  on_hold: 'yellow',
  completed: 'green',
  cancelled: 'gray',
};

export function TaskStatusBadge({ status }: { status: TaskStatus }) {
  const { t } = useTranslation('tasks');
  return <Token size="sm" color={STATUS_COLOR[status]} label={t(`statuses.${status}`)} />;
}

export function OverdueBadge() {
  const { t } = useTranslation('tasks');
  return <Token size="sm" color="red" label={t('detail.overdue')} />;
}
