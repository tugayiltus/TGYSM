/**
 * @input A change.
 * @output The change's tasks grouped by phase, each phase with a button that opens a task preset with that phase.
 * @position Change detail page, Görevler section.
 */
import { Button } from '@astryxdesign/core/Button';
import { Heading } from '@astryxdesign/core/Heading';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { taskPath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { TaskStatusBadge } from '../tasks/TaskBadges';
import { TaskFormDialog } from '../tasks/TaskFormDialog';

import { CHANGE_PHASES, changeKeys, type ChangeDetail, type ChangePhase } from './api';

const META_SEPARATOR = ' · ';

export function ChangeTasks({ change }: { change: ChangeDetail }) {
  const { t } = useTranslation('changes');
  const { t: tc } = useTranslation();
  const location = useLocation();
  const queryClient = useQueryClient();
  const [phase, setPhase] = useState<ChangePhase | null>(null);
  const canAdd = change.canWork && change.status !== 'closed' && change.status !== 'cancelled';
  return (
    <VStack gap={4}>
      {CHANGE_PHASES.map((key) => {
        const tasks = change.tasksByPhase.find((group) => group.phase === key)?.tasks ?? [];
        const phaseName = t(`phases.${key}`);
        return (
          <VStack key={key} gap={2}>
            <HStack gap={2} hAlign="between" vAlign="center" wrap="wrap">
              <Heading level={3}>{phaseName}</Heading>
              {canAdd && (
                <Button
                  label={t('tasks.add')}
                  aria-label={recordActionLabel(t('tasks.add'), phaseName)}
                  variant="secondary"
                  icon={<Icon icon={Icons.plus} />}
                  onClick={() => setPhase(key)}
                />
              )}
            </HStack>
            {tasks.length === 0 ? (
              <Text type="supporting">{t('tasks.empty')}</Text>
            ) : (
              <List hasDividers density="compact" aria-label={t('tasks.caption', { phase: phaseName })}>
                {tasks.map((task) => (
                  <ListItem
                    key={task.id}
                    label={
                      <StateLink href={taskPath(task.number)} state={linkState(location)}>
                        {`${task.number}${META_SEPARATOR}${task.title}`}
                      </StateLink>
                    }
                    description={`${task.teamName}${META_SEPARATOR}${task.assigneeName ?? tc('none')}`}
                    endContent={<TaskStatusBadge status={task.status} />}
                  />
                ))}
              </List>
            )}
          </VStack>
        );
      })}
      {phase !== null && (
        <TaskFormDialog
          change={{ number: change.number, phase }}
          onClose={() => setPhase(null)}
          onCreated={() => {
            void queryClient.invalidateQueries({ queryKey: changeKeys.all });
            setPhase(null);
          }}
        />
      )}
    </VStack>
  );
}
