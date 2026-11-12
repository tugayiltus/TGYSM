/**
 * @input A task.
 * @output The work actions the task allows: direct buttons and buttons that open a dialog.
 * @position Task detail page, first in the side column.
 */
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { useIsFetching } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { SectionCard } from '../../shared/page/SectionCard';

import { taskKeys, useTaskAction, type TaskDetail } from './api';
import { TaskActionDialog, type TaskDialogKind } from './TaskActionDialog';

type DirectKind = 'claim' | 'start' | 'resume' | 'reopen';

const isClosed = (t: TaskDetail) => t.status === 'completed' || t.status === 'cancelled';

/** Mirrors the domain state machine so only allowed actions show (the server still decides). */
function availableActions(t: TaskDetail): { direct: DirectKind[]; dialogs: TaskDialogKind[] } {
  if (!t.canWork) {
    return { direct: [], dialogs: [] };
  }

  if (isClosed(t)) {
    return { direct: ['reopen'], dialogs: [] };
  }

  const direct: DirectKind[] = [];
  if (t.assigneeId === null) {
    direct.push('claim');
  }

  if (t.status === 'open') {
    direct.push('start');
  }

  if (t.status === 'on_hold') {
    direct.push('resume');
  }

  const dialogs: TaskDialogKind[] = [];
  if (t.status === 'open' || t.status === 'in_progress') {
    dialogs.push('hold', 'complete');
  }

  dialogs.push('assign', 'transfer', 'cancel');
  return { direct, dialogs };
}

export function TaskActions({ task }: { task: TaskDetail }) {
  const { t } = useTranslation('tasks');
  const direct = useTaskAction(task);
  const [dialog, setDialog] = useState<TaskDialogKind | null>(null);
  const actions = availableActions(task);
  // Every action sends the version shown here; wait for a refetch so a dialog does not open with an old version.
  // A running action changes the version too, so the other buttons wait for its answer.
  const busy = useIsFetching({ queryKey: taskKeys.detail(task.number) }) > 0 || direct.isPending;
  if (actions.direct.length === 0 && actions.dialogs.length === 0) {
    return null;
  }

  return (
    <SectionCard title={t('actions.title')}>
      <FormAlert message={direct.error ? describeError(direct.error) : null} />
      <HStack gap={2} wrap="wrap">
        {/* One primary action per group: the first direct step; the rest are secondary. */}
        {actions.direct.map((kind, index) => (
          <Button
            key={kind}
            label={t(`actions.${kind}`)}
            variant={index === 0 ? 'primary' : 'secondary'}
            isDisabled={busy}
            isLoading={direct.isPending && direct.variables?.kind === kind}
            onClick={() => direct.mutate({ kind })}
          />
        ))}
        {actions.dialogs.map((kind) => (
          <Button
            key={kind}
            label={t(`actions.${kind}`)}
            variant={kind === 'cancel' ? 'destructive' : 'secondary'}
            isDisabled={busy}
            onClick={() => {
              direct.reset();
              setDialog(kind);
            }}
          />
        ))}
      </HStack>
      {dialog !== null && <TaskActionDialog key={dialog} task={task} kind={dialog} onClose={() => setDialog(null)} />}
    </SectionCard>
  );
}
