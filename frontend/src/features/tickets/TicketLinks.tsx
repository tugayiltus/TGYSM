/**
 * @input A ticket and whether the signed-in person is its requester.
 * @output Linked devices and related tickets, with dialogs to link a device or create a related request.
 * @position Ticket detail page.
 */
import { Button } from '@astryxdesign/core/Button';
import { Grid } from '@astryxdesign/core/Grid';
import { HStack } from '@astryxdesign/core/HStack';
import { Link } from '@astryxdesign/core/Link';
import { List, ListItem } from '@astryxdesign/core/List';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';

import { describeError } from '../../shared/api/errorMessage';
import { ActionDialog } from '../../shared/feedback/ActionDialog';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { SectionCard } from '../../shared/page/SectionCard';
import { ticketPath } from '../../shared/routes';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { useAssets, useMyAssets } from '../assets/api';

import { useCreateRelatedRequest, useLinkAsset, useReferenceRequestTypes, useTicketAssets, useUnlinkAsset, type TicketDetail } from './api';
import { TicketStatusBadge } from './TicketBadges';

const SEARCH_DEBOUNCE_MS = 300;
const ASSET_SEARCH_PAGE_SIZE = 20;
const FIRST_PAGE = 1;
/** The two cards sit side by side from this width. */
const CARD_COLUMNS = { minWidth: 320 } as const;
const LABEL_SEPARATOR = ' · ';

function AssetPicker({ ticket, value, onChange }: { ticket: TicketDetail; value: string; onChange: (id: string) => void }) {
  const { t } = useTranslation('tickets');
  const [search, setSearch] = useState('');
  const debounced = useDebouncedValue(search, SEARCH_DEBOUNCE_MS);
  // Workers pick from the inventory; the requester only from devices assigned to them.
  const inventory = useAssets({ search: debounced, type: undefined, status: undefined, page: FIRST_PAGE, pageSize: ASSET_SEARCH_PAGE_SIZE }, ticket.canWork);
  const mine = useMyAssets(!ticket.canWork);
  const options = useMemo(
    () =>
      ticket.canWork
        ? (inventory.data?.items ?? []).map((a) => ({ value: a.id, label: `${a.assetTag} · ${a.brand} ${a.model}` }))
        : (mine.data ?? []).map((a) => ({ value: a.id, label: `${a.assetTag} · ${a.brand} ${a.model}` })),
    [ticket.canWork, inventory.data, mine.data],
  );
  return (
    <>
      {ticket.canWork && <TextInput type="search" label={t('fields.assetSearch')} value={search} onChange={setSearch} />}
      <Selector label={t('fields.asset')} placeholder={t('fields.select')} options={options} value={value} onChange={onChange} />
    </>
  );
}

function LinkAssetDialog({ ticket, onClose }: { ticket: TicketDetail; onClose: () => void }) {
  const { t } = useTranslation('tickets');
  const link = useLinkAsset(ticket.number);
  const [assetId, setAssetId] = useState('');
  return (
    <ActionDialog
      title={t('actions.linkAsset')}
      isPending={link.isPending}
      isConfirmDisabled={assetId === ''}
      error={link.error}
      onConfirm={() => link.mutate(assetId, { onSuccess: onClose })}
      onClose={onClose}
    >
      <AssetPicker ticket={ticket} value={assetId} onChange={setAssetId} />
    </ActionDialog>
  );
}

function RelatedRequestDialog({ ticket, onClose }: { ticket: TicketDetail; onClose: () => void }) {
  const { t } = useTranslation('tickets');
  const { t: tc } = useTranslation();
  const navigate = useNavigate();
  const types = useReferenceRequestTypes();
  const create = useCreateRelatedRequest(ticket.number);
  const [typeId, setTypeId] = useState('');
  return (
    <ActionDialog
      title={t('actions.relatedRequest')}
      confirmLabel={tc('actions.create')}
      isPending={create.isPending}
      isConfirmDisabled={typeId === ''}
      error={create.error}
      onConfirm={() => create.mutate(typeId, { onSuccess: (request) => void navigate(ticketPath(request.number)) })}
      onClose={onClose}
    >
      <Selector label={t('fields.requestType')} placeholder={t('fields.select')} options={(types.data ?? []).map((r) => ({ value: r.id, label: r.name }))} value={typeId} onChange={setTypeId} />
    </ActionDialog>
  );
}

/** Related tickets and linked devices (both respect access scope on the server). */
export function TicketLinks({ ticket, isRequester }: { ticket: TicketDetail; isRequester: boolean }) {
  const { t } = useTranslation('tickets');
  const assets = useTicketAssets(ticket.number);
  const unlink = useUnlinkAsset(ticket.number);
  const [dialog, setDialog] = useState<'asset' | 'related' | null>(null);
  const open = ticket.status !== 'completed' && ticket.status !== 'cancelled';

  const linkedAssets = assets.data ?? [];
  return (
    <Grid columns={CARD_COLUMNS} gap={4} align="start">
      <SectionCard title={t('detail.assets')}>
        <FormAlert message={unlink.error ? describeError(unlink.error) : null} />
        {linkedAssets.length === 0 ? (
          <Text type="supporting">{t('detail.noAssets')}</Text>
        ) : (
          <List hasDividers density="compact" aria-label={t('detail.assets')}>
            {linkedAssets.map((asset) => (
              <ListItem
                key={asset.assetId}
                label={[asset.assetTag, `${asset.brand} ${asset.model}`].join(LABEL_SEPARATOR)}
                endContent={
                  ticket.canWork &&
                  open && <Button label={t('actions.unlinkAsset')} aria-label={recordActionLabel(t('actions.unlinkAsset'), asset.assetTag)} variant="ghost" onClick={() => unlink.mutate(asset.assetId)} />
                }
              />
            ))}
          </List>
        )}
        {open && (ticket.canWork || isRequester) && (
          <HStack>
            <Button label={t('actions.linkAsset')} variant="secondary" onClick={() => setDialog('asset')} />
          </HStack>
        )}
      </SectionCard>
      <SectionCard title={t('detail.related')}>
        {ticket.relatedTickets.length > 0 && (
          <List hasDividers density="compact" aria-label={t('detail.related')}>
            {ticket.relatedTickets.map((related) => (
              <ListItem
                key={related.number}
                label={<Link href={ticketPath(related.number)}>{[related.number, related.subject].join(LABEL_SEPARATOR)}</Link>}
                endContent={<TicketStatusBadge status={related.status} />}
              />
            ))}
          </List>
        )}
        {ticket.canWork && open && ticket.kind === 'incident' && (
          <HStack>
            <Button label={t('actions.relatedRequest')} variant="secondary" onClick={() => setDialog('related')} />
          </HStack>
        )}
      </SectionCard>
      {dialog === 'asset' && <LinkAssetDialog ticket={ticket} onClose={() => setDialog(null)} />}
      {dialog === 'related' && <RelatedRequestDialog ticket={ticket} onClose={() => setDialog(null)} />}
    </Grid>
  );
}
