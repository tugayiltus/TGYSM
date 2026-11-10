/**
 * @input The ticket number from the route.
 * @output The ticket: status, description, links, articles, comments, history, actions, SLA and details.
 * @position `/tickets/:number`, inside the app shell.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Button } from '@astryxdesign/core/Button';
import { Grid, GridSpan } from '@astryxdesign/core/Grid';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { useLocation, useParams } from 'react-router';

import { FormAlert } from '../../shared/feedback/FormAlert';
import { ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';
import { useIsPhone } from '../../shared/list/useIsPhone';
import { useBreadcrumbLabel } from '../../shared/navigation/breadcrumbLabels';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { useReturnTo } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { SectionCard } from '../../shared/page/SectionCard';
import { ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { useSession } from '../auth/session';

import { useComments, useTicket, useTimeline, type TicketDetail } from './api';
import { CommentBox, CommentList } from './Comments';
import { SlaSummary } from './SlaBadge';
import { TicketActions } from './TicketActions';
import { TicketArticles } from './TicketArticles';
import { PriorityBadge, TicketStatusBadge } from './TicketBadges';
import { TicketLinks } from './TicketLinks';
import { Timeline } from './Timeline';

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

function ApprovalBox({ ticket }: { ticket: TicketDetail }) {
  const { t } = useTranslation('tickets');
  const approval = ticket.approval;
  if (approval === null) {
    return null;
  }
  const approver = approval.approverName ? t('detail.approver', { name: approval.approverName }) : t('detail.noApprover');
  return (
    <Banner
      status="warning"
      title={`${t('detail.approval')}: ${t(`approvalStatuses.${approval.status}`)}`}
      description={approval.reason ? `${approver}${META_SEPARATOR}${approval.reason}` : approver}
    />
  );
}

/** Navigation state from the new-ticket page when the files could not be uploaded. */
export interface TicketDetailState {
  /** Set when the ticket was just created, so the form's leave guard lets this navigation through. */
  created?: boolean;
  uploadError?: string;
}

/** Keyed by number: switching tickets starts with fresh drafts, dialogs and errors. */
export function TicketDetailPage() {
  const { number = '' } = useParams();
  return <TicketDetail key={number} number={number} />;
}

function TicketDetail({ number }: { number: string }) {
  const { t } = useTranslation('tickets');
  const location = useLocation();
  const uploadError = (location.state as TicketDetailState | null)?.uploadError ?? null;
  const me = useSession();
  const ticket = useTicket(number);
  const comments = useComments(number);
  const timeline = useTimeline(number);
  const backTo = useReturnTo(ROUTES.tickets);
  const isPhone = useIsPhone();
  // Only from loaded data: a 404 must not show the number typed into the URL as a title.
  useBreadcrumbLabel('ticketDetail', ticket.data?.number);

  if (ticket.isPending) {
    return <LoadingState />;
  }
  if (ticket.isError) {
    return <ErrorState error={ticket.error} onRetry={() => void ticket.refetch()} />;
  }
  const item = ticket.data;

  const side = (
    <VStack gap={4}>
      <TicketActions ticket={item} />
      <ApprovalBox ticket={item} />
      <SectionCard title={t('sla.title')}>
        <SlaSummary sla={item.sla} showDue />
      </SectionCard>
      <SectionCard>
        <MetadataList>
          <Field label={t('detail.requester')} value={item.requesterName} />
          <Field label={t('detail.team')} value={item.teamName} />
          <Field label={t('detail.assignee')} value={item.assigneeName} />
          <Field label={t('detail.category')} value={item.categoryName} />
          <Field label={t('detail.location')} value={item.locationName} />
          {item.kind === 'request' && <Field label={t('detail.requestType')} value={item.requestTypeName} />}
          <Field label={t('detail.createdAt')} value={formatDateTime(item.createdAt)} />
        </MetadataList>
      </SectionCard>
    </VStack>
  );
  const main = (
    <VStack gap={4}>
      <FormAlert message={uploadError} />
      <SectionCard>
        <MetadataList label={{ position: 'top' }}>
          <Field label={t('detail.description')} value={item.description} />
          {item.holdNote && <Field label={t('detail.holdNote')} value={item.holdNote} />}
          {item.resolution && <Field label={t('detail.resolution')} value={item.resolution} />}
          {item.rootCause && <Field label={t('detail.rootCause')} value={item.rootCause} />}
          {item.cancelReason && <Field label={t('detail.cancelReason')} value={item.cancelReason} />}
        </MetadataList>
      </SectionCard>
      <TicketLinks ticket={item} isRequester={item.requesterId === me.id} />
      <TicketArticles ticket={item} />
      <SectionCard title={t('detail.comments')}>
        {comments.isError && <ErrorState error={comments.error} onRetry={() => void comments.refetch()} />}
        {comments.data && <CommentList ticket={item} comments={comments.data} />}
        <CommentBox ticket={item} />
      </SectionCard>
      <SectionCard title={t('detail.timeline')}>
        {timeline.isError && <ErrorState error={timeline.error} onRetry={() => void timeline.refetch()} />}
        {timeline.data && <Timeline events={timeline.data} />}
      </SectionCard>
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.myTickets.icon}
        titleOverride={item.subject}
        actions={<Button label={t('detail.back')} variant="secondary" href={backTo} icon={<Icon icon={Icons.arrowLeft} />} />}
      />
      <HStack gap={2} vAlign="center" wrap="wrap">
        <Text type="supporting">{`${item.number}${META_SEPARATOR}${t(`kinds.${item.kind}`)}`}</Text>
        <TicketStatusBadge status={item.status} />
        <PriorityBadge priority={item.priority} />
        {item.holdReason && <Text type="supporting">{t(`holdReasons.${item.holdReason}`)}</Text>}
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
