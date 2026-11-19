/**
 * @input A change number, its status and whether the viewer may edit its links.
 * @output Linked incidents and requests in two groups, with a dialog to bind one and a confirmed remove.
 * @position Change detail page (full and summary view), İlgili kayıtlar section.
 */
import { Button } from '@astryxdesign/core/Button';
import { Heading } from '@astryxdesign/core/Heading';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { List, ListItem } from '@astryxdesign/core/List';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { ActionDialog } from '../../shared/feedback/ActionDialog';
import { ConfirmDialog } from '../../shared/feedback/ConfirmDialog';
import { ErrorState } from '../../shared/feedback/States';
import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { SectionCard } from '../../shared/page/SectionCard';
import { ticketPath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { TicketStatusBadge } from '../tickets/TicketBadges';

import {
  CHANGE_TICKET_LINK_KINDS,
  useChangeTickets,
  useLinkTicket,
  useUnlinkTicket,
  type ChangeStatus,
  type ChangeTicketLinkKind,
  type LinkedTicketItem,
} from './api';

const META_SEPARATOR = ' · ';
const GROUPS: readonly ChangeTicketLinkKind[] = CHANGE_TICKET_LINK_KINDS;

function LinkDialog({ number, onClose }: { number: string; onClose: () => void }) {
  const { t } = useTranslation('changes');
  const link = useLinkTicket(number);
  const [ticketNumber, setTicketNumber] = useState('');
  const [kind, setKind] = useState<ChangeTicketLinkKind | ''>('');
  return (
    <ActionDialog
      title={t('links.add')}
      confirmLabel={t('links.confirmAdd')}
      isPending={link.isPending}
      isConfirmDisabled={ticketNumber.trim() === '' || kind === ''}
      error={link.error}
      onConfirm={() => kind !== '' && link.mutate({ ticketNumber: ticketNumber.trim(), kind }, { onSuccess: onClose })}
      onClose={onClose}
    >
      <TextInput label={t('links.ticketNumber')} value={ticketNumber} onChange={setTicketNumber} />
      <Selector
        label={t('links.kind')}
        placeholder={t('links.select')}
        options={GROUPS.map((value) => ({ value, label: t(`links.kinds.${value}`) }))}
        value={kind}
        onChange={(value) => setKind(value as ChangeTicketLinkKind)}
      />
    </ActionDialog>
  );
}

function Row({ item, canEdit, onRemove }: { item: LinkedTicketItem; canEdit: boolean; onRemove: (item: LinkedTicketItem) => void }) {
  const { t } = useTranslation('changes');
  const location = useLocation();
  const text = `${item.number}${META_SEPARATOR}${item.title}`;
  return (
    <ListItem
      label={
        item.canOpen ? (
          <StateLink href={ticketPath(item.number)} state={linkState(location)}>
            {text}
          </StateLink>
        ) : (
          text
        )
      }
      description={[item.canOpen ? item.teamName : t('links.notOpenable', { team: item.teamName }), t(`links.kinds.${item.linkKind}`)].join(META_SEPARATOR)}
      endContent={
        <HStack gap={2} vAlign="center">
          <TicketStatusBadge status={item.status} />
          {canEdit && <Button label={t('links.remove')} aria-label={recordActionLabel(t('links.remove'), item.number)} variant="ghost" onClick={() => onRemove(item)} />}
        </HStack>
      }
    />
  );
}

export function ChangeTicketLinks({ number, status, canEdit }: { number: string; status: ChangeStatus; canEdit: boolean }) {
  const { t } = useTranslation('changes');
  const links = useChangeTickets(number);
  const unlink = useUnlinkTicket(number);
  const [adding, setAdding] = useState(false);
  const [removing, setRemoving] = useState<LinkedTicketItem | null>(null);
  const canAdd = canEdit && status !== 'cancelled';
  const items = links.data ?? [];

  return (
    <SectionCard
      title={t('links.title')}
      actions={canAdd && <Button label={t('links.add')} variant="secondary" size="sm" icon={<Icon icon={Icons.plus} />} onClick={() => setAdding(true)} />}
    >
      {links.isError && <ErrorState error={links.error} onRetry={() => void links.refetch()} />}
      {links.data && items.length === 0 && <Text type="supporting">{t('links.empty')}</Text>}
      <VStack gap={3}>
        {GROUPS.map((kind) => {
          const rows = items.filter((item) => item.linkKind === kind);
          if (rows.length === 0) {
            return null;
          }
          const heading = t(kind === 'prompted' ? 'links.promptedGroup' : 'links.causedGroup');
          return (
            <VStack key={kind} gap={2}>
              <Heading level={3}>{heading}</Heading>
              <List hasDividers density="compact" aria-label={heading}>
                {rows.map((item) => (
                  <Row key={item.number} item={item} canEdit={canEdit} onRemove={setRemoving} />
                ))}
              </List>
            </VStack>
          );
        })}
      </VStack>
      {adding && <LinkDialog number={number} onClose={() => setAdding(false)} />}
      {removing !== null && (
        <ConfirmDialog
          title={t('links.remove')}
          message={t('links.removeMessage')}
          confirmLabel={t('links.remove')}
          variant="danger"
          isPending={unlink.isPending}
          error={unlink.error}
          onConfirm={() => unlink.mutate(removing.number, { onSuccess: () => setRemoving(null) })}
          onCancel={() => {
            unlink.reset();
            setRemoving(null);
          }}
        />
      )}
    </SectionCard>
  );
}
