/**
 * @input A ticket.
 * @output The tasks opened from the ticket, with a button that opens a new linked task.
 * @position Ticket detail page, main column (IT staff and admins only).
 */
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate } from 'react-router';

import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { SectionCard } from '../../shared/page/SectionCard';
import { taskPath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { TaskStatusBadge } from '../tasks/TaskBadges';
import { TaskFormDialog } from '../tasks/TaskFormDialog';

import type { TicketDetail } from './api';

const isClosed = (t: TicketDetail) => t.status === 'completed' || t.status === 'cancelled';
const META_SEPARATOR = ' · ';

export function TicketTasks({ ticket }: { ticket: TicketDetail }) {
  const { t } = useTranslation('tasks');
  const location = useLocation();
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);
  const canAdd = ticket.canWork && !isClosed(ticket);
  if (!canAdd && ticket.tasks.length === 0) {
    return null;
  }

  return (
    <SectionCard
      title={t('ticketSection.title')}
      actions={canAdd && <Button label={t('ticketSection.add')} variant="secondary" size="sm" icon={<Icon icon={Icons.plus} />} onClick={() => setCreating(true)} />}
    >
      {ticket.tasks.length === 0 ? (
        <Text type="supporting">{t('ticketSection.empty')}</Text>
      ) : (
        <List hasDividers density="compact" aria-label={t('ticketSection.title')}>
          {ticket.tasks.map((task) => (
            <ListItem
              key={task.number}
              label={
                <StateLink href={taskPath(task.number)} state={linkState(location)}>
                  {`${task.number}${META_SEPARATOR}${task.title}`}
                </StateLink>
              }
              description={task.assigneeName ?? task.teamName}
              endContent={<TaskStatusBadge status={task.status} />}
            />
          ))}
        </List>
      )}
      {creating && <TaskFormDialog ticketNumber={ticket.number} onClose={() => setCreating(false)} onCreated={(task) => void navigate(taskPath(task.number))} />}
    </SectionCard>
  );
}
