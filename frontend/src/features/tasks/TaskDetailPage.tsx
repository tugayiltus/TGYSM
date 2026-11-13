/**
 * @input The task number from the route.
 * @output The task: status, description, linked ticket, result, history with notes, actions and details.
 * @position `/tasks/:number`, inside the app shell (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { Grid, GridSpan } from '@astryxdesign/core/Grid';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { useLocation, useParams } from 'react-router';

import { ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDate, formatDateTime } from '../../shared/lib/format';
import { useIsPhone } from '../../shared/list/useIsPhone';
import { useBreadcrumbLabel } from '../../shared/navigation/breadcrumbLabels';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState, useReturnTo } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { StateLink } from '../../shared/navigation/StateLink';
import { SectionCard } from '../../shared/page/SectionCard';
import { ROUTES, ticketPath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { PriorityBadge, TicketStatusBadge } from '../tickets/TicketBadges';

import { useTask, type TaskDetail } from './api';
import { TaskActions } from './TaskActions';
import { OverdueBadge, TaskStatusBadge } from './TaskBadges';
import { TaskTimeline } from './TaskTimeline';

/** Wide screens: content takes two of three columns, the side column one. */
const WIDE_COLUMNS = 3;
const MAIN_SPAN = 2;
const META_SEPARATOR = ' · ';
const LINE_BREAK = /\r?\n/;

/** A labelled value. Each typed line is its own text line, so the author's line breaks stay. */
function Field({ label, value }: { label: string; value: string | null }) {
  const { t: tc } = useTranslation();
  const lines = (value ?? tc('none')).split(LINE_BREAK);
  return (
    <MetadataListItem label={label}>
      <VStack gap={0}>
        {lines.map((line, index) => (
          <Text key={index} wordBreak="break-word">
            {line}
          </Text>
        ))}
      </VStack>
    </MetadataListItem>
  );
}

/** The linked ticket, or a plain notice when it is outside the viewer's ticket scope (ADR-0031). */
function LinkedTicket({ task }: { task: TaskDetail }) {
  const { t } = useTranslation('tasks');
  const { t: tt } = useTranslation('tickets');
  const location = useLocation();
  const link = task.link;
  if (link === null) {
    return <Text type="supporting">{t('detail.standalone')}</Text>;
  }

  if (!link.accessible || link.number === null) {
    return <Text type="supporting">{t('detail.noAccess')}</Text>;
  }

  return (
    <HStack gap={2} vAlign="center" wrap="wrap">
      <StateLink href={ticketPath(link.number)} state={linkState(location)}>
        {`${link.number}${META_SEPARATOR}${link.title ?? ''}`}
      </StateLink>
      {link.kind && <Text type="supporting">{tt(`kinds.${link.kind}`)}</Text>}
      {link.status && <TicketStatusBadge status={link.status} />}
    </HStack>
  );
}

/** Keyed by number: switching tasks starts with fresh dialogs and errors. */
export function TaskDetailPage() {
  const { number = '' } = useParams();
  return <TaskDetailView key={number} number={number} />;
}

function TaskDetailView({ number }: { number: string }) {
  const { t } = useTranslation('tasks');
  const { t: tt } = useTranslation('tickets');
  const task = useTask(number);
  const backTo = useReturnTo(ROUTES.tasks);
  const isPhone = useIsPhone();
  // Only from loaded data: a 404 must not show the number typed into the URL as a title.
  useBreadcrumbLabel('taskDetail', task.data?.number);
  if (task.isPending) {
    return <LoadingState />;
  }

  if (task.isError) {
    return <ErrorState error={task.error} onRetry={() => void task.refetch()} />;
  }

  const item = task.data;
  const side = (
    <VStack gap={4}>
      <TaskActions task={item} />
      <SectionCard>
        <MetadataList>
          <Field label={t('detail.team')} value={item.teamName} />
          <Field label={t('detail.assignee')} value={item.assigneeName} />
          <Field label={t('detail.dueDate')} value={item.dueDate ? formatDate(item.dueDate) : null} />
          <Field label={t('detail.createdBy')} value={item.createdByName} />
          <Field label={t('detail.createdAt')} value={formatDateTime(item.createdAt)} />
        </MetadataList>
      </SectionCard>
    </VStack>
  );
  const main = (
    <VStack gap={4}>
      <SectionCard title={t('detail.link')}>
        <LinkedTicket task={item} />
      </SectionCard>
      <SectionCard>
        <MetadataList label={{ position: 'top' }}>
          <Field label={t('detail.description')} value={item.description} />
          {item.holdNote && <Field label={t('detail.holdNote')} value={item.holdNote} />}
          {item.resultNote && <Field label={t('detail.resultNote')} value={item.resultNote} />}
          {item.cancelReason && <Field label={t('detail.cancelReason')} value={item.cancelReason} />}
        </MetadataList>
      </SectionCard>
      <SectionCard title={t('timeline.title')}>
        <TaskTimeline task={item} />
      </SectionCard>
    </VStack>
  );
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.tasks.icon}
        titleOverride={item.title}
        actions={<Button label={t('detail.back')} variant="secondary" href={backTo} icon={<Icon icon={Icons.arrowLeft} />} />}
      />
      <HStack gap={2} vAlign="center" wrap="wrap">
        <Text type="supporting">{item.number}</Text>
        <TaskStatusBadge status={item.status} />
        <PriorityBadge priority={item.priority} />
        {item.isOverdue && <OverdueBadge />}
        {item.holdReason && <Text type="supporting">{tt(`holdReasons.${item.holdReason}`)}</Text>}
      </HStack>
      {/* Phones show the actions before the long content; wide screens put them in a side column. */}
      {isPhone ? (
        <VStack gap={4}>
          {side}
          {main}
        </VStack>
      ) : (
        <Grid columns={WIDE_COLUMNS} gap={4} align="start">
          <GridSpan columns={MAIN_SPAN}>{main}</GridSpan>
          {side}
        </Grid>
      )}
    </VStack>
  );
}
