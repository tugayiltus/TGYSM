/**
 * @input A ticket.
 * @output The changes linked to the ticket, each with its link kind and status.
 * @position Ticket detail page, main column (IT staff and admins only; employees make no request).
 */
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { ErrorState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';
import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { SectionCard } from '../../shared/page/SectionCard';
import { changePath } from '../../shared/routes';
import { ChangeStatusBadge } from '../changes/ChangeBadges';

import { useTicketChanges, type LinkedChangeItem, type TicketDetail } from './api';

const META_SEPARATOR = ' · ';

function Row({ item }: { item: LinkedChangeItem }) {
  const { t } = useTranslation('tickets');
  const location = useLocation();
  const text = `${item.number}${META_SEPARATOR}${item.title}`;
  return (
    <ListItem
      label={
        item.canOpen ? (
          <StateLink href={changePath(item.number)} state={linkState(location)}>
            {text}
          </StateLink>
        ) : (
          text
        )
      }
      description={[
        item.canOpen ? item.ownerTeamName : t('changeLinks.notOpenable', { team: item.ownerTeamName }),
        t(`changeLinks.kinds.${item.linkKind}`),
        ...(item.canOpen && item.windowStart !== null ? [formatDateTime(item.windowStart)] : []),
      ].join(META_SEPARATOR)}
      endContent={<ChangeStatusBadge status={item.status} />}
    />
  );
}

export function TicketChangeLinks({ ticket, isStaff }: { ticket: TicketDetail; isStaff: boolean }) {
  const { t } = useTranslation('tickets');
  const changes = useTicketChanges(ticket.number, isStaff);
  if (!isStaff) {
    return null;
  }

  const items = changes.data ?? [];
  return (
    <SectionCard title={t('changeLinks.title')}>
      {changes.isError && <ErrorState error={changes.error} onRetry={() => void changes.refetch()} />}
      {items.length === 0 ? (
        changes.data && <Text type="supporting">{t('changeLinks.empty')}</Text>
      ) : (
        <List hasDividers density="compact" aria-label={t('changeLinks.title')}>
          {items.map((item) => (
            <Row key={item.number} item={item} />
          ))}
        </List>
      )}
    </SectionCard>
  );
}
