/**
 * @input Nothing; reads the session.
 * @output The role-based start page: report actions, IT queues, latest records and more links.
 * @position `/`, inside the app shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Grid } from '@astryxdesign/core/Grid';
import { Heading } from '@astryxdesign/core/Heading';
import { HStack } from '@astryxdesign/core/HStack';
import { List, ListItem } from '@astryxdesign/core/List';
import { VStack } from '@astryxdesign/core/VStack';
import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { StateLink } from '../../shared/navigation/StateLink';
import { ActionCard, type ActionCardProps } from '../../shared/page/ActionCard';
import { ROUTES, ticketPath } from '../../shared/routes';
import { isItStaffOrAdmin, useSession } from '../auth/session';
import { useTickets, type TicketQueue, type TicketStatus } from '../tickets/api';
import { TicketStatusBadge } from '../tickets/TicketBadges';
import { QueueCard } from '../workspace/QueueCard';

const RECENT_COUNT = 5;
const FIRST_PAGE = 1;
const HOME_QUEUES: readonly TicketQueue[] = ['assigned_to_me', 'team', 'sla_risk'];

/** Cards reflow from one column on phones to several on wide screens. */
const CARD_MIN_WIDTH = 260;
const CARD_COLUMNS = { minWidth: CARD_MIN_WIDTH } as const;

function ReportActions() {
  const { t } = useTranslation('tickets');
  const actions: ActionCardProps[] = [
    { to: ROUTES.newIncident, title: t('home.incidentCard'), description: t('home.incidentCardDescription'), icon: 'warning', iconColor: 'error' },
    { to: ROUTES.newRequest, title: t('home.requestCard'), description: t('home.requestCardDescription'), icon: 'plus' },
  ];
  return (
    <Grid columns={CARD_COLUMNS} gap={3}>
      {actions.map((action) => (
        <ActionCard key={action.to} {...action} />
      ))}
    </Grid>
  );
}

function RecentTicketRow({ number, subject, status, state }: { number: string; subject: string; status: TicketStatus; state: ReturnType<typeof linkState> }) {
  const link = useRef<HTMLAnchorElement>(null);
  return (
    <ListItem
      label={
        <StateLink href={ticketPath(number)} state={state} ref={link}>
          {subject}
        </StateLink>
      }
      description={number}
      interactiveRef={link}
      endContent={<TicketStatusBadge status={status} />}
    />
  );
}

function RecentTickets() {
  const { t } = useTranslation('tickets');
  const location = useLocation();
  const tickets = useTickets({ queue: 'mine', status: undefined, kind: undefined, priority: undefined, search: '', page: FIRST_PAGE, pageSize: RECENT_COUNT });
  return (
    <Card padding={0}>
      <VStack gap={0}>
        <HStack gap={2} hAlign="between" vAlign="center" padding={4}>
          <Heading level={2}>{t('home.recentTitle')}</Heading>
          <Button label={t('home.allTickets')} variant="ghost" href={ROUTES.tickets} />
        </HStack>
        {tickets.isPending && <LoadingState rows={RECENT_COUNT} />}
        {tickets.isError && <ErrorState error={tickets.error} onRetry={() => void tickets.refetch()} />}
        {tickets.data?.items.length === 0 && <EmptyState message={t('home.recentEmpty')} />}
        {tickets.data !== undefined && tickets.data.items.length > 0 && (
          <List hasDividers aria-label={t('home.recentTitle')}>
            {tickets.data.items.map((ticket) => (
              <RecentTicketRow key={ticket.number} number={ticket.number} subject={ticket.subject} status={ticket.status} state={linkState(location)} />
            ))}
          </List>
        )}
      </VStack>
    </Card>
  );
}

function MoreLinks() {
  const { t } = useTranslation('tickets');
  const { t: tk } = useTranslation('kb');
  const { t: ta } = useTranslation('assets');
  const links: ActionCardProps[] = [
    { to: ROUTES.kb, title: tk('home.card'), description: tk('home.cardDescription'), icon: 'book' },
    { to: ROUTES.myAssets, title: ta('home.mineCard'), description: ta('home.mineCardDescription'), icon: 'laptop' },
  ];
  return (
    <VStack gap={3}>
      <Heading level={2}>{t('home.moreTitle')}</Heading>
      <Grid columns={CARD_COLUMNS} gap={3}>
        {links.map((link) => (
          <ActionCard key={link.to} {...link} />
        ))}
      </Grid>
    </VStack>
  );
}

/** Employees get the report actions and their latest records; IT staff also their queues. */
export function HomePage() {
  const { t } = useTranslation('auth');
  const { t: tt } = useTranslation('tickets');
  const me = useSession();
  return (
    <VStack gap={6}>
      <PageHeader icon={ROUTE_META.home.icon} titleOverride={t('home.greeting', { name: me.firstName })} />
      <ReportActions />
      {isItStaffOrAdmin(me.role) && (
        <VStack gap={3}>
          <Heading level={2}>{tt('home.queuesTitle')}</Heading>
          <Grid columns={CARD_COLUMNS} gap={3}>
            {HOME_QUEUES.map((queue) => (
              <QueueCard key={queue} queue={queue} />
            ))}
          </Grid>
        </VStack>
      )}
      <RecentTickets />
      <MoreLinks />
    </VStack>
  );
}
